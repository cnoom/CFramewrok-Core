using System.Threading;
using Cysharp.Threading.Tasks;

namespace CFramework.Core.Interfaces.LifeScope
{
    public interface IRegisterAsync
    {
        UniTask RegisterAsync(CancellationToken cancellationToken);
    }
}