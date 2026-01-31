using System;
using System.Text;
using UnityEngine;

namespace CFramework.Core.Log
{
    /// <summary>
    ///     日志格式化器，统一管理日志输出格式，支持在运行时和编辑器中使用相同的格式
    /// </summary>
    public static class LoggerFormatter
    {

        // 日志级别名称
        public const string DebugLevelName = "Debug";
        public const string InfoLevelName = "Info";
        public const string WarningLevelName = "Warning";
        public const string ErrorLevelName = "Error";
        // 日志级别颜色定义
        public static readonly Color DebugColor = new Color(0.5f, 0.5f, 0.5f);
        public static readonly Color InfoColor = new Color(0.3f, 0.6f, 1f);
        public static readonly Color WarningColor = new Color(1f, 0.8f, 0.3f);
        public static readonly Color ErrorColor = new Color(1f, 0.3f, 0.3f);

        private static readonly StringBuilder StringBuilderCache = new StringBuilder(512);

        /// <summary>
        ///     获取指定日志级别的颜色
        /// </summary>
        public static Color GetLevelColor(ICFLogger.Level level)
        {
            return level switch
            {
                ICFLogger.Level.Debug => DebugColor,
                ICFLogger.Level.Info => InfoColor,
                ICFLogger.Level.Warning => WarningColor,
                ICFLogger.Level.Error => ErrorColor,
                _ => Color.white
            };
        }

        /// <summary>
        ///     获取指定日志级别的名称
        /// </summary>
        public static string GetLevelName(ICFLogger.Level level)
        {
            return level switch
            {
                ICFLogger.Level.Debug => DebugLevelName,
                ICFLogger.Level.Info => InfoLevelName,
                ICFLogger.Level.Warning => WarningLevelName,
                ICFLogger.Level.Error => ErrorLevelName,
                _ => "Unknown"
            };
        }

        /// <summary>
        ///     格式化日志消息
        /// </summary>
        /// <param name="tag">日志标签</param>
        /// <param name="message">日志消息</param>
        /// <param name="level">日志级别</param>
        /// <param name="tagColor">标签颜色</param>
        /// <returns>格式化后的消息字符串</returns>
        public static string FormatMessage(string tag, string message, ICFLogger.Level level, Color tagColor)
        {
            StringBuilderCache.Clear();

            string levelName = GetLevelName(level);
            Color levelColor = GetLevelColor(level);

            #if UNITY_EDITOR
            StringBuilderCache.Append($"<color=#{ColorUtility.ToHtmlStringRGBA(tagColor)}>[{tag}]</color>");
            StringBuilderCache.Append($"[<color=#{ColorUtility.ToHtmlStringRGBA(levelColor)}>{levelName}</color>]: ");
            #else
            StringBuilderCache.Append($"[{tag}][{levelName}]: ");
            StringBuilderCache.Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ");
            #endif

            StringBuilderCache.Append(message);
            return StringBuilderCache.ToString();
        }

        /// <summary>
        ///     格式化异常信息
        /// </summary>
        /// <param name="tag">日志标签</param>
        /// <param name="exception">异常对象</param>
        /// <returns>格式化后的异常字符串</returns>
        public static string FormatException(string tag, Exception exception)
        {
            if(exception == null) return string.Empty;

            StringBuilderCache.Clear();
            StringBuilderCache.Append($"[{tag}] Exception: {exception.Message}");
            StringBuilderCache.Append($"\nStackTrace: {exception.StackTrace}");

            if(exception.InnerException != null)
            {
                StringBuilderCache.Append($"\nInnerException: {exception.InnerException.Message}");
                StringBuilderCache.Append($"\nInnerStackTrace: {exception.InnerException.StackTrace}");
            }

            return StringBuilderCache.ToString();
        }

        /// <summary>
        ///     格式化完整的日志条目（包含标签、消息、级别）
        /// </summary>
        /// <param name="logEntry">日志条目</param>
        /// <param name="tagColor">标签颜色</param>
        /// <returns>格式化后的完整日志字符串</returns>
        public static string FormatLogEntry(ILogEntry logEntry, Color tagColor)
        {
            if(logEntry == null) return string.Empty;

            if(logEntry.Exception != null)
            {
                return FormatException(logEntry.Tag, logEntry.Exception);
            }

            return FormatMessage(logEntry.Tag, logEntry.Message, logEntry.Level, tagColor);
        }

        /// <summary>
        ///     创建统一的日志条目
        /// </summary>
        public static LogEntry CreateLogEntry(string tag, ICFLogger.Level level, string message, Exception exception = null)
        {
            string formattedMessage;
            if(exception != null)
            {
                formattedMessage = FormatException(tag, exception);
            }
            else
            {
                Color tagColor = LoggerColorManager.GetTagColor(tag);
                formattedMessage = FormatMessage(tag, message, level, tagColor);
            }

            return new LogEntry(tag, level, message, formattedMessage, exception);
        }
    }
}