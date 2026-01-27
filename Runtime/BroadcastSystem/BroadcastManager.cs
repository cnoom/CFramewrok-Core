using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using CFramework.Core;
using CFramework.Core.Attributes;
using CFramework.Core.Execution;
using CFramework.Core.Log;
using CFramework.Core.Utilities;
using Cysharp.Threading.Tasks;

namespace CFramework.Core.BroadcastSystem
{
    public class BroadcastManager
    {
        // 使用 Copy-on-Write 快照数组，读零锁
        private readonly ConcurrentDictionary<Type, BroadcastHandler[]> _listeners = new();

        // 每个广播类型一个锁对象（仅在写入时使用）
        private readonly ConcurrentDictionary<Type, object> _locks = new();

        private readonly CFPool<BroadcastHandler> _pool = new(() => new BroadcastHandler(), maxCapacity: 256,
            prewarm: 16);

        private readonly CFLogger _logger;

        public bool EnsureMainThread { get; set; } = true;
        public CFExecutionOptions Options { get; set; } = new();
        private bool _disposed = false;

        internal BroadcastManager(CFLogger logger, CFExecutionOptions options = null)
        {
            _logger = logger;
            if (options != null) Options = options;
            EnsureMainThread = Options.EnsureMainThread;
        }

        #region 订阅

        // 支持带 CancellationToken 的异步订阅
        public void Subscribe<T>(Func<T, CancellationToken, UniTask> action, int priority = 100)
            where T : IBroadcastData
        {
            var type = typeof(T);
            var locker = _locks.GetOrAdd(type, _ => new object());

            var handler = _pool.Get();
            handler.Set(priority, action, needsMainThread: EnsureMainThread);

            lock (locker)
            {
                var oldArr = _listeners.TryGetValue(type, out var arr) ? arr : Array.Empty<BroadcastHandler>();
                var newArr = new BroadcastHandler[oldArr.Length + 1];
                Array.Copy(oldArr, newArr, oldArr.Length);
                newArr[^1] = handler;
                Array.Sort(newArr);
                _listeners[type] = newArr;
            }
        }

        #endregion

        #region 取消注册（按目标委托删除）

        public void Unsubscribe<T>(Func<T, CancellationToken, UniTask> handler) where T : IBroadcastData
        {
            RemoveHandler(typeof(T), h => h.Handler == (Delegate)handler, out var removed);
            if (removed != null) _pool.Return(removed);
        }

        private void RemoveHandler(Type type, Predicate<BroadcastHandler> match, out BroadcastHandler removed)
        {
            removed = null;
            if (!_listeners.TryGetValue(type, out var oldArr) || oldArr.Length == 0) return;

            var locker = _locks.GetOrAdd(type, _ => new object());
            lock (locker)
            {
                oldArr = _listeners.TryGetValue(type, out var arr2) ? arr2 : Array.Empty<BroadcastHandler>();
                if (oldArr.Length == 0) return;

                int idx = Array.FindIndex(oldArr, h => match(h));
                if (idx < 0) return;

                removed = oldArr[idx];

                if (oldArr.Length == 1)
                {
                    _listeners[type] = Array.Empty<BroadcastHandler>();
                    // 清理锁对象，防止内存泄漏
                    _locks.TryRemove(type, out _);
                }
                else
                {
                    var newArr = new BroadcastHandler[oldArr.Length - 1];
                    if (idx > 0) Array.Copy(oldArr, 0, newArr, 0, idx);
                    if (idx < oldArr.Length - 1) Array.Copy(oldArr, idx + 1, newArr, idx, oldArr.Length - idx - 1);
                    _listeners[type] = newArr;
                }
            }
        }

        #endregion

        #region 广播

