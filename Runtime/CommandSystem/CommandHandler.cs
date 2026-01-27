using System;
using System.Threading;
using CFramework.Core.Interfaces;
using CFramework.Core.Log;
using Cysharp.Threading.Tasks;

namespace CFramework.Core.CommandSystem
{
    public class CommandHandler : IPoolData, ISet<Delegate>
    {
        public Delegate Handler { get; private set; }

        public async UniTask Invoke(ICommandData commandData, CFLogger logger, CancellationToken ct)
        {
            await AsyncInvoke(commandData, logger, ct);
        }

        private async UniTask AsyncInvoke(ICommandData commandData, CFLogger logger, CancellationToken ct)
        {
            try
            {
                switch (Handler)
                {
                    case Func<ICommandData, CancellationToken, UniTask> funcWithCt:
                        await funcWithCt.Invoke(commandData, ct);
                        break;
                    case Func<ICommandData, UniTask> funcTask:
                        await funcTask.Invoke(commandData);
                        break;
                    default:
                        logger.LogWarning($"未知的异步方法执行! 执行者：{Handler?.Method.Name} 数据：{commandData.GetType().Name}");
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // 忽略或可选日志
            }
            catch (Exception ex)
            {
                logger.LogError($"异步命令执行失败: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
        }

        private void Clear()
        {
            Handler = null;
        }

        public void Dispose() => Clear();

        public void OnReturn() => Clear();

        public void Set(Delegate handler)
        {
            Handler = handler;
        }
    }
}