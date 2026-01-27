using System.Threading;
using Cysharp.Threading.Tasks;

namespace CFramework.Core.Interfaces.LifeScope
{
    public interface IUnRegisterAsync
    {
        UniTask UnRegisterAsync(CancellationToken cancellationToken);
    }
}