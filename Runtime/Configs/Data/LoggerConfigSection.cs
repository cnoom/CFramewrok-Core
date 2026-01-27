using System;
using CFramework.Core.Log;
using UnityEngine;

namespace CFramework.Core
{

    [Serializable]
    public class LoggerConfigSection
    {
        public ICFLogger.Level defaultLogLevel = ICFLogger.Level.Debug;
        public bool loggerGlobalEnabled = true;
        public bool enableDefaultLogger = true;
        public bool enableBroadcastLogger = true;
        public bool enableModuleLogger = true;
        public bool enableCommandLogger = true;
        public bool enableQueryLogger = true;

        [Tooltip("额外日志 Tag 的开关配置（用于非内置系统的 Logger）")]
        public LoggerToggle[] additionalLoggerToggles;

        [Header("日志颜色配置"), Space]
        public LoggerColorConfig colorConfig = new LoggerColorConfig();
    }
}