using CFramework.Core.Editor.Attributes;
using UnityEngine;

namespace CFramework.Core.Editor.Base
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    [EditorConfig("CFrameworkEditorConfig")]
    public class CFrameworkEditorConfig : ScriptableObject
    {
        [Header("初始化"), Tooltip("是否执行过框架初始化")] 
        public bool isInitialized;

        [Header("日志配置"), Tooltip("日志输出最低等级")] 
        public LogLevel minLogLevel = LogLevel.Info;

        [Tooltip("运行时是否输出日志")]
        public bool runtimeLog = true;
    }
}