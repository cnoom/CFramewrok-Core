using CFramework.Core.Editor.Base;
using UnityEditor;
using UnityEngine;

namespace CFramework.Core.Editor.Utilities
{
    /// <summary>
    ///     编辑器专用日志输出器，使用与运行时相同的日志格式
    /// </summary>
    public static class EditorLogUtility
    {
        private const string DefaultTag = "CFramework";

        // 日志级别名称
        private const string DebugLevelName = "Debug";
        private const string InfoLevelName = "Info";
        private const string WarningLevelName = "Warning";
        private const string ErrorLevelName = "Error";

        // 日志级别颜色定义
        private static readonly Color DebugColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color InfoColor = new Color(0.3f, 0.6f, 1f);
        private static readonly Color WarningColor = new Color(1f, 0.8f, 0.3f);
        private static readonly Color ErrorColor = new Color(1f, 0.3f, 0.3f);

        /// <summary>
        ///     输出Info级别日志
        /// </summary>
        public static void LogInfo(string message, string tag = null)
        {
            if(!ShouldLog(LogLevel.Info))
                return;
            string effectiveTag = string.IsNullOrEmpty(tag) ? DefaultTag : tag;
            string formatted = FormatMessage(effectiveTag, message, InfoLevelName, InfoColor);
            Debug.Log(formatted);
        }

        /// <summary>
        ///     输出Debug级别日志
        /// </summary>
        public static void LogDebug(string message, string tag = null)
        {
            if(!ShouldLog(LogLevel.Debug))
                return;
            string effectiveTag = string.IsNullOrEmpty(tag) ? DefaultTag : tag;
            string formatted = FormatMessage(effectiveTag, message, DebugLevelName, DebugColor);
            Debug.Log(formatted);
        }

        /// <summary>
        ///     输出Warning级别日志
        /// </summary>
        public static void LogWarning(string message, string tag = null)
        {
            if(!ShouldLog(LogLevel.Warning))
                return;
            string effectiveTag = string.IsNullOrEmpty(tag) ? DefaultTag : tag;
            string formatted = FormatMessage(effectiveTag, message, WarningLevelName, WarningColor);
            Debug.LogWarning(formatted);
        }

        /// <summary>
        ///     输出Error级别日志
        /// </summary>
        public static void LogError(string message, string tag = null)
        {
            if(!ShouldLog(LogLevel.Error))
                return;
            string effectiveTag = string.IsNullOrEmpty(tag) ? DefaultTag : tag;
            string formatted = FormatMessage(effectiveTag, message, ErrorLevelName, ErrorColor);
            Debug.LogError(formatted);
        }

        /// <summary>
        ///     判断是否应该输出日志
        /// </summary>
        private static bool ShouldLog(LogLevel level)
        {
            CFrameworkEditorConfig config = ConfigUtility.GetEditorConfig<CFrameworkEditorConfig>();
            if(config == null)
                return true;
            if(!config.runtimeLog && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return false;
            }
            // 检查日志等级
            return level >= config.minLogLevel;
        }

        /// <summary>
        ///     格式化日志消息（与运行时保持一致的格式）
        /// </summary>
        private static string FormatMessage(string tag, string message, string level, Color levelColor)
        {
            Color tagColor = new Color(0.6f, 0.8f, 1f);

            // 编辑器环境下使用富文本颜色标签
            return $"<color=#{ColorUtility.ToHtmlStringRGBA(tagColor)}>[{tag}]</color>[<color=#{ColorUtility.ToHtmlStringRGBA(levelColor)}>{level}</color>]: {message}";
        }
    }
}