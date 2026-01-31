using System;
using UnityEngine;

namespace CFramework.Core.Log
{
    public class CFLogger : ICFLogger
    {
        private readonly string _tag;
        private bool _enabled = true;

        internal CFLogger(string tag)
        {
            _tag = tag;
        }
        public ICFLogger.Level Level { get; private set; } = ICFLogger.Level.Debug;

        public void LogDebug(string message)
        {
            if(!CanLog(ICFLogger.Level.Debug)) return;
            Color tagColor = LoggerColorManager.GetTagColor(_tag);
            string formatted = LoggerFormatter.FormatMessage(_tag, message, ICFLogger.Level.Debug, tagColor);
            Debug.Log(formatted);
            OnLog?.Invoke(new LogEntry(_tag, ICFLogger.Level.Debug, message, formatted));
        }

        public void LogInfo(string message)
        {
            if(!CanLog(ICFLogger.Level.Info)) return;
            Color tagColor = LoggerColorManager.GetTagColor(_tag);
            string formatted = LoggerFormatter.FormatMessage(_tag, message, ICFLogger.Level.Info, tagColor);
            Debug.Log(formatted);
            OnLog?.Invoke(new LogEntry(_tag, ICFLogger.Level.Info, message, formatted));
        }

        public void LogWarning(string message)
        {
            if(!CanLog(ICFLogger.Level.Warning)) return;
            Color tagColor = LoggerColorManager.GetTagColor(_tag);
            string formatted = LoggerFormatter.FormatMessage(_tag, message, ICFLogger.Level.Warning, tagColor);
            Debug.LogWarning(formatted);
            OnLog?.Invoke(new LogEntry(_tag, ICFLogger.Level.Warning, message, formatted));
        }

        public void LogError(string message)
        {
            if(!CanLog(ICFLogger.Level.Error)) return;
            Color tagColor = LoggerColorManager.GetTagColor(_tag);
            string formatted = LoggerFormatter.FormatMessage(_tag, message, ICFLogger.Level.Error, tagColor);
            Debug.LogError(formatted);
            OnLog?.Invoke(new LogEntry(_tag, ICFLogger.Level.Error, message, formatted));
        }

        public void LogException(Exception exception)
        {
            if(!_enabled || exception == null) return;
            string exceptionMessage = LoggerFormatter.FormatException(_tag, exception);
            Debug.LogError(exceptionMessage);
            OnLog?.Invoke(new LogEntry(_tag, ICFLogger.Level.Error, exceptionMessage, exceptionMessage, exception));
        }

        public event Action<ILogEntry> OnLog;

        public void SetLevel(ICFLogger.Level level)
        {
            Level = level;
        }

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        public bool IsEnabled()
        {
            return _enabled;
        }

        private bool CanLog(ICFLogger.Level targetLevel)
        {
            if(!_enabled) return false;
            return Level <= targetLevel;
        }
    }

    public interface ILogEntry
    {
        string Tag { get; }
        ICFLogger.Level Level { get; }
        string Message { get; }
        string FormattedMessage { get; }
        Exception Exception { get; }
    }

    public class LogEntry : ILogEntry
    {

        public LogEntry(string tag, ICFLogger.Level level, string message, string formattedMessage, Exception exception = null)
        {
            Tag = tag;
            Level = level;
            Message = message;
            FormattedMessage = formattedMessage;
            Exception = exception;
        }
        public string Tag { get; }
        public ICFLogger.Level Level { get; }
        public string Message { get; }
        public string FormattedMessage { get; }
        public Exception Exception { get; }
    }
}