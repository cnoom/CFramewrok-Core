using System;
using System.Threading;

namespace CFramework.Core.Execution
{
    public sealed class CFExecutionContext : IDisposable
    {
        public readonly CFExecutionOptions Options;
        public readonly CancellationToken CancellationToken;

        private readonly CancellationTokenSource _linkedCts;

        public CFExecutionContext(CFExecutionOptions options, CancellationToken externalToken, TimeSpan overallTimeout)
        {
            Options = options ?? new CFExecutionOptions();

            // 组装整体超时 + 外部 ct
            if (overallTimeout > TimeSpan.Zero)
            {
                var timeoutCts = new CancellationTokenSource(overallTimeout);
                _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken, timeoutCts.Token);
            }
            else
            {
                _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            }

            CancellationToken = _linkedCts.Token;
        }

        public void Cancel() => _linkedCts.Cancel();

        public void Dispose()
        {
            _linkedCts.Dispose();
        }
    }
}