        public async UniTask Broadcast<T>(T data, CancellationToken externalCt = default) where T : IBroadcastData
        {
            var broadcastType = typeof(T);
            if (!_listeners.TryGetValue(broadcastType, out var snapshot) || snapshot.Length == 0) return;

            using var context = new CFExecutionContext(Options, externalCt, Options.OverallTimeout);

            var startTs = DateTime.UtcNow;
            _logger.LogDebug($"[Broadcast-Start]\ntype={broadcastType.Name} data={data}\nhandlers={snapshot.Length}");

            if (Options.BroadcastConcurrency == ConcurrencyMode.Concurrent)
            {
                var taskList = new List<UniTask>(snapshot.Length);
                var stopOnErrorCts = new CancellationTokenSource();

                foreach (var t in snapshot)
                {
                    if (Options.CancellationPolicy == CancellationPolicy.CancelAll &&
                        context.CancellationToken.IsCancellationRequested)
                        break;

                    var perHandlerCts = CreatePerHandlerCts(context.CancellationToken, Options.PerHandlerTimeout);
                    var linkedCts =
                        CancellationTokenSource.CreateLinkedTokenSource(perHandlerCts.Token, stopOnErrorCts.Token);
                    taskList.Add(RunHandlerConcurrentWithDispose(data, t, linkedCts, stopOnErrorCts));
                }

                try
                {
                    if (taskList.Count > 0)
                        await UniTask.WhenAll(taskList.ToArray());
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug($"[Broadcast-Canceled]\ntype={broadcastType.Name} data={data}");
                }
                catch (Exception e)
                {
                    _logger.LogException(e);
                }
                finally
                {
                    stopOnErrorCts?.Dispose();
                    var dur = (System.DateTime.UtcNow - startTs).TotalMilliseconds;
                    _logger.LogDebug($"[Broadcast-End]\ntype={broadcastType.Name}  data={data}\ndurationMs={dur:F1}");
                }
            }
            else
            {
                foreach (var h in snapshot)
                {
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        if (Options.CancellationPolicy == CancellationPolicy.CancelAll) break;
                        else continue;
                    }

                    using var perHandlerCts = CreatePerHandlerCts(context.CancellationToken, Options.PerHandlerTimeout);
                    try
                    {
                        if (h.NeedsMainThread) await UniTask.SwitchToMainThread();
                        await h.Invoke(data, _logger, perHandlerCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        if (Options.CancellationPolicy == CancellationPolicy.CancelAll) break;
                    }
                    catch (Exception e)
                    {
                        _logger.LogException(e);
                        if (Options.ErrorPolicy == ErrorPolicy.StopOnError)
                            break;
                    }
                }

                var dur = (System.DateTime.UtcNow - startTs).TotalMilliseconds;
                _logger.LogDebug($"[Broadcast-End]\ntype={broadcastType.Name}  data={data}\ndurationMs={dur:F1}");
            }
        }

        private async UniTask InvokeHandlerConcurrent<T>(T data, BroadcastHandler h, CancellationToken ct,
            CancellationTokenSource stopOnErrorCts)
            where T : IBroadcastData
        {
            try
            {
                if (h.NeedsMainThread) await UniTask.SwitchToMainThread();
                await h.Invoke(data, _logger, ct);
            }
            catch (OperationCanceledException)
            {
                // 协作式取消
            }
            catch (Exception e)
            {
                _logger.LogException(e);
                // 如果启用 StopOnError，取消所有其他并发任务
                if (Options.ErrorPolicy == ErrorPolicy.StopOnError)
                {
                    stopOnErrorCts?.Cancel();
                    throw;
                }
            }
        }

        // 并发包装：保证每个 handler 的 CTS 在任务结束后释放
        private async UniTask RunHandlerConcurrentWithDispose<T>(T data, BroadcastHandler h,
            CancellationTokenSource linkedCts, CancellationTokenSource stopOnErrorCts) where T : IBroadcastData
        {
            try
            {
                await InvokeHandlerConcurrent(data, h, linkedCts.Token, stopOnErrorCts);
            }
            finally
            {
                linkedCts.Dispose();
            }
        }

        // 创建可释放的 per-handler CTS（确保链接到父级，并可选超时）
        private static CancellationTokenSource CreatePerHandlerCts(CancellationToken parent, TimeSpan timeout)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(parent);
            if (timeout > TimeSpan.Zero)
            {
                cts.CancelAfter(timeout);
            }

            return cts;
        }

        #endregion

        #region 注册/取消注册

