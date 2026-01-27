using System;
using UnityEngine;

namespace CFramework.Core
{
    [Serializable]
    public class LoggerColorConfig
    {
        [Header("日志等级颜色")]
        public Color debugColor = new Color(0.5f, 0.5f, 0.5f);
        public Color infoColor = new Color(0.3f, 0.6f, 1f);
        public Color warningColor = new Color(1f, 0.8f, 0.3f);
        public Color errorColor = new Color(1f, 0.3f, 0.3f);

        [Header("Tag 颜色配置")]
        public bool enableTagColor = true;
        public Color tagColor = new Color(0.7f, 0.7f, 0.9f);

        [Header("自定义 Tag 颜色")]
        public CustomTagColor[] customTagColors;
    }
}