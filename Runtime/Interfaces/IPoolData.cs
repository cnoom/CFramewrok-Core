using System;

namespace CFramework.Core.Interfaces
{
    public interface IPoolData : IDisposable
    {
        void OnReturn();
    }
}