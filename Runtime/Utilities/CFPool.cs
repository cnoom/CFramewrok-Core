using System;
using System.Collections.Generic;
using CFramework.Core.Interfaces;
using UnityEngine;

namespace CFramework.Core.Utilities
{
    /// <summary>
    ///     线程安全对象池：
    ///     - 支持容量上限（maxCapacity）
    ///     - 支持预热（Prewarm/构造预热）
    ///     - 支持 onGet/onRelease 回调（在锁外执行）
    ///     - 自动调用 IPoolData.OnReturn()；超容量时若实现 IDisposable 将自动释放
    ///     - 提供 Count/InUseCount/TryGet
    ///     兼容原有 API：保留 (factory, onGet) 构造函数。
    /// </summary>
    public class CFPool<TData> : IDisposable where TData : IPoolData
    {
        private readonly Func<TData> _factory;
        private readonly object _lock = new object();
        private readonly int _maxCapacity;
        private readonly Action<TData> _onGet; // Get 后回调（锁外）
        private readonly Action<TData> _onRelease; // Return 时额外重置（锁外）
        private readonly Stack<TData> _pool;

        private bool _disposed;
        // 近似在用计数（Get++/Return--）
        private int _inUseCount;

        /// <summary>
        ///     兼容旧构造：仅指定工厂与 onGet 回调，容量无限制。
        /// </summary>
        public CFPool(Func<TData> generate, Action<TData> get = null)
            : this(generate, get, null)
        {
        }

        /// <summary>
        ///     完整构造：支持 onGet/onRelease、容量上限、预热。
        /// </summary>
        public CFPool(
            Func<TData> factory,
            Action<TData> onGet = null,
            Action<TData> onRelease = null,
            int maxCapacity = int.MaxValue,
            int prewarm = 0)
        {
            if(factory == null) throw new ArgumentNullException(nameof(factory));
            if(maxCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(maxCapacity), "maxCapacity 必须 > 0");
            if(prewarm < 0) throw new ArgumentOutOfRangeException(nameof(prewarm), "prewarm 不能为负数");

            _factory = factory;
            _onGet = onGet;
            _onRelease = onRelease;
            _maxCapacity = maxCapacity;

            int initialCapacity = Math.Min(maxCapacity, Math.Max(prewarm, 4));
            _pool = new Stack<TData>(initialCapacity);

            if(prewarm > 0)
            {
                int count = Math.Min(prewarm, _maxCapacity);
                for(var i = 0; i < count; i++)
                {
                    _pool.Push(_factory());
                }
            }
        }

        /// <summary>
        ///     池内可用数量（不含在用）。
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _pool.Count;
                }
            }
        }

        /// <summary>
        ///     近似在用数量（用于监控）。
        /// </summary>
        public int InUseCount
        {
            get
            {
                lock (_lock)
                {
                    return _inUseCount;
                }
            }
        }

        public void Dispose()
        {
            if(_disposed) return;

            List<TData> toDispose = null;
            lock (_lock)
            {
                if(_disposed) return;
                _disposed = true;

                if(_pool.Count > 0)
                {
                    toDispose = new List<TData>(_pool.Count);
                    while (_pool.Count > 0) toDispose.Add(_pool.Pop());
                }
            }

            if(toDispose != null)
            {
                foreach (TData item in toDispose)
                {
                    if(item is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch
                        {
                            /* ignore */
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     获取对象；若池为空则创建。
        /// </summary>
        public TData Get()
        {
            ThrowIfDisposed();

            TData data;
            lock (_lock)
            {
                if(_pool.Count > 0)
                {
                    data = _pool.Pop();
                }
                else
                {
                    data = _factory();
                }
                _inUseCount++;
            }

            _onGet?.Invoke(data); // 锁外执行
            return data;
        }

        /// <summary>
        ///     尝试仅从池中获取（不创建新对象）。成功返回 true。
        /// </summary>
        public bool TryGet(out TData data)
        {
            ThrowIfDisposed();

            lock (_lock)
            {
                if(_pool.Count > 0)
                {
                    data = _pool.Pop();
                    _inUseCount++;
                    return true;
                }
            }
            data = default;
            return false;
        }

        /// <summary>
        ///     归还对象：调用自定义 onRelease（可选）与 IPoolData.OnReturn()，再按容量放回池；
        ///     超容量且实现 IDisposable 的对象会被释放。
        /// </summary>
        public void Return(TData data)
        {
            if(data == null) return;
            ThrowIfDisposed();

            _onRelease?.Invoke(data); // 锁外执行额外重置
            data.OnReturn(); // 统一重置

            var pushed = false;
            lock (_lock)
            {
                if(_pool.Count < _maxCapacity)
                {
                    _pool.Push(data);
                    pushed = true;
                }
                _inUseCount = Math.Max(0, _inUseCount - 1);
            }

            if(!pushed && data is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    // 记录异常但不要中断流程，因为对象可能在已部分失效的状态下
                    Debug.LogWarning($"[CFPool] 释放对象时发生异常: {ex.Message}\n堆栈: {ex.StackTrace}");
                }
            }
        }

        /// <summary>
        ///     附加预热指定数量（遵循容量上限）。
        /// </summary>
        public void Prewarm(int count)
        {
            ThrowIfDisposed();
            if(count <= 0) return;

            int toCreate;
            lock (_lock)
            {
                int canAdd = _maxCapacity - _pool.Count;
                if(canAdd <= 0) return;
                toCreate = Math.Min(count, canAdd);
            }

            List<TData> temp = new List<TData>(toCreate);
            for(var i = 0; i < toCreate; i++) temp.Add(_factory());

            lock (_lock)
            {
                foreach (TData item in temp) _pool.Push(item);
            }
        }

        private void ThrowIfDisposed()
        {
            if(_disposed) throw new ObjectDisposedException(nameof(CFPool<TData>));
        }
    }
}