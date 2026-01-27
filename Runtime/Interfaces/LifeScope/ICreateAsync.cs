using System.Threading;
using Cysharp.Threading.Tasks;

namespace CFramework.Core.Interfaces.LifeScope
{
    public interface ICreateAsync
    {
        UniTask CreateAsync(CancellationToken cancellationToken);
    }
}