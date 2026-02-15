using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using CFramework.Core;
using System.Threading;
using System.Linq.Expressions;
using CFramework.Core.Attributes;
using CFramework.Core.Execution;
using CFramework.Core.Log;
using CFramework.Core.Utilities;

namespace CFramework.Core.QuerySystem
{
    public class QueryManager
    {
        private readonly CFLogger _logger;
        private readonly CFExecutionOptions _options;

        // 并发安全的处理器表
        private readonly ConcurrentDictionary<(Type, Type), QueryHandlerAsync> _dictionary = new();

        // 去重与缓存（并发安全）
        private readonly ConcurrentDictionary<(Type queryType, Type resultType, int hash), object> _pending = new();

        // 使用LRU缓存替代简单的Dictionary
        private readonly ConcurrentLRUCache<(Type queryType, Type resultType, int hash), (object value, DateTime time)>
            _cache;

        private const int DefaultCacheCapacity = 1024; // LRU缓存容量上限

        private readonly CFPool<QueryHandlerAsync> _pool = new(() => new QueryHandlerAsync(), maxCapacity: 128,
            prewarm: 8);

        private readonly object _poolLock = new();

        private static readonly MethodInfo GenericQueryMethodDefinition = typeof(QueryManager)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .First(m => m.Name == nameof(Query) && m.IsGenericMethodDefinition
                && m.GetGenericArguments().Length == 2 && m.GetParameters().Length == 2);

        private static readonly ConcurrentDictionary<(Type queryType, Type resultType), MethodInfo> QueryMethodCache = new();

        // 仅在真正调用处理器前切回主线程
        public bool EnsureMainThread { get; set; } = true;

        // 主线程 ID 与一次性处置标记
        private readonly int _mainThreadId;
        private int _disposedFlag = 0; // 0=未处置, 1=已处置

