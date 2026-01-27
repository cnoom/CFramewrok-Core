using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CFramework.Core.Config;
using CFramework.Core.Editor.Attributes;
using CFramework.Core.Editor.Base;
using UnityEditor;
using UnityEngine;

namespace CFramework.Core.Editor.Utilities
{
    /// <summary>
    /// 配置工具类 - 自动创建和管理配置文件
    /// 支持 AutoConfigAttribute（运行时配置）和 EditorConfigAttribute（编辑器配置）
    /// </summary>
    public static class ConfigUtility
    {
        private static readonly Dictionary<Type, ScriptableObject> RuntimeCache = new();
        private static readonly Dictionary<Type, ScriptableObject> EditorCache = new();

        #region 运行时配置 (AutoConfigAttribute)

        /// <summary>
        /// 获取运行时配置，自动创建
        /// </summary>
        public static T GetRuntimeConfig<T>() where T : ScriptableObject
        {
            var type = typeof(T);

            if (RuntimeCache.TryGetValue(type, out var cached))
                return (T)cached;

            var attr = type.GetCustomAttribute<AutoConfigAttribute>();
            if (attr == null)
            {
                Debug.LogError($"配置类型 [{type.FullName}] 未标记 [AutoConfig] 特性");
                return null;
            }

            string fileName = string.IsNullOrEmpty(attr.FileName) ? type.Name : attr.FileName;
            string folder = CFDirectoryKey.FrameworkConfig;
            if (!string.IsNullOrEmpty(attr.SubFolder))
            {
                folder = Path.Combine(folder, attr.SubFolder).Replace("\\", "/");
            }
            CFDirectoryUtility.EnsureFolder(folder);

            string assetPath = $"{folder}/{fileName}.asset";

            var config = AssetDatabase.LoadAssetAtPath<T>(assetPath);

            if (config == null)
            {
                if (attr.AutoCreate)
                {
                    config = CreateConfigAsset<T>(assetPath);
                }
                else if (attr.Required)
                {
                    Debug.LogError($"必需的运行时配置不存在: {assetPath}");
                    return null;
                }
            }

            if (config != null)
            {
                RuntimeCache[type] = config;
            }

            return config;
        }

        /// <summary>
        /// 重新加载运行时配置（清除缓存）
        /// </summary>
        public static void ReloadRuntimeConfig<T>() where T : ScriptableObject
        {
            var type = typeof(T);
            if (RuntimeCache.ContainsKey(type))
            {
                RuntimeCache.Remove(type);
            }
        }

        /// <summary>
        /// 清除所有运行时配置缓存
        /// </summary>
        public static void ClearRuntimeCache()
        {
            RuntimeCache.Clear();
        }

        #endregion

        #region 编辑器配置 (EditorConfigAttribute)

        /// <summary>
        /// 获取编辑器配置，自动创建
        /// </summary>
        public static T GetEditorConfig<T>() where T : ScriptableObject
        {
            var type = typeof(T);

            if (EditorCache.TryGetValue(type, out var cached))
                return (T)cached;

            var attr = type.GetCustomAttribute<EditorConfigAttribute>();
            if (attr == null)
            {
                Debug.LogError($"配置类型 [{type.FullName}] 未标记 [EditorConfig] 特性");
                return null;
            }

            string fileName = string.IsNullOrEmpty(attr.FileName) ? type.Name : attr.FileName;
            string folder = CFDirectoryKey.FrameworkEditorConfig;
            if (!string.IsNullOrEmpty(attr.SubFolder))
            {
                folder = Path.Combine(folder, attr.SubFolder).Replace("\\", "/");
            }
            CFDirectoryUtility.EnsureFolder(folder);
            
            string assetPath = $"{folder}/{fileName}.asset";

            var config = AssetDatabase.LoadAssetAtPath<T>(assetPath);

            if (config == null)
            {
                if (attr.AutoCreate)
                {
                    config = CreateConfigAsset<T>(assetPath);
                }
                else if (attr.Required)
                {
                    Debug.LogError($"必需的编辑器配置不存在: {assetPath}");
                    return null;
                }
            }

            if (config != null)
            {
                EditorCache[type] = config;
            }

            return config;
        }

        /// <summary>
        /// 重新加载编辑器配置（清除缓存）
        /// </summary>
        public static void ReloadEditorConfig<T>() where T : ScriptableObject
        {
            var type = typeof(T);
            if (EditorCache.ContainsKey(type))
            {
                EditorCache.Remove(type);
            }
        }

        /// <summary>
        /// 清除所有编辑器配置缓存
        /// </summary>
        public static void ClearEditorCache()
        {
            EditorCache.Clear();
        }

        #endregion

        #region 通用方法

        /// <summary>
        /// 创建配置资源文件
        /// </summary>
        private static T CreateConfigAsset<T>(string assetPath) where T : ScriptableObject
        {
            var config = ScriptableObject.CreateInstance<T>();

            // 确保目录存在
            string directory = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                EditorUtility.SetDirty(config);
                AssetDatabase.CreateAsset(config, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorLogUtility.LogInfo($"创建配置文件: {assetPath}", "Config");
            }
            else
            {
                EditorLogUtility.LogInfo($"创建配置文件: {assetPath}", "Config");
                AssetDatabase.CreateAsset(config, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return config;
        }

        /// <summary>
        /// 确保配置文件存在
        /// </summary>
        public static void EnsureConfigExists<T>() where T : ScriptableObject
        {
            var type = typeof(T);
            var runtimeAttr = type.GetCustomAttribute<AutoConfigAttribute>();
            var editorAttr = type.GetCustomAttribute<EditorConfigAttribute>();

            if (runtimeAttr != null)
            {
                GetRuntimeConfig<T>();
            }
            else if (editorAttr != null)
            {
                GetEditorConfig<T>();
            }
        }

        /// <summary>
        /// 获取配置文件路径（不创建文件）
        /// </summary>
        public static string GetConfigPath<T>() where T : ScriptableObject
        {
            var type = typeof(T);

            var runtimeAttr = type.GetCustomAttribute<AutoConfigAttribute>();
            if (runtimeAttr != null)
            {
                string fileName = string.IsNullOrEmpty(runtimeAttr.FileName) ? type.Name : runtimeAttr.FileName;
                string folder = CFDirectoryKey.FrameworkConfig;
                if (!string.IsNullOrEmpty(runtimeAttr.SubFolder))
                {
                    folder = Path.Combine(folder, runtimeAttr.SubFolder).Replace("\\", "/");
                }

                return $"{folder}/{fileName}.asset";
            }

            var editorAttr = type.GetCustomAttribute<EditorConfigAttribute>();
            if (editorAttr != null)
            {
                string fileName = string.IsNullOrEmpty(editorAttr.FileName) ? type.Name : editorAttr.FileName;
                string folder = CFDirectoryKey.FrameworkEditorConfig;
                if (!string.IsNullOrEmpty(editorAttr.SubFolder))
                {
                    folder = Path.Combine(folder, editorAttr.SubFolder).Replace("\\", "/");
                }

                return $"{folder}/{fileName}.asset";
            }

            return null;
        }

        /// <summary>
        /// 清除所有配置缓存
        /// </summary>
        public static void ClearAllCache()
        {
            RuntimeCache.Clear();
            EditorCache.Clear();
        }

        #endregion
    }
}