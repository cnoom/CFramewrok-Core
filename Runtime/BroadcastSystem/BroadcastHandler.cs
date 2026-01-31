using System;
using System.Reflection;
using System.Threading;
using CFramework.Core.Interfaces;
using CFramework.Core.Log;
using Cysharp.Threading.Tasks;

namespace CFramework.Core.BroadcastSystem
{
    public class BroadcastHandler : IPoolData, IComparable<BroadcastHandler>
    {
        public int Priority { get; private set; }
        public Delegate Handler { get; private set; }
        public bool NeedsMainThread { get; private set; } = true;

        // 记录原始注册来源（用于 Attribute 注册的精确反注册）
        public object SourceTarget { get; internal set; }
        public MethodInfo SourceMethod { get; internal set; }

        public int CompareTo(BroadcastHandler other)
        {
            return Priority.CompareTo(other.Priority);
        }

        public void Dispose()
        {
            Clear();
        }

        public void OnReturn()
        {
            Clear();
        }

        public async UniTask Invoke(IBroadcastData broadcastData, CFLogger logger, CancellationToken ct)
        {
            await AsyncInvoke(broadcastData, logger, ct);
        }

        public void Set(int priority, Delegate handler, bool needsMainThread)
        {
            Priority = priority;
            Handler = handler;
            NeedsMainThread = needsMainThread;
        }

        private async UniTask AsyncInvoke(IBroadcastData broadcastData, CFLogger logger, CancellationToken ct)
        {
            try
            {
                switch(Handler)
                {
                    case Func<IBroadcastData, CancellationToken, UniTask> funcTaskWithCt:
                        await funcTaskWithCt.Invoke(broadcastData, ct);
                        break;
                    case Func<IBroadcastData, UniTask> funcTask:
                        await funcTask.Invoke(broadcastData);
                        break;
                    default:
                        logger.LogWarning($"未知的异步方法执行! 执行者：{Handler?.Method.Name} 数据：{broadcastData.GetType().Name}");
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // 协作式取消，按需静默或debug日志
            }
            catch (Exception e)
            {
                logger.LogError($"异步广播监听失败: {e.Message}\nStackTrace: {e.StackTrace}");
            }
        }

        private void Clear()
        {
            Priority = 0;
            Handler = null;
            SourceTarget = null;
            SourceMethod = null;
        }
    }
}