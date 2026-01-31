using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
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
        private readonly ConcurrentDictionary<Type, BroadcastHandler[]> _listeners = new ConcurrentDictionary<Type, BroadcastHandler[]>();

        // 每个广播类型一个锁对象（仅在写入时使用）
        private readonly ConcurrentDictionary<Type, object> _locks = new ConcurrentDictionary<Type, object>();

        private readonly CFLogger _logger;

        private readonly CFPool<BroadcastHandler> _pool = new CFPool<BroadcastHandler>(() => new BroadcastHandler(), maxCapacity: 256, prewarm: 16);
        private bool _disposed;

        internal BroadcastManager(CFLogger logger, CFExecutionOptions options = null)
        {
            _logger = logger;
            if(options != null) Options = options;
            EnsureMainThread = Options.EnsureMainThread;
        }

        public bool EnsureMainThread { get; set; } = true;
        public CFExecutionOptions Options { get; set; } = new CFExecutionOptions();

        #region 订阅

        // 支持带 CancellationToken 的异步订阅
        public void Subscribe<T>(Func<T, CancellationToken, UniTask> action, int priority = 100)
            where T : IBroadcastData
        {
            Type type = typeof(T);
            object locker = _locks.GetOrAdd(type, _ => new object());

            BroadcastHandler handler = _pool.Get();
            handler.Set(priority, action, EnsureMainThread);

            lock (locker)
            {
                BroadcastHandler[] oldArr = _listeners.TryGetValue(type, out BroadcastHandler[] arr) ? arr : Array.Empty<BroadcastHandler>();
                BroadcastHandler[] newArr = new BroadcastHandler[oldArr.Length + 1];
                Array.Copy(oldArr, newArr, oldArr.Length);
                newArr[^1] = handler;
                Array.Sort(newArr);
                _listeners[type] = newArr;
            }
        }

        #endregion

        public async UniTask DisposeAsync()
        {
            if(_disposed) return;
            _disposed = true;
            _listeners.Clear();
            _locks.Clear();
            _pool.Dispose();
            _logger.LogInfo("广播管理异步卸载完成!");
            await UniTask.CompletedTask;
        }

        #region 取消注册（按目标委托删除）

        public void Unsubscribe<T>(Func<T, CancellationToken, UniTask> handler) where T : IBroadcastData
        {
            RemoveHandler(typeof(T), h => h.Handler == (Delegate)handler, out BroadcastHandler removed);
            if(removed != null) _pool.Return(removed);
        }

        private void RemoveHandler(Type type, Predicate<BroadcastHandler> match, out BroadcastHandler removed)
        {
            removed = null;
            if(!_listeners.TryGetValue(type, out BroadcastHandler[] oldArr) || oldArr.Length == 0) return;

            object locker = _locks.GetOrAdd(type, _ => new object());
            lock (locker)
            {
                oldArr = _listeners.TryGetValue(type, out BroadcastHandler[] arr2) ? arr2 : Array.Empty<BroadcastHandler>();
                if(oldArr.Length == 0) return;

                int idx = Array.FindIndex(oldArr, h => match(h));
                if(idx < 0) return;

                removed = oldArr[idx];

                if(oldArr.Length == 1)
                {
                    _listeners[type] = Array.Empty<BroadcastHandler>();
                    // 清理锁对象，防止内存泄漏
                    _locks.TryRemove(type, out _);
                }
                else
                {
                    BroadcastHandler[] newArr = new BroadcastHandler[oldArr.Length - 1];
                    if(idx > 0) Array.Copy(oldArr, 0, newArr, 0, idx);
                    if(idx < oldArr.Length - 1) Array.Copy(oldArr, idx + 1, newArr, idx, oldArr.Length - idx - 1);
                    _listeners[type] = newArr;
                }
            }
        }

        #endregion

        #region 广播

        public async UniTask Broadcast<T>(T data, CancellationToken externalCt = default) where T : IBroadcastData
        {
            Type broadcastType = typeof(T);
            if(!_listeners.TryGetValue(broadcastType, out BroadcastHandler[] snapshot) || snapshot.Length == 0) return;

            using CFExecutionContext context = new CFExecutionContext(Options, externalCt, Options.OverallTimeout);

            DateTime startTs = DateTime.UtcNow;
            _logger.LogDebug($"[Broadcast-Start]\ntype={broadcastType.Name} data={data}\nhandlers={snapshot.Length}");

            if(Options.BroadcastConcurrency == ConcurrencyMode.Concurrent)
            {
                List<UniTask> taskList = new List<UniTask>(snapshot.Length);
                CancellationTokenSource stopOnErrorCts = new CancellationTokenSource();

                foreach (BroadcastHandler t in snapshot)
                {
                    if(Options.CancellationPolicy == CancellationPolicy.CancelAll &&
                       context.CancellationToken.IsCancellationRequested)
                        break;

                    CancellationTokenSource perHandlerCts = CreatePerHandlerCts(context.CancellationToken, Options.PerHandlerTimeout);
                    CancellationTokenSource linkedCts =
                        CancellationTokenSource.CreateLinkedTokenSource(perHandlerCts.Token, stopOnErrorCts.Token);
                    taskList.Add(RunHandlerConcurrentWithDispose(data, t, linkedCts, stopOnErrorCts));
                }

                try
                {
                    if(taskList.Count > 0)
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
                    double dur = (DateTime.UtcNow - startTs).TotalMilliseconds;
                    _logger.LogDebug($"[Broadcast-End]\ntype={broadcastType.Name}  data={data}\ndurationMs={dur:F1}");
                }
            }
            else
            {
                foreach (BroadcastHandler h in snapshot)
                {
                    if(context.CancellationToken.IsCancellationRequested)
                    {
                        if(Options.CancellationPolicy == CancellationPolicy.CancelAll) break;
                        continue;
                    }

                    using CancellationTokenSource perHandlerCts = CreatePerHandlerCts(context.CancellationToken, Options.PerHandlerTimeout);
                    try
                    {
                        if(h.NeedsMainThread) await UniTask.SwitchToMainThread();
                        await h.Invoke(data, _logger, perHandlerCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        if(Options.CancellationPolicy == CancellationPolicy.CancelAll) break;
                    }
                    catch (Exception e)
                    {
                        _logger.LogException(e);
                        if(Options.ErrorPolicy == ErrorPolicy.StopOnError)
                            break;
                    }
                }

                double dur = (DateTime.UtcNow - startTs).TotalMilliseconds;
                _logger.LogDebug($"[Broadcast-End]\ntype={broadcastType.Name}  data={data}\ndurationMs={dur:F1}");
            }
        }

        private async UniTask InvokeHandlerConcurrent<T>(T data, BroadcastHandler h, CancellationToken ct,
            CancellationTokenSource stopOnErrorCts)
            where T : IBroadcastData
        {
            try
            {
                if(h.NeedsMainThread) await UniTask.SwitchToMainThread();
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
                if(Options.ErrorPolicy == ErrorPolicy.StopOnError)
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
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(parent);
            if(timeout > TimeSpan.Zero)
            {
                cts.CancelAfter(timeout);
            }

            return cts;
        }

        #endregion

        #region 注册/取消注册

        public void Register<T>(T broadcaster) where T : class
        {
            if(broadcaster == null)
            {
                _logger.LogError($"type={typeof(T).Name} 实例为空!");
                return;
            }

            Type type = broadcaster.GetType();
            MethodInfo[] methods = ReflectUtil.GetAllInstanceMethods(type);

            foreach (MethodInfo method in methods)
            {
                IEnumerable<BroadcastHandlerAttribute> attributes = method.GetCustomAttributes<BroadcastHandlerAttribute>(true);
                if(method.IsAbstract) continue;
                foreach (BroadcastHandlerAttribute attribute in attributes)
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    // 广播处理器必须返回 UniTask，支持 (IBroadcastData) 或 (IBroadcastData, CancellationToken) 两种签名
                    if(parameters.Length < 1 ||
                       !typeof(IBroadcastData).IsAssignableFrom(parameters[0].ParameterType) ||
                       parameters.Length == 2 && parameters[1].ParameterType != typeof(CancellationToken) ||
                       parameters.Length > 2)
                    {
                        _logger.LogWarning($"类型 {type.Name} 方法 {method.Name} 的参数不符合广播处理器的要求。");
                        continue;
                    }

                    Type returnType = method.ReturnType;
                    bool hasCancellationToken = parameters.Length == 2;
                    if(returnType != typeof(UniTask))
                    {
                        _logger.LogWarning($"类型 {type.Name} 方法 {method.Name} 必须返回 UniTask，已忽略注册。");
                        continue;
                    }

                    // 原始具体类型委托 + 统一适配器构建（性能优化：消除调用期 DynamicInvoke）
                    Delegate action;
                    Type dataType = parameters[0].ParameterType;

                    if(hasCancellationToken)
                    {
                        // 原始强类型委托：Func<TConcrete, CancellationToken, UniTask>
                        Type funcType =
                            typeof(Func<,,>).MakeGenericType(dataType, typeof(CancellationToken), typeof(UniTask));
                        Delegate original = method.CreateDelegate(funcType, broadcaster);

                        // 适配器：Func<IBroadcastData, CancellationToken, UniTask>
                        ParameterExpression dParam = Expression.Parameter(typeof(IBroadcastData), "d");
                        ParameterExpression ctParam = Expression.Parameter(typeof(CancellationToken), "ct");
                        UnaryExpression castD = Expression.Convert(dParam, dataType);
                        ConstantExpression originalConst = Expression.Constant(original);
                        InvocationExpression invokeExpr = Expression.Invoke(originalConst, castD, ctParam);

                        ConditionalExpression condition = Expression.Condition(
                            Expression.TypeIs(dParam, dataType),
                            invokeExpr,
                            Expression.Throw(
                                Expression.New(
                                    typeof(ArgumentException).GetConstructor(new[]
                                    {
                                        typeof(string)
                                    }),
                                    Expression.Constant($"广播数据类型不匹配，期望 {dataType.Name}")
                                ),
                                typeof(UniTask)
                            )
                        );

                        action = Expression
                            .Lambda<Func<IBroadcastData, CancellationToken, UniTask>>(condition, dParam, ctParam)
                            .Compile();
                    }
                    else
                    {
                        // 原始强类型委托：Func<TConcrete, UniTask>
                        Type funcType = typeof(Func<,>).MakeGenericType(dataType, typeof(UniTask));
                        Delegate original = method.CreateDelegate(funcType, broadcaster);

                        // 适配器：Func<IBroadcastData, UniTask>
                        ParameterExpression dParam = Expression.Parameter(typeof(IBroadcastData), "d");
                        UnaryExpression castD = Expression.Convert(dParam, dataType);
                        ConstantExpression originalConst = Expression.Constant(original);
                        InvocationExpression invokeExpr = Expression.Invoke(originalConst, castD);

                        ConditionalExpression condition = Expression.Condition(
                            Expression.TypeIs(dParam, dataType),
                            invokeExpr,
                            Expression.Throw(
                                Expression.New(
                                    typeof(ArgumentException).GetConstructor(new[]
                                    {
                                        typeof(string)
                                    }),
                                    Expression.Constant($"广播数据类型不匹配，期望 {dataType.Name}")
                                ),
                                typeof(UniTask)
                            )
                        );

                        action = Expression
                            .Lambda<Func<IBroadcastData, UniTask>>(condition, dParam)
                            .Compile();
                    }

                    Type broadcastType = dataType;

                    // 创建并添加 handler（Copy-on-Write）
                    object locker = _locks.GetOrAdd(broadcastType, _ => new object());
                    BroadcastHandler handler = _pool.Get();
                    handler.Set(attribute.Priority, action, attribute.RequiresMainThread);
                    handler.SourceTarget = broadcaster;
                    handler.SourceMethod = method;

                    lock (locker)
                    {
                        BroadcastHandler[] oldArr = _listeners.TryGetValue(broadcastType, out BroadcastHandler[] arr)
                            ? arr
                            : Array.Empty<BroadcastHandler>();
                        BroadcastHandler[] newArr = new BroadcastHandler[oldArr.Length + 1];
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
            Type type = broadcaster.GetType();
            MethodInfo[] methods = ReflectUtil.GetAllInstanceMethods(type);

            foreach (MethodInfo method in methods)
            {
                ParameterInfo[] parameters = method.GetParameters();
                if(parameters.Length < 1 ||
                   !typeof(IBroadcastData).IsAssignableFrom(parameters[0].ParameterType))
                {
                    continue;
                }

                Type broadcastType = parameters[0].ParameterType;
                if(!_listeners.TryGetValue(broadcastType, out BroadcastHandler[] oldArr) || oldArr.Length == 0) continue;

                object locker = _locks.GetOrAdd(broadcastType, _ => new object());
                List<BroadcastHandler> removedList = null;

                lock (locker)
                {
                    oldArr = _listeners.TryGetValue(broadcastType, out BroadcastHandler[] arr2)
                        ? arr2
                        : Array.Empty<BroadcastHandler>();
                    if(oldArr.Length == 0) continue;

                    // 找到匹配当前 broadcaster + method 的所有 handler
                    List<int> indexes = new List<int>();
                    for(var i = 0; i < oldArr.Length; i++)
                    {
                        BroadcastHandler h = oldArr[i];
                        // 先用记录的来源信息匹配（可靠）
                        bool matchBySource = h.SourceTarget == broadcaster && h.SourceMethod == method;
                        // 兼容旧数据：退回到适配后委托的 Target/Method 匹配（可能失败）
                        bool matchByDelegate = h.Handler != null && h.Handler.Target == broadcaster &&
                                               h.Handler.Method == method;
                        if(matchBySource || matchByDelegate)
                        {
                            indexes.Add(i);
                        }
                    }

                    if(indexes.Count == 0) continue;

                    removedList = new List<BroadcastHandler>(indexes.Count);
                    // 生成新数组，移除这些 index
                    int keepCount = oldArr.Length - indexes.Count;
                    BroadcastHandler[] newArr = new BroadcastHandler[keepCount];
                    var write = 0;
                    var nextIdxPos = 0;
                    int nextRemoveIdx = indexes[nextIdxPos];

                    for(var read = 0; read < oldArr.Length; read++)
                    {
                        if(read == nextRemoveIdx)
                        {
                            removedList.Add(oldArr[read]);
                            nextIdxPos++;
                            if(nextIdxPos < indexes.Count) nextRemoveIdx = indexes[nextIdxPos];
                            continue;
                        }

                        newArr[write++] = oldArr[read];
                    }

                    _listeners[broadcastType] = newArr;
                }

                if(removedList != null)
                {
                    foreach (BroadcastHandler h in removedList) _pool.Return(h);
                }
            }
        }

        #endregion
    }
}