        public QueryManager(CFLogger logger, CFExecutionOptions options = null)
        {
            _logger = logger;
            _options = options ?? new CFExecutionOptions();
            _mainThreadId = Thread.CurrentThread.ManagedThreadId; // 假定在主线程构造
            _cache = new ConcurrentLRUCache<(Type, Type, int), (object, DateTime)>(DefaultCacheCapacity);
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposedFlag) != 0)
                throw new ObjectDisposedException(nameof(QueryManager));
        }

        private void EnsureOnMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                throw new InvalidOperationException("该操作必须在主线程执行。");
        }

        #region 订阅

        public void Subscribe<TQuery, TResult>(Func<TQuery, CancellationToken, UniTask<TResult>> query)
            where TQuery : IQueryData<TResult>
        {
            Type queryType = typeof(TQuery);
            Type resultType = typeof(TResult);

            if (TryGetQueryResultType(queryType, out var expectedResultType) && expectedResultType != resultType)
            {
                _logger.LogError(
                    $"查询类型 {queryType.Name} 声明的结果类型为 {expectedResultType.Name}，但订阅结果类型为 {resultType.Name}。请检查 IQueryData<TResult> 定义与处理器返回值。");
                return;
            }

            Subscribe(queryType, resultType, query);
        }

        private void Subscribe(Type queryType, Type resultType, Delegate query)
        {
            ThrowIfDisposed();
            EnsureOnMainThread();

            QueryHandlerAsync handler;
            lock (_poolLock)
                handler = _pool.Get();
            handler.Set(query);

            if (!_dictionary.TryAdd((queryType, resultType), handler))
            {
                _logger.LogWarning($"异步查询处理器已存在!<{queryType.Name},{resultType.Name}>\n" +
                                   $"oldHandler={handler.Handler.Target.GetType().Name}\n" +
                                   $"newHandler={query.Target.GetType().Name}");
                lock (_poolLock)
                    _pool.Return(handler);
            }
        }

        #endregion

        #region 取消订阅

        public void Unsubscribe<TQuery, TResult>()
            where TQuery : IQueryData<TResult>
        {
            Type queryType = typeof(TQuery);
            Type resultType = typeof(TResult);
            Unsubscribe(queryType, resultType);
        }

        private void Unsubscribe(Type queryType, Type resultType)
        {
            ThrowIfDisposed();
            EnsureOnMainThread();

            if (!_dictionary.TryRemove((queryType, resultType), out var queryHandler)) return;
            lock (_poolLock)
                _pool.Return(queryHandler);
        }

        #endregion

        #region 查询

        public async UniTask<TResult> Query<TQuery, TResult>(TQuery query, CancellationToken ct)
            where TQuery : IQueryData<TResult>
        {
            ThrowIfDisposed();

            var queryType = (query != null && (typeof(TQuery).IsInterface || typeof(TQuery).IsAbstract))
                ? query.GetType()
                : typeof(TQuery);
            var resultType = typeof(TResult);

            using var context = new CFExecutionContext(_options, ct, _options.OverallTimeout);
            var startTs = DateTime.UtcNow;
            _logger.LogDebug($"[Query-Start] req={queryType.Name} res={resultType.Name}");

            // 缓存
            if (_options?.QueryCacheEnabled == true)
            {
                if (TryGetCache(queryType, resultType, query, out TResult cached))
                {
                    var durCache = (DateTime.UtcNow - startTs).TotalMilliseconds;
                    _logger.LogDebug(
                        $"[Query-End] req={queryType.Name} res={resultType.Name} durationMs={durCache:F1} cache=hit");
                    return cached;
                }
            }

            // 去重：原子合流，创建者负责移除
            if (_options?.QueryDeduplicateEnabled == true)
            {
                var key = MakeDedupKey(queryType, resultType, query);
                var created = false;

                var obj = _pending.GetOrAdd(key, _ =>
                {
                    created = true;
                    return InternalQueryAsync<TQuery, TResult>(query, context.CancellationToken);
                });

                var t = (UniTask<TResult>)obj;
                try
                {
                    var res = await t;
                    TrySetCache(queryType, resultType, query, res);
                    var dur = (DateTime.UtcNow - startTs).TotalMilliseconds;
                    _logger.LogDebug(
                        $"[Query-End] req={queryType.Name} res={resultType.Name} durationMs={dur:F1} dedup={(created ? "leader" : "joined")}");
                    return res;
                }
                finally
                {
                    if (created)
                        _pending.TryRemove(key, out _);
                }
            }
            else
            {
                var res = await InternalQueryAsync<TQuery, TResult>(query, context.CancellationToken);
                TrySetCache(queryType, resultType, query, res);
                var dur = (DateTime.UtcNow - startTs).TotalMilliseconds;
                _logger.LogDebug(
                    $"[Query-End] req={queryType.Name} res={resultType.Name} durationMs={dur:F1}");
                return res;
            }
        }

        public UniTask<TResult> Query<TResult>(IQueryData<TResult> query, CancellationToken ct)
        {
            ThrowIfDisposed();

            if (query == null)
            {
                _logger.LogWarning("查询参数为 null，已忽略。请传入实现 IQueryData<TResult> 的查询对象。");
                return UniTask.FromResult(default(TResult));
            }

            var queryType = query.GetType();
            var resultType = typeof(TResult);
            var method = GetCachedQueryMethod(queryType, resultType);
            var result = method.Invoke(this, new object[] { query, ct });
            if (result is UniTask<TResult> task) return task;
            return UniTask.FromResult(default(TResult));
        }

        // 缓存实现
        private bool TryGetCache<TResult>(Type queryType, Type resultType, object query, out TResult value)
        {
            value = default;
            if (_options?.QueryCacheEnabled != true) return false;

            var key = MakeDedupKey(queryType, resultType, query);
            if (_cache.TryGet(key, out var entry))
            {
                // TTL=0 视为禁用缓存，避免永久缓存导致增长
                if (_options.QueryCacheTtl > TimeSpan.Zero && (DateTime.UtcNow - entry.time) <= _options.QueryCacheTtl)
                {
                    if (entry.value is TResult casted)
                    {
                        value = casted;
                        return true;
                    }
                }
                else
                {
                    _cache.TryRemove(key, out _); // 并发安全过期剔除
                }
            }

            return false;
        }

        private void TrySetCache<TResult>(Type queryType, Type resultType, object query, TResult value)
        {
            if (_options?.QueryCacheEnabled != true) return;
            if (_options.QueryCacheTtl <= TimeSpan.Zero) return; // TTL=0 禁用缓存
            var key = MakeDedupKey(queryType, resultType, query);
            // LRU缓存自动处理容量控制
            _cache.TryAdd(key, (value, DateTime.UtcNow));
        }

        private async UniTask<TResult> InternalQueryAsync<TQuery, TResult>(TQuery query, CancellationToken ct)
            where TQuery : IQueryData<TResult>
        {
            var queryType = (query != null && (typeof(TQuery).IsInterface || typeof(TQuery).IsAbstract))
                ? query.GetType()
                : typeof(TQuery);
            var resultType = typeof(TResult);
            if (_dictionary.TryGetValue((queryType, resultType), out var d))
            {
                if (ct.IsCancellationRequested) return default;
                if (EnsureMainThread)
                    await UniTask.SwitchToMainThread();
                try
                {
                    return await d.InvokeAsync<TQuery, TResult>(query, _logger, ct);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug($"[Query-Canceled] req={queryType.Name} res={resultType.Name}");
                    return default;
                }
            }

            _logger.LogWarning($"未注册异步查询处理器!<{queryType.Name},{resultType.Name}>\n{query.ToString()}");
            return default;
        }

        #endregion

        // 新增：不抛异常的查询
        public async UniTask<(bool ok, TResult result)> TryQueryAsync<TResult>(IQueryData<TResult> query,
            CancellationToken ct = default)
        {
            ThrowIfDisposed();

            if (query == null)
            {
                _logger.LogWarning("查询参数为 null，已忽略。请传入实现 IQueryData<TResult> 的查询对象。");
                return (false, default);
            }

            var queryType = query.GetType();
            var resultType = typeof(TResult);

            bool has = _dictionary.ContainsKey((queryType, resultType));

            if (!has)
            {
                _logger.LogWarning($"未注册该查询处理器!<{queryType.Name},{resultType.Name}>");
                return (false, default);
            }

            try
            {
                var res = await Query(query, ct);
                return (true, res); // default 也可能是合法值
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug($"[Query-Canceled] req={queryType.Name} res={resultType.Name}");
                return (false, default);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[Query-Error] req={queryType.Name} res={resultType.Name} error={ex.Message}");
                return (false, default);
            }
        }

        public async UniTask<(bool ok, TResult result)> TryQueryAsync<TQuery, TResult>(TQuery query,
            CancellationToken ct = default) where TQuery : IQueryData<TResult>
        {
            ThrowIfDisposed();

            var queryType = (query != null && (typeof(TQuery).IsInterface || typeof(TQuery).IsAbstract))
                ? query.GetType()
                : typeof(TQuery);
            var resultType = typeof(TResult);

            bool has = _dictionary.ContainsKey((queryType, resultType));

            if (!has)
            {
                _logger.LogWarning($"未注册该查询处理器!<{queryType.Name},{resultType.Name}>");
                return (false, default);
            }

            try
            {
                var res = await Query<TQuery, TResult>(query, ct);
                return (true, res); // default 也可能是合法值
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug($"[Query-Canceled] req={queryType.Name} res={resultType.Name}");
                return (false, default);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[Query-Error] req={queryType.Name} res={resultType.Name} error={ex.Message}");
                return (false, default);
            }
        }

        // 内部：去重/缓存工具
        private (Type, Type, int) MakeDedupKey(Type queryType, Type resultType, object query)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (queryType?.GetHashCode() ?? 0);
                h = h * 31 + (resultType?.GetHashCode() ?? 0);
                h = h * 31 + (query?.GetHashCode() ?? 0);
                return (queryType, resultType, h);
            }
        }

        private static MethodInfo GetCachedQueryMethod(Type queryType, Type resultType)
        {
            return QueryMethodCache.GetOrAdd((queryType, resultType), key =>
                GenericQueryMethodDefinition.MakeGenericMethod(key.queryType, key.resultType));
        }

        private static bool TryGetQueryResultType(Type queryType, out Type resultType)
        {
            resultType = null;
            if (queryType == null) return false;

            var iface = queryType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryData<>));

            if (iface == null) return false;
            resultType = iface.GetGenericArguments()[0];
            return true;
        }

        // 使用表达式树构建强类型 Func 委托，避免运行时反射绑定开销
        private static Delegate BuildFuncDelegate(object target, MethodInfo method)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (method == null) throw new ArgumentNullException(nameof(method));

            var delegateType = ReflectUtil.GetFuncType(method);
            var parameters = method.GetParameters();

            // 参数表达式
            var paramExprs = new ParameterExpression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                paramExprs[i] = Expression.Parameter(parameters[i].ParameterType, parameters[i].Name ?? $"arg{i}");
            }

            // 实例表达式（确保转换为声明类型，以兼容继承场景）
            var instanceConst = Expression.Constant(target);
            Expression instanceExpr = method.IsStatic
                ? null
                : Expression.Convert(instanceConst, method.DeclaringType);

            // 调用表达式
            var callExpr = method.IsStatic
                ? Expression.Call(method, paramExprs)
                : Expression.Call(instanceExpr, method, paramExprs);

            // 构建并编译 Lambda
            var lambda = Expression.Lambda(delegateType, callExpr, paramExprs);
            return lambda.Compile();
        }

        public void Register<T>(T queryObject) where T : class
        {
            ThrowIfDisposed();
            EnsureOnMainThread();

            if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));

            var type = queryObject.GetType();
            MethodInfo[] methods = ReflectUtil.GetAllInstanceMethods(type);

            foreach (MethodInfo method in methods)
            {
                var attr = method.GetCustomAttribute<QueryHandlerAttribute>(inherit: true);
                if (attr == null) continue;

                var parameters = method.GetParameters();
                var requestType = parameters.Length > 0 ? parameters[0].ParameterType : null;
                var resultType = method.ReturnType;

                // 允许 (IQueryData) 或 (IQueryData, CancellationToken)
                if (parameters.Length != 2 ||
                    !typeof(IQueryData).IsAssignableFrom(requestType) ||
                    parameters[1].ParameterType != typeof(CancellationToken))
                {
                    _logger.LogError(
                        $"类型 {type.Name} 方法 {method.Name} 签名无效，必须接收 (IQueryData, CancellationToken) 参数。");
                    continue;
                }


                if (resultType == typeof(void))
                {
                    _logger.LogError(
                        $"类型 {type.Name}  方法 {method.DeclaringType?.FullName}.{method.Name} 必须有返回值（查询的结果）。");
                    continue;
                }

                var resultTypeArg = resultType.GetGenericArguments()[0];

                if (TryGetQueryResultType(requestType, out var expectedResultType) &&
                    expectedResultType != resultTypeArg)
                {
                    _logger.LogError(
                        $"类型 {type.Name} 方法 {method.Name} 返回值 {resultTypeArg.Name} 与查询类型 {requestType.Name} 声明的结果类型 {expectedResultType.Name} 不一致。");
                    continue;
                }

                Delegate asyncHandler = BuildFuncDelegate(queryObject, method);
                Subscribe(requestType, resultTypeArg, asyncHandler);
            }
        }

        public void Unregister<T>(T queryObject) where T : class
        {
            ThrowIfDisposed();
            EnsureOnMainThread();

            if (queryObject == null) throw new ArgumentNullException(nameof(queryObject));

            var type = queryObject.GetType();
            MethodInfo[] methods = ReflectUtil.GetAllInstanceMethods(type);

            foreach (MethodInfo method in methods)
            {
                var attr = method.GetCustomAttribute<QueryHandlerAttribute>(inherit: true);
                if (attr == null) continue;

                var parameters = method.GetParameters();
                var resultType = method.ReturnType;

                // 允许 (IQueryData) 或 (IQueryData, CancellationToken)
                if (parameters.Length != 2 || parameters[1].ParameterType != typeof(CancellationToken))
                {
                    _logger.LogError(
                        $"类型 {type.Name} 方法 {method.Name} 签名无效，必须接收 (IQueryData, CancellationToken) 参数。");
                    continue;
                }

                var requestType = parameters[0].ParameterType;
                if (!typeof(IQueryData).IsAssignableFrom(requestType))
                {
                    _logger.LogError(
                        $"类型 {type.Name} 方法 {method.Name} 签名无效，必须接收 (IQueryData, CancellationToken) 参数。");
                    continue;
                }

                if (resultType == typeof(void))
                {
                    _logger.LogError(
                        $"类型 {type.Name} 方法 {method.DeclaringType?.FullName}.{method.Name} 必须有返回值（查询的结果）。");
                    continue;
                }

                var resultTypeArg = resultType.GetGenericArguments()[0];
                Unsubscribe(requestType, resultTypeArg);
            }
        }

        // 缓存API（管理操作，强制主线程）
        public void ClearQueryCache()
        {
            ThrowIfDisposed();
            EnsureOnMainThread();
            _cache.Clear();
        }

        public void InvalidateCache<TResult>(IQueryData<TResult> query)
        {
            ThrowIfDisposed();
            EnsureOnMainThread();

            if (query == null)
            {
                _logger.LogWarning("查询参数为 null，已忽略。请传入实现 IQueryData<TResult> 的查询对象。");
                return;
            }

            var key = MakeDedupKey(query.GetType(), typeof(TResult), query);
            _cache.TryRemove(key, out _);
        }

        public void InvalidateCache<TQuery, TResult>(TQuery query) where TQuery : IQueryData<TResult>
        {
            ThrowIfDisposed();
            EnsureOnMainThread();
            var key = MakeDedupKey(typeof(TQuery), typeof(TResult), query);
            _cache.TryRemove(key, out _);
        }

        public async UniTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposedFlag, 1) == 1)
                return;

            // 清空注册并归还池对象
            foreach (var kv in _dictionary)
            {
                if (_dictionary.TryRemove(kv.Key, out var handler))
                {
                    lock (_poolLock)
                        _pool.Return(handler);
                }
            }

            _pending.Clear();
            _cache.Clear();

            lock (_poolLock)
            {
                _pool.Dispose();
            }

            _logger.LogDebug("查询管理卸载完成!");
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 线程安全的LRU缓存实现
        /// </summary>
        private class ConcurrentLRUCache<TKey, TValue>
        {
            private readonly int _capacity;
            private readonly LinkedList<TKey> _lruList;
            private readonly Dictionary<TKey, LinkedListNode<TKey>> _cache;
            private readonly ConcurrentDictionary<TKey, TValue> _valueCache;
            private readonly ReaderWriterLockSlim _lock = new();

            public int Count
            {
                get
                {
                    _lock.EnterReadLock();
                    try
                    {
                        return _cache.Count;
                    }
                    finally
                    {
                        _lock.ExitReadLock();
                    }
                }
            }

            public ConcurrentLRUCache(int capacity)
            {
                _capacity = capacity > 0 ? capacity : 1024;
                _lruList = new LinkedList<TKey>();
                _cache = new Dictionary<TKey, LinkedListNode<TKey>>();
                _valueCache = new ConcurrentDictionary<TKey, TValue>();
            }

            public bool TryGet(TKey key, out TValue value)
            {
                // 先尝试从并发字典中获取值（无锁）
                if (_valueCache.TryGetValue(key, out value))
                {
                    // 如果值存在，更新 LRU 顺序（写锁）
                    _lock.EnterWriteLock();
                    try
                    {
                        if (_cache.TryGetValue(key, out var node))
                        {
                            // 将访问的节点移到链表末尾（最新）
                            _lruList.Remove(node);
                            _lruList.AddLast(node);
                        }
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }

                    return true;
                }

                return false;
            }

            public bool TryAdd(TKey key, TValue value)
            {
                _lock.EnterWriteLock();
                try
                {
                    // 如果键已存在，更新值并移到末尾（更新 LRU 顺序）
                    if (_cache.TryGetValue(key, out var existingNode))
                    {
                        _valueCache[key] = value;
                        // 移除旧节点，创建新节点并添加到末尾
                        _lruList.Remove(existingNode);
                        var newNode = _lruList.AddLast(key);
                        _cache[key] = newNode;
                        return true;
                    }

                    // 如果达到容量上限，移除最少使用的项
                    if (_cache.Count >= _capacity && _lruList.Count > 0)
                    {
                        var lruKey = _lruList.First.Value;
                        _lruList.RemoveFirst();
                        _cache.Remove(lruKey);
                        _valueCache.TryRemove(lruKey, out _);
                    }

                    // 添加新项
                    var newNode2 = _lruList.AddLast(key);
                    _cache[key] = newNode2;
                    _valueCache[key] = value;
                    return true;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            public bool TryRemove(TKey key, out TValue value)
            {
                _lock.EnterWriteLock();
                try
                {
                    if (_cache.TryGetValue(key, out var node))
                    {
                        _lruList.Remove(node);
                        _cache.Remove(key);
                        _valueCache.TryRemove(key, out value);
                        return true;
                    }

                    value = default;
                    return false;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }

            public void Clear()
            {
                _lock.EnterWriteLock();
                try
                {
                    _lruList.Clear();
                    _cache.Clear();
                    _valueCache.Clear();
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }
    }
}