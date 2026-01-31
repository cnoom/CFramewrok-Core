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

namespace CFramework.Core.CommandSystem
{
    public class CommandManager
    {
        // 并发安全的处理器表：命令类型 -> 处理器
        private readonly ConcurrentDictionary<Type, CommandHandler> _handlers = new ConcurrentDictionary<Type, CommandHandler>();

        private readonly CFLogger _logger;

        // 主线程 ID 与一次性处置标记
        private readonly int _mainThreadId;
        private readonly CFExecutionOptions _options;

        // 对象池（若 CFPool 非线程安全，通过锁保护）
        private readonly CFPool<CommandHandler> _pool = new CFPool<CommandHandler>(() => new CommandHandler(), maxCapacity: 128, prewarm: 8);
        private readonly object _poolLock = new object();
        private int _disposedFlag; // 0=未处置, 1=已处置

        public CommandManager(CFLogger logger, CFExecutionOptions options = null)
        {
            _logger = logger;
            _options = options ?? new CFExecutionOptions();
            _mainThreadId = Thread.CurrentThread.ManagedThreadId; // 假定在主线程构造
        }

        public bool EnsureMainThread { get; set; } = true;

        private void ThrowIfDisposed()
        {
            if(Volatile.Read(ref _disposedFlag) != 0)
                throw new ObjectDisposedException(nameof(CommandManager));
        }

