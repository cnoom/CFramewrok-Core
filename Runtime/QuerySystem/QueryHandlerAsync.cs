using System;
using System.Threading;
using CFramework.Core.Interfaces;
using CFramework.Core.Log;
using Cysharp.Threading.Tasks;

namespace CFramework.Core.QuerySystem
{
    public class QueryHandlerAsync : IPoolData, ISet<Delegate>
    {
        public Delegate Handler { get; private set; }

        public void OnReturn()
        {
            Clear();
        }

        public void Dispose()
        {
            Clear();
        }

        public void Set(Delegate handler)
        {
            Handler = handler;
        }

        public async UniTask<TResult> InvokeAsync<TQuery, TResult>(TQuery query, CFLogger logger, CancellationToken ct)
            where TQuery : IQueryData
        {
            try
            {
                switch(Handler)
                {
                    case Func<TQuery, CancellationToken, UniTask<TResult>> asyncFuncWithCt:
                        return await asyncFuncWithCt.Invoke(query, ct);
                    case Func<TQuery, UniTask<TResult>> asyncFunc:
                        return await asyncFunc.Invoke(query);
                    default:
                        logger.LogError($"查询处理不是支持的 UniTask<TResult> 类型! 方法名:{Handler?.Method.Name}");
                        return default;
                }
            }
            catch (OperationCanceledException)
            {
                return default;
            }
            catch (Exception ex)
            {
                logger.LogError($"异步查询执行失败: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return default;
            }
        }

        private void Clear()
        {
            Handler = null;
        }
    }
}