        public void Register<T>(T broadcaster) where T : class
        {
            if (broadcaster == null)
            {
                _logger.LogError($"type={typeof(T).Name} 实例为空!");
                return;
            }

            var type = broadcaster.GetType();
            var methods = ReflectUtil.GetAllInstanceMethods(type);

            foreach (var method in methods)
            {
                var attributes = method.GetCustomAttributes<BroadcastHandlerAttribute>(inherit: true);
                if (method.IsAbstract) continue;
                foreach (var attribute in attributes)
                {
                    var parameters = method.GetParameters();
                    // 广播处理器必须返回 UniTask，支持 (IBroadcastData) 或 (IBroadcastData, CancellationToken) 两种签名
                    if (parameters.Length < 1 ||
                        !typeof(IBroadcastData).IsAssignableFrom(parameters[0].ParameterType) ||
                        (parameters.Length == 2 && parameters[1].ParameterType != typeof(CancellationToken)) ||
                        parameters.Length > 2)
                    {
                        _logger.LogWarning($"类型 {type.Name} 方法 {method.Name} 的参数不符合广播处理器的要求。");
                        continue;
                    }

                    var returnType = method.ReturnType;
                    var hasCancellationToken = parameters.Length == 2;
                    if (returnType != typeof(UniTask))
                    {
                        _logger.LogWarning($"类型 {type.Name} 方法 {method.Name} 必须返回 UniTask，已忽略注册。");
                        continue;
                    }

                    // 原始具体类型委托 + 统一适配器构建（性能优化：消除调用期 DynamicInvoke）
                    Delegate action;
                    var dataType = parameters[0].ParameterType;

                    if (hasCancellationToken)
                    {
                        // 原始强类型委托：Func<TConcrete, CancellationToken, UniTask>
                        var funcType =
                            typeof(Func<,,>).MakeGenericType(dataType, typeof(CancellationToken), typeof(UniTask));
                        var original = method.CreateDelegate(funcType, broadcaster);

                        // 适配器：Func<IBroadcastData, CancellationToken, UniTask>
                        var dParam = System.Linq.Expressions.Expression.Parameter(typeof(IBroadcastData), "d");
                        var ctParam = System.Linq.Expressions.Expression.Parameter(typeof(CancellationToken), "ct");
                        var castD = System.Linq.Expressions.Expression.Convert(dParam, dataType);
                        var originalConst = System.Linq.Expressions.Expression.Constant(original);
                        var invokeExpr = System.Linq.Expressions.Expression.Invoke(originalConst, castD, ctParam);

                        var condition = System.Linq.Expressions.Expression.Condition(
                            System.Linq.Expressions.Expression.TypeIs(dParam, dataType),
                            invokeExpr,
                            System.Linq.Expressions.Expression.Throw(
                                System.Linq.Expressions.Expression.New(
                                    typeof(ArgumentException).GetConstructor(new[] { typeof(string) }),
                                    System.Linq.Expressions.Expression.Constant($"广播数据类型不匹配，期望 {dataType.Name}")
                                ),
                                typeof(UniTask)
                            )
                        );

                        action = System.Linq.Expressions.Expression
                            .Lambda<Func<IBroadcastData, CancellationToken, UniTask>>(condition, dParam, ctParam)
                            .Compile();
                    }
                    else
                    {
                        // 原始强类型委托：Func<TConcrete, UniTask>
                        var funcType = typeof(Func<,>).MakeGenericType(dataType, typeof(UniTask));
                        var original = method.CreateDelegate(funcType, broadcaster);

                        // 适配器：Func<IBroadcastData, UniTask>
                        var dParam = System.Linq.Expressions.Expression.Parameter(typeof(IBroadcastData), "d");
                        var castD = System.Linq.Expressions.Expression.Convert(dParam, dataType);
                        var originalConst = System.Linq.Expressions.Expression.Constant(original);
                        var invokeExpr = System.Linq.Expressions.Expression.Invoke(originalConst, castD);

                        var condition = System.Linq.Expressions.Expression.Condition(
                            System.Linq.Expressions.Expression.TypeIs(dParam, dataType),
                            invokeExpr,
                            System.Linq.Expressions.Expression.Throw(
                                System.Linq.Expressions.Expression.New(
                                    typeof(ArgumentException).GetConstructor(new[] { typeof(string) }),
                                    System.Linq.Expressions.Expression.Constant($"广播数据类型不匹配，期望 {dataType.Name}")
                                ),
                                typeof(UniTask)
                            )
                        );

                        action = System.Linq.Expressions.Expression
                            .Lambda<Func<IBroadcastData, UniTask>>(condition, dParam)
                            .Compile();
                    }

                    var broadcastType = dataType;

                    // 创建并添加 handler（Copy-on-Write）
                    var locker = _locks.GetOrAdd(broadcastType, _ => new object());
                    var handler = _pool.Get();
                    handler.Set(attribute.Priority, action, needsMainThread: (attribute.RequiresMainThread));
                    handler.SourceTarget = broadcaster;
                    handler.SourceMethod = method;

                    lock (locker)
                    {
                        var oldArr = _listeners.TryGetValue(broadcastType, out var arr)
                            ? arr
                            : Array.Empty<BroadcastHandler>();
                        var newArr = new BroadcastHandler[oldArr.Length + 1];
                        Array.Copy(oldArr, newArr, oldArr.Length);
                        newArr[^1] = handler;
                        Array.Sort(newArr);
                        _listeners[broadcastType] = newArr;
                    }
                }
            }
        }