        private void EnsureOnMainThread()
        {
            if(Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                throw new InvalidOperationException("该操作必须在主线程执行。");
        }

        #region 执行

        public async UniTask Execute<TCommand>(TCommand commandData, CancellationToken ct) where TCommand : ICommandData
        {
            ThrowIfDisposed();

            Type commandType = typeof(TCommand);
            if(!_handlers.TryGetValue(commandType, out CommandHandler handler))
            {
                _logger.LogWarning($"未找到命令处理器: {commandType.Name}\n{commandData}");
                return;
            }

            using CFExecutionContext context = new CFExecutionContext(_options, ct, _options.OverallTimeout);
            DateTime startTs = DateTime.UtcNow;
            _logger.LogDebug($"[Command-Start] type={commandType.Name} data={commandData}");

            if(context.CancellationToken.IsCancellationRequested) return;

            // 仅在真正调用处理器前切回主线程
            if(EnsureMainThread)
                await UniTask.SwitchToMainThread();

            try
            {
                await handler.Invoke(commandData, _logger, context.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug($"[Command-Canceled] type={commandType.Name} data={commandData}");
            }
            catch (Exception e)
            {
                _logger.LogException(e);
            }
            finally
            {
                double dur = (DateTime.UtcNow - startTs).TotalMilliseconds;
                _logger.LogDebug(
                    $"[Command-End] type={commandType.Name} data={commandData}durationMs={dur:F1}");
            }
        }

        #endregion

        #region 释放

        public async UniTask DisposeAsync()
        {
            if(Interlocked.Exchange(ref _disposedFlag, 1) == 1)
                return;

            // 并发安全地清理所有处理器并归还池对象
            foreach (KeyValuePair<Type, CommandHandler> kv in _handlers)
            {
                if(_handlers.TryRemove(kv.Key, out CommandHandler handler))
                {
                    lock (_poolLock)
                    {
                        _pool.Return(handler);
                    }
                }
            }

            lock (_poolLock)
            {
                _pool.Dispose();
            }

            _logger.LogDebug("命令管理异步卸载完成!");
            await UniTask.CompletedTask;
        }

        #endregion

        #region 订阅

        public void Subscribe<TCommand>(Func<TCommand, CancellationToken, UniTask> handler)
            where TCommand : ICommandData
        {
            ThrowIfDisposed();
            EnsureOnMainThread();

            Type commandType = typeof(TCommand);
            Subscribe(commandType, handler);
        }

        private void Subscribe(Type commandType, Delegate @delegate)
        {
            ThrowIfDisposed();
            EnsureOnMainThread();

            // 从池获取并设置
            CommandHandler commandHandler;
            lock (_poolLock)
            {
                commandHandler = _pool.Get();
            }
            commandHandler.Set(@delegate);

            if(!_handlers.TryAdd(commandType, commandHandler))
            {
                _logger.LogWarning(
                    $"命令处理器已存在: {commandType.Name}\noldHandler={_handlers[commandType].Handler.Target.GetType().Name}\nnewHandler={commandHandler.Handler.Target.GetType().Name}");

                // 先移除并回收旧的handler
                if(_handlers.TryRemove(commandType, out CommandHandler oldHandler))
                {
                    lock (_poolLock)
                    {
                        _pool.Return(oldHandler);
                    }
                }

                // 再添加新的handler
                _handlers.TryAdd(commandType, commandHandler);
            }
        }

        #endregion

        #region 取消订阅

        public void UnSubscribe<TCommand>() where TCommand : ICommandData
        {
            ThrowIfDisposed();
            EnsureOnMainThread();

            UnSubscribe(typeof(TCommand));
        }

        private void UnSubscribe(Type commandType)
        {
            ThrowIfDisposed();
            EnsureOnMainThread();

            if(_handlers.TryRemove(commandType, out CommandHandler handler))
            {
                lock (_poolLock)
                {
                    _pool.Return(handler);
                }
            }
        }

        #endregion

        #region 注册

        public void Register<T>(T commandHandler) where T : class
        {
            ThrowIfDisposed();
            EnsureOnMainThread();

            if(commandHandler == null) throw new ArgumentNullException(nameof(commandHandler));

            Type type = commandHandler.GetType();
            MethodInfo[] methods = ReflectUtil.GetAllInstanceMethods(type);

            foreach (MethodInfo method in methods)
            {
                CommandHandlerAttribute attr = method.GetCustomAttribute<CommandHandlerAttribute>(true);
                if(attr == null) continue;
                if(method.IsAbstract) continue;

                ParameterInfo[] parameters = method.GetParameters();
                Type commandType = parameters.Length > 0 ? parameters[0].ParameterType : null;
                Type returnType = method.ReturnType;

                // 命令处理器必须接收 (ICommandData, CancellationToken) 参数并返回 UniTask
                if(parameters.Length != 2 ||
                   !typeof(ICommandData).IsAssignableFrom(commandType) ||
                   parameters[1].ParameterType != typeof(CancellationToken))
                {
                    _logger.LogError(
                        $"类型 {commandHandler.GetType().Name} 方法 {method.Name} 签名无效，必须接收 (ICommandData, CancellationToken) 参数。");
                    continue;
                }

                if(returnType != typeof(UniTask))
                {
                    _logger.LogError(
                        $"类型 {commandHandler.GetType().Name} 方法 {method.Name} 必须返回 UniTask，已忽略注册。");
                    continue;
                }

                // 统一构建强类型原始委托 + 适配器，避免调用期反射与 DynamicInvoke
                Type expected = parameters[0].ParameterType;

                // 原始强类型委托：Func<TConcrete, CancellationToken, UniTask>
                Type funcType =
                    typeof(Func<,,>).MakeGenericType(expected, typeof(CancellationToken), typeof(UniTask));
                Delegate original = method.CreateDelegate(funcType, commandHandler);

                // 适配器：Func<ICommandData, CancellationToken, UniTask>
                ParameterExpression dParam = Expression.Parameter(typeof(ICommandData), "d");
                ParameterExpression ctParam = Expression.Parameter(typeof(CancellationToken), "ct");
                UnaryExpression castD = Expression.Convert(dParam, expected);
                ConstantExpression originalConst = Expression.Constant(original);
                InvocationExpression invokeExpr = Expression.Invoke(originalConst, castD, ctParam);

                // 包一层 d is T t 校验，避免无意义的 InvalidCastException
                ParameterExpression tVar = Expression.Variable(expected, "t");
                BinaryExpression assignT = Expression.Assign(tVar,
                    Expression.Convert(dParam, expected));
                // 为了显式错误，使用条件表达式：如果不是期望类型，抛异常
                ConditionalExpression condition = Expression.Condition(
                    Expression.TypeIs(dParam, expected),
                    invokeExpr,
                    Expression.Throw(
                        Expression.New(
                            typeof(ArgumentException).GetConstructor(new[]
                            {
                                typeof(string)
                            }),
                            Expression.Constant(
                                $"命令数据类型不匹配，期望 {expected.Name}，实际 {nameof(ICommandData)}")
                        ),
                        typeof(UniTask)
                    )
                );

                Delegate @delegate = Expression
                    .Lambda<Func<ICommandData, CancellationToken, UniTask>>(condition, dParam, ctParam).Compile();

                // 统一交给 Subscribe，后续 CommandHandler 中的 switch 将稳定命中
                Subscribe(commandType, @delegate);
            }
        }

        public void Unregister<T>(T commandHandler) where T : class
        {
            ThrowIfDisposed();
            EnsureOnMainThread();

            if(commandHandler == null) throw new ArgumentNullException(nameof(commandHandler));

            Type type = commandHandler.GetType();
            MethodInfo[] methods = ReflectUtil.GetAllInstanceMethods(type);
            foreach (MethodInfo method in methods)
            {
                CommandHandlerAttribute attr = method.GetCustomAttribute<CommandHandlerAttribute>(true);
                if(attr == null) continue;
                if(method.IsAbstract) continue;

                ParameterInfo[] parameters = method.GetParameters();
                if(parameters.Length != 2)
                {
                    _logger.LogError($"类型 {commandHandler.GetType().Name} 方法 {method.Name} 签名无效，它需要 2 个参数。");
                    continue;
                }

                Type commandType = parameters[0].ParameterType;
                if(!typeof(ICommandData).IsAssignableFrom(commandType))
                {
                    _logger.LogError(
                        $"类型 {commandHandler.GetType().Name} 方法 {method.Name} 的第一个参数类型无效，必须是 {nameof(ICommandData)} 的子类型。");
                    continue;
                }

                if(parameters.Length == 2 && parameters[1].ParameterType != typeof(CancellationToken))
                {
                    _logger.LogError(
                        $"类型 {commandHandler.GetType().Name} 方法 {method.Name} 的第二个参数必须是 {nameof(CancellationToken)}。");
                    continue;
                }

                UnSubscribe(commandType);
            }
        }

        #endregion
    }
}