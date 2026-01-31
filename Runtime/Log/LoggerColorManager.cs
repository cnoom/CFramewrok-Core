using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using UnityEngine;

namespace CFramework.Core.Log
{
    public static class LoggerColorManager
    {
        private static ConcurrentDictionary<string, Color> _customTagColors = new ConcurrentDictionary<string, Color>();
        private static readonly object _lock = new object();
        private static bool _initialized;
        private static Color _defaultTagColor = new Color(0.7f, 0.7f, 0.9f);
        private static bool _enableTagColor = true;

        public static void Initialize(object config)
        {
            if(config == null) return;

            try
            {
                // 通过反射获取配置，避免程序集依赖
                object loggerConfig = GetPropertyValue(config, "loggerConfig");
                object colorConfig = GetPropertyValue(loggerConfig, "colorConfig");
                IEnumerable customTagColors = GetPropertyValue(colorConfig, "customTagColors") as IEnumerable;
                var enableTagColor = (bool)GetPropertyValue(colorConfig, "enableTagColor");
                Color tagColor = (Color)GetPropertyValue(colorConfig, "tagColor");

                ConcurrentDictionary<string, Color> newCustomTagColors = new ConcurrentDictionary<string, Color>();

                if(customTagColors != null)
                {
                    foreach (object customColor in customTagColors)
                    {
                        if(customColor != null)
                        {
                            var tag = (string)GetPropertyValue(customColor, "tag");
                            Color color = (Color)GetPropertyValue(customColor, "color");

                            if(!string.IsNullOrEmpty(tag))
                            {
                                newCustomTagColors[tag] = color;
                            }
                        }
                    }
                }

                lock (_lock)
                {
                    _customTagColors = newCustomTagColors;
                    _enableTagColor = enableTagColor;
                    _defaultTagColor = tagColor;
                    _initialized = true;
                }
            }
            catch
            {
                // 反射失败时使用默认值
            }
        }

        private static object GetPropertyValue(object obj, string propertyName)
        {
            PropertyInfo property = obj.GetType().GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            return property?.GetValue(obj);
        }

        public static Color GetTagColor(string tag)
        {
            bool initialized;
            bool enableTagColor;
            ConcurrentDictionary<string, Color> customTagColors;
            Color defaultTagColor;

            lock (_lock)
            {
                initialized = _initialized;
                enableTagColor = _enableTagColor;
                customTagColors = _customTagColors;
                defaultTagColor = _defaultTagColor;
            }

            if(!initialized || !enableTagColor)
                return Color.white;

            if(customTagColors.TryGetValue(tag, out Color customColor))
            {
                return customColor;
            }

            return defaultTagColor;
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _customTagColors = new ConcurrentDictionary<string, Color>();
                _initialized = false;
                _enableTagColor = true;
                _defaultTagColor = new Color(0.7f, 0.7f, 0.9f);
            }
        }
    }
}