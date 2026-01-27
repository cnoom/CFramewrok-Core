using System.Threading;

namespace CFramework.Core.Interfaces
{
    /// <summary>
    /// 取消令牌持有者
    /// </summary>
    public interface ICancellationHolder
    {
        CancellationTokenSource CancellationTokenSource { get; set; }
    }
}