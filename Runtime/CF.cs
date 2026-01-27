using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using CFramework.Core.BroadcastSystem;
using CFramework.Core.CommandSystem;
using CFramework.Core.Log;
using CFramework.Core.ModuleSystem;
using CFramework.Core.QuerySystem;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CFramework.Core
{
    /// <summary>
    /// CFramework 的静态访问入口类，提供日志、模块、广播、命令和查询等核心功能的统一访问接口。
    /// </summary>
    public static class CF
    {
        private static readonly object InstanceLock = new object();
        private static CFramework _instance;

        /// <summary>
        /// 自动跟踪并清理的 CTS 集合，防止内存泄漏
        /// 使用 ConcurrentDictionary 确保 CTS 和 registration 一一对应
        /// </summary>
        private static readonly ConcurrentDictionary<object, CancellationTokenSource> _trackedCts = new();

        /// <summary>
        /// 获取框架级别的取消令牌，用于取消异步操作。
        /// </summary>
        public static CancellationToken CancellationToken => _instance.CancellationToken;

        /// <summary>
        /// 获取框架是否已初始化。
        /// </summary>
        public static bool IsInitialized => _instance != null;

        /// <summary>
        /// 线程安全地初始化框架实例。若已初始化且为同一实例，则幂等返回 true；若已存在不同实例，则返回 false 并不覆盖。
        /// </summary>
        internal static bool Initialize(CFramework instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            lock (InstanceLock)
            {
                if (_instance == null)
                {
                    _instance = instance;
                    return true;
                }

                if (ReferenceEquals(_instance, instance)) return true;
#if UNITY_EDITOR
                Debug.LogWarning("CF 已初始化，忽略重复设置不同实例。");
#endif
                return false;
            }
        }

        /// <summary>
        /// 线程安全地清理框架实例。若提供 expected 且非当前实例则不清理，返回 false。
        /// </summary>
        public static bool TryClearInstance(CFramework expected = null)
        {
            lock (InstanceLock)
            {
                if (_instance == null) return true;
                if (expected != null && !ReferenceEquals(_instance, expected)) return false;
                _instance = null;
                return true;
            }
        }

        private static void EnsureInitializedOrWarn(string apiName)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"CF API '{apiName}' 在框架未初始化时被调用，调用已被忽略。");
#endif
        }

        #region 日志

        /// <summary>
        /// 获取默认的日志记录器实例。
        /// </summary>
        /// <returns>返回 CFLogger 类型的默认日志记录器实例。</returns>
        public static CFLogger DefaultLogger()
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(DefaultLogger));
                return null;
            }

            return _instance.LogManager.CFLogger;
        }

        /// <summary>
        /// 创建一个新的日志记录器实例。
        /// </summary>
        /// <param name="tag">标识新创建的日志记录器的标签。</param>
        public static CFLogger CreateLogger(string tag)
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(CreateLogger));
                return null;
            }

            return _instance.LogManager.Create(tag);
        }

        /// <summary>
        /// 移除指定标签的日志记录器。
        /// </summary>
        /// <param name="tag">要移除的日志记录器的标签。</param>
        /// <returns>如果成功移除日志记录器，则返回 true；否则返回 false。</returns>
        public static bool RemoveLogger(string tag)
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(RemoveLogger));
                return false;
            }

            return _instance.LogManager.RemoveLogger(tag);
        }

        /// <summary>
        /// 记录一条信息级别的日志。
        /// </summary>
        /// <param name="message">要记录的日志消息。</param>
        public static void LogInfo(string message)
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(LogInfo));
                return;
            }

            _instance.LogManager.CFLogger.LogInfo(message);
        }

        /// <summary>
        /// 记录调试级别的日志消息。
        /// </summary>
        /// <param name="message">要记录的日志消息。</param>
        public static void LogDebug(string message)
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(LogDebug));
                return;
            }

            _instance.LogManager.CFLogger.LogDebug(message);
        }

        /// <summary>
        /// 记录警告级别的日志消息。
        /// </summary>
        /// <param name="message">要记录的警告消息。</param>
        public static void LogWarning(string message)
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(LogWarning));
                return;
            }

            _instance.LogManager.CFLogger.LogWarning(message);
        }

        /// <summary>
        /// 记录错误级别的日志信息。
        /// </summary>
        /// <param name="message">要记录的错误消息。</param>
        public static void LogError(string message)
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(LogError));
                return;
            }

            _instance.LogManager.CFLogger.LogError(message);
        }

        /// <summary>
        /// 记录异常信息。
        /// </summary>
        /// <param name="exception">要记录的异常实例。</param>
        public static void LogException(Exception exception)
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(LogException));
                return;
            }

            _instance.LogManager.CFLogger.LogException(exception);
        }

        /// <summary>
        /// 设置指定标签的日志级别，如果未提供标签，则设置所有日志的级别。
        /// </summary>
        /// <param name="level">要设置的日志级别。</param>
        /// <param name="logTag">可选参数，指定要设置日志级别的标签。如果为空或未提供，则对所有日志生效。</param>
        public static void SetLogLevel(ICFLogger.Level level, string logTag = "")
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(SetLogLevel));
                return;
            }

            if (string.IsNullOrEmpty(logTag)) _instance.LogManager.SetLevelAll(level);
            else _instance.LogManager.SetLevel(logTag, level);
        }

        /// <summary>
        /// 启用或禁用指定标签的日志记录器；如果未提供标签，则对所有已注册日志记录器及默认日志生效。
        /// </summary>
        /// <param name="enabled">true 启用；false 禁用。</param>
        /// <param name="logTag">可选，指定目标日志记录器的标签。</param>
        public static void SetLoggerEnabled(bool enabled, string logTag = "")
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(SetLoggerEnabled));
                return;
            }

            if (string.IsNullOrEmpty(logTag)) _instance.LogManager.SetEnabledAll(enabled);
            else _instance.LogManager.SetEnabled(logTag, enabled);
        }

        /// <summary>
        /// 启用指定标签的日志输出。
        /// </summary>
        public static void EnableLogger(string logTag)
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(EnableLogger));
                return;
            }

            _instance.LogManager.SetEnabled(logTag, true);
        }

        /// <summary>
        /// 禁用指定标签的日志输出。
        /// </summary>
        public static void DisableLogger(string logTag)
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(DisableLogger));
                return;
            }

            _instance.LogManager.SetEnabled(logTag, false);
        }

        /// <summary>
        /// 启用所有日志输出（默认 Logger + 所有已注册 Logger）。
        /// </summary>
        public static void EnableAllLoggers()
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(EnableAllLoggers));
                return;
            }

            _instance.LogManager.SetEnabledAll(true);
        }

        /// <summary>
        /// 禁用所有日志输出（默认 Logger + 所有已注册 Logger）。
        /// </summary>
        public static void DisableAllLoggers()
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(DisableAllLoggers));
                return;
            }

            _instance.LogManager.SetEnabledAll(false);
        }

        #endregion

        #region 模组

        /// <summary>
        /// 注册一个模块到模块管理系统。
        /// </summary>
        /// <typeparam name="TModule">要注册的模块类型，必须实现 IModule 接口。</typeparam>
        public static UniTask RegisterModule<TModule>() where TModule : IModule, new()
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(RegisterModule));
                return UniTask.FromResult(false);
            }

            return _instance.ModuleManager.RegisterModule<TModule>();
        }

        /// <summary>
        /// 注册多个模块到模块管理系统，支持自动解析依赖排序
        /// </summary>
        /// <param name="registry">模块注册器</param>
        public static UniTask RegisterModules(ModulesRegistry registry)
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(RegisterModules));
                return UniTask.FromResult(false);
            }

            return _instance.ModuleManager.RegisterModules(registry);
        }

        /// <summary>
        /// 取消注册指定类型的模块。
        /// </summary>
        /// <typeparam name="TModule">要取消注册的模块类型，该类型必须实现 IModule 接口。</typeparam>
        /// <returns>如果成功取消注册则返回 true，否则返回 false。</returns>
        public static UniTask<bool> UnregisterModule<TModule>() where TModule : IModule
        {
            if (!IsInitialized) return UniTask.FromResult(false);
            return _instance.ModuleManager.UnregisterModule<TModule>(CancellationToken);
        }

        /// <summary>
        /// 取消注册多个模块
        /// </summary>
        /// <returns>是否全部卸载成功</returns>
        ///<remarks>按依赖倒序取消</remarks>
        public static UniTask<bool> UnregisterModules(ModulesRegistry registry)
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(UnregisterModules));
                return UniTask.FromResult(false);
            }

            return _instance.ModuleManager.UnregisterModules(registry, CancellationToken);
        }

        /// <summary>
        /// 检查指定类型的模块是否已注册。
        /// </summary>
        /// <typeparam name="TModule">要检查的模块类型，必须实现 IModule 接口。</typeparam>
        /// <returns>如果指定类型的模块已经注册，则返回 true；否则返回 false。</returns>
        public static bool IsRegistered<TModule>() where TModule : IModule
        {
            return _instance?.ModuleManager?.IsRegistered<TModule>() ?? false;
        }

        #endregion

        #region 广播

        /// <summary>
        /// 广播指定的数据。
        /// </summary>
        /// <typeparam name="TBroadcast">广播数据的类型，必须实现 IBroadcastData 接口。</typeparam>
        /// <param name="broadcast">要广播的数据实例。</param>
        public static UniTask Broadcast<TBroadcast>(TBroadcast broadcast = default, CancellationToken? ct = null)
            where TBroadcast : IBroadcastData
        {
            if (IsInitialized)
                return _instance.BroadcastManager.Broadcast(broadcast,
                    LinkedToken(CancellationToken, ct ?? CancellationToken.None));
            EnsureInitializedOrWarn(nameof(Broadcast));
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 订阅异步广播处理。
        /// </summary>
        /// <typeparam name="TSubscribe">要订阅的广播消息类型，必须实现 IBroadcastData 接口。</typeparam>
        /// <param name="subscribe">异步处理函数，带有 CancellationToken。</param>
        /// <param name="priority">优先级，默认 100。</param>
        public static void SubscribeBroadcast<TSubscribe>(Func<TSubscribe, CancellationToken, UniTask> subscribe,
            int priority = 100)
            where TSubscribe : IBroadcastData
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(SubscribeBroadcast));
                return;
            }

            _instance.BroadcastManager.Subscribe(subscribe, priority);
        }

        /// <summary>
        /// 取消订阅指定类型的广播。
        /// </summary>
        /// <typeparam name="TUnsubscribe">要取消订阅的广播消息类型，必须实现 IBroadcastData 接口。</typeparam>
        /// <param name="unsubscribe">之前订阅时提供的处理函数。</param>
        public static void UnsubscribeBroadcast<TUnsubscribe>(
            Func<TUnsubscribe, CancellationToken, UniTask> unsubscribe)
            where TUnsubscribe : IBroadcastData
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(UnsubscribeBroadcast));
                return;
            }

            _instance.BroadcastManager.Unsubscribe(unsubscribe);
        }

        #endregion

        #region 命令

        /// <summary>
        /// 执行指定的命令。
        /// </summary>
        /// <typeparam name="TCommand">要执行的命令类型，必须实现 ICommandData 接口。</typeparam>
        /// <param name="command">要执行的具体命令实例。</param>
        public static UniTask Execute<TCommand>(TCommand command = default, CancellationToken? ct = null)
            where TCommand : ICommandData
        {
            if (IsInitialized)
                return _instance.CommandManager.Execute(command,
                    LinkedToken(CancellationToken, ct ?? CancellationToken.None));
            EnsureInitializedOrWarn(nameof(Execute));
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 订阅指定类型的命令，当该类型命令被触发时执行提供的操作。
        /// </summary>
        /// <typeparam name="TCommand">要订阅的命令类型，必须实现 ICommandData 接口。</typeparam>
        /// <param name="subscribe">每当 TCommand 类型的命令被触发时将调用此委托。参数为触发的命令实例。</param>
        public static void SubscribeCommand<TCommand>(Func<TCommand, CancellationToken, UniTask> subscribe)
            where TCommand : ICommandData
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(SubscribeCommand));
                return;
            }

            _instance.CommandManager.Subscribe(subscribe);
        }

        /// <summary>
        /// 取消订阅指定类型的命令。
        /// </summary>
        /// <typeparam name="TCommand">要取消订阅的命令类型，必须实现 ICommandData 接口。</typeparam>
        public static void UnsubscribeCommand<TCommand>()
            where TCommand : ICommandData
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(UnsubscribeCommand));
                return;
            }

            _instance.CommandManager.UnSubscribe<TCommand>();
        }

        #endregion

        #region 查询

        /// <summary>
        /// 执行查询并返回结果。查询结果会被缓存，后续相同查询将直接返回缓存结果。
        /// </summary>
        /// <typeparam name="TQuery">查询数据类型，必须实现 IQueryData 接口。</typeparam>
        /// <typeparam name="TResult">查询结果的类型。</typeparam>
        /// <param name="query">要执行的查询实例，默认为 default。</param>
        /// <param name="ct">可选的取消令牌。</param>
        /// <returns>包含查询结果的异步任务。</returns>
        public static async UniTask<TResult> Query<TQuery, TResult>(TQuery query = default,
            CancellationToken? ct = null)
            where TQuery : IQueryData
        {
            if (IsInitialized)
                return await _instance.QueryManager.Query<TQuery, TResult>(query,
                    LinkedToken(CancellationToken, ct ?? CancellationToken.None));
            EnsureInitializedOrWarn(nameof(Query));
            return default;
        }

        /// <summary>
        /// 订阅查询，以便在执行特定类型的查询时能够处理该查询并返回结果。
        /// </summary>
        /// <typeparam name="TQuery">要订阅的查询类型，必须实现 IQueryData 接口。</typeparam>
        /// <typeparam name="TResult">查询处理函数返回的结果类型。</typeparam>
        /// <param name="subscribe">一个委托，用于定义如何处理 TQuery 类型的查询并返回 TResult 类型的结果。</param>
        public static void SubscribeQuery<TQuery, TResult>(
            Func<TQuery, CancellationToken, UniTask<TResult>> subscribe)
            where TQuery : IQueryData
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(SubscribeQuery));
                return;
            }

            _instance.QueryManager.Subscribe(subscribe);
        }

        /// <summary>
        /// 取消订阅指定类型的查询。
        /// </summary>
        /// <typeparam name="TQuery">要取消订阅的查询类型，必须实现 IQueryData 接口。</typeparam>
        /// <typeparam name="TResult">查询结果的类型。</typeparam>
        public static void UnsubscribeQuery<TQuery, TResult>() where TQuery : IQueryData
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(UnsubscribeQuery));
                return;
            }

            _instance.QueryManager.Unsubscribe<TQuery, TResult>();
        }
        
        /// <summary>
        /// 清除查询缓存中的所有缓存数据。
        /// </summary>
        public static void ClearQueryCache()
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(ClearQueryCache));
                return;
            }

            _instance.QueryManager.ClearQueryCache();
        }

        /// <summary>
        /// 使指定查询的缓存失效，下次查询时会重新执行并缓存新结果。
        /// </summary>
        /// <typeparam name="TQuery">查询类型，必须实现 IQueryData 接口。</typeparam>
        /// <typeparam name="TResult">查询结果的类型。</typeparam>
        /// <param name="query">要使缓存失效的查询实例。</param>
        public static void InvalidateCache<TQuery, TResult>(TQuery query) where TQuery : IQueryData
        {
            if (!IsInitialized)
            {
                EnsureInitializedOrWarn(nameof(InvalidateCache));
                return;
            }

            _instance.QueryManager.InvalidateCache<TQuery, TResult>(query);
        }

        #endregion

        #region 事件注册/取消注册

        /// <summary>
        /// 注册一个处理程序到广播、命令和查询管理器中。
        /// </summary>
        /// <param name="handler">要注册的处理程序对象。</param>
        public static void RegisterHandler(object handler)
        {
            if (TryRegisterHandler(handler)) return;
            EnsureInitializedOrWarn(nameof(RegisterHandler));
        }

        /// <summary>
        /// 尝试注册一个处理程序到广播、命令和查询管理器中，不发出警告。
        /// </summary>
        /// <param name="handler">要注册的处理程序对象。</param>
        /// <returns>如果成功注册则返回 true，否则返回 false。</returns>
        public static bool TryRegisterHandler(object handler)
        {
            if (!IsInitialized)
            {
                return false;
            }

            _instance.BroadcastManager.Register(handler);
            _instance.CommandManager.Register(handler);
            _instance.QueryManager.Register(handler);
            return true;
        }

        /// <summary>
        /// 从广播、命令和查询管理器中注销指定的处理程序。
        /// </summary>
        /// <param name="handler">要注销的处理程序对象。</param>
        public static void UnregisterHandler(object handler)
        {
            if (TryUnregisterHandler(handler)) return;
            EnsureInitializedOrWarn(nameof(UnregisterHandler));
        }

        /// <summary>
        /// 尝试从广播、命令和查询管理器中注销指定的处理程序，不发出警告。
        /// </summary>
        /// <param name="handler">要注销的处理程序对象。</param>
        /// <returns>如果成功注销则返回 true，否则返回 false。</returns>
        public static bool TryUnregisterHandler(object handler)
        {
            if (!IsInitialized)
            {
                return false;
            }

            _instance.BroadcastManager.Unregister(handler);
            _instance.CommandManager.Unregister(handler);
            _instance.QueryManager.Unregister(handler);
            return true;
        }

        #endregion

        /// <summary>
        /// 获取当前 CFramework 的实例。
        /// </summary>
        /// <returns>返回 CFramework 的实例。</returns>
        internal static CFramework CFramework()
        {
            return _instance;
        }

        /// <summary>
        /// 创建一个链接的取消令牌，将多个取消令牌组合在一起，任意一个令牌被取消时，链接令牌也会被取消。
        /// CTS 会自动跟踪并在所有关联的 Token 被取消或框架销毁时释放，防止内存泄漏。
        /// </summary>
        /// <param name="ct">要链接的取消令牌数组。</param>
        /// <returns>链接后的取消令牌。</returns>
        private static CancellationToken LinkedToken(params CancellationToken[] ct)
        {
            // 过滤掉None token，避免创建不必要的Source
            var validTokens = ct.Where(t => t != CancellationToken.None).ToArray();
            if (validTokens.Length == 0) return CancellationToken.None;
            if (validTokens.Length == 1) return validTokens[0];

            var cts = CancellationTokenSource.CreateLinkedTokenSource(validTokens);

            // 自动跟踪 CTS，防止泄漏
            // 使用 ConcurrentDictionary 确保 CTS 和 registration 一一对应
            var trackingKey = new object();
            _trackedCts.TryAdd(trackingKey, cts);

            // 当链接令牌被取消时，自动释放 CTS
            CancellationToken linkedToken  = cts.Token;
            linkedToken .Register(() =>
            {
                cts.Dispose();
                if (_trackedCts.TryRemove(trackingKey, out var removed))
                {
                    DisposeTrackedCts(removed);
                }
            });

            return cts.Token;
        }

        /// <summary>
        /// 安全地释放被跟踪的 CTS
        /// </summary>
        private static void DisposeTrackedCts(CancellationTokenSource cts)
        {
            try
            {
                cts.Dispose();
            }
            catch
            {
                // 忽略释放异常
            }
        }

        /// <summary>
        /// 清理所有被跟踪的 CTS（通常在框架销毁时调用）
        /// </summary>
        internal static void ClearTrackedCts()
        {
            foreach (var kvp in _trackedCts)
            {
                DisposeTrackedCts(kvp.Value);
            }
            _trackedCts.Clear();
        }

        /// <summary>
        /// 创建一个可释放的链接令牌对，用于需要手动管理生命周期的场景。
        /// </summary>
        /// <param name="ct">要链接的取消令牌数组。</param>
        /// <returns>包含Token和CancellationTokenSource的元组，使用后应调用Dispose。</returns>
        internal static (CancellationToken Token, IDisposable Disposer) CreateDisposableLinkedToken(params CancellationToken[] ct)
        {
            var validTokens = ct.Where(t => t != CancellationToken.None).ToArray();
            if (validTokens.Length == 0) return (CancellationToken.None, null);
            if (validTokens.Length == 1) return (validTokens[0], null);
            
            var cts = CancellationTokenSource.CreateLinkedTokenSource(validTokens);
            return (cts.Token, cts);
        }
    }
}