        public void Unregister<T>(T broadcaster) where T : class
        {
            var type = broadcaster.GetType();
            var methods = ReflectUtil.GetAllInstanceMethods(type);

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length < 1 ||
                    !typeof(IBroadcastData).IsAssignableFrom(parameters[0].ParameterType))
                {
                    continue;
                }

                var broadcastType = parameters[0].ParameterType;
                if (!_listeners.TryGetValue(broadcastType, out var oldArr) || oldArr.Length == 0) continue;

                var locker = _locks.GetOrAdd(broadcastType, _ => new object());
                List<BroadcastHandler> removedList = null;

                lock (locker)
                {
                    oldArr = _listeners.TryGetValue(broadcastType, out var arr2)
                        ? arr2
                        : Array.Empty<BroadcastHandler>();
                    if (oldArr.Length == 0) continue;

                    // 找到匹配当前 broadcaster + method 的所有 handler
                    var indexes = new List<int>();
                    for (int i = 0; i < oldArr.Length; i++)
                    {
                        var h = oldArr[i];
                        // 先用记录的来源信息匹配（可靠）
                        bool matchBySource = (h.SourceTarget == broadcaster && h.SourceMethod == method);
                        // 兼容旧数据：退回到适配后委托的 Target/Method 匹配（可能失败）
                        bool matchByDelegate = (h.Handler != null && h.Handler.Target == broadcaster &&
                                                h.Handler.Method == method);
                        if (matchBySource || matchByDelegate)
                        {
                            indexes.Add(i);
                        }
                    }

                    if (indexes.Count == 0) continue;

                    removedList = new List<BroadcastHandler>(indexes.Count);
                    // 生成新数组，移除这些 index
                    var keepCount = oldArr.Length - indexes.Count;
                    var newArr = new BroadcastHandler[keepCount];
                    int write = 0;
                    int nextIdxPos = 0;
                    int nextRemoveIdx = indexes[nextIdxPos];

                    for (int read = 0; read < oldArr.Length; read++)
                    {
                        if (read == nextRemoveIdx)
                        {
                            removedList.Add(oldArr[read]);
                            nextIdxPos++;
                            if (nextIdxPos < indexes.Count) nextRemoveIdx = indexes[nextIdxPos];
                            continue;
                        }

                        newArr[write++] = oldArr[read];
                    }

                    _listeners[broadcastType] = newArr;
                }

                if (removedList != null)
                {
                    foreach (var h in removedList) _pool.Return(h);
                }
            }
        }

        #endregion

        public async UniTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            _listeners.Clear();
            _locks.Clear();
            _pool.Dispose();
            _logger.LogInfo("广播管理异步卸载完成!");
            await UniTask.CompletedTask;
        }
    }
}