using System;

namespace CFramework.Core.Log
{
    public interface ICFLogger
    {
        public enum Level
        {
            Debug,
            Info,
            Warning,
            Error,
        }

        void LogInfo(string message);
        void LogDebug(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogException(Exception exception);
    }
}