using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
        private static readonly Dictionary<Type, ScriptableObject> EditorCache = new();

        #region 编辑器配置 (EditorConfigAttribute)

        /// <summary>
        /// 获取编辑器配置，自动创建
        /// </summary>
        public static T GetOrCreateEditorConfig<T>() where T : ScriptableObject
        {
            var config = GetEditorConfig<T>();

            if(!config)
            {
                config = CreateEditorConfig<T>(true);
            }
            if(config)
            {
                EditorCache[typeof(T)] = config;
            }
            return config;
        }

        /// <summary>
        /// 获取存在的编辑器配置
        /// </summary>
        public static T GetEditorConfig<T>() where T : ScriptableObject
        {
            var type = typeof(T);
            if(EditorCache.TryGetValue(type, out var cached))
                return (T)cached;
            var attr = type.GetCustomAttribute<EditorConfigAttribute>();
            if(attr == null)
            {
                Debug.LogError($"配置类型 [{type.FullName}] 未标记 [EditorConfig] 特性");
                return null;
            }

            string fileName = string.IsNullOrEmpty(attr.FileName) ? type.Name : attr.FileName;
            string folder = CFDirectoryKey.FrameworkEditorConfig;
            if(!string.IsNullOrEmpty(attr.SubFolder))
            {
                folder = Path.Combine(folder, attr.SubFolder).Replace("\\", "/");
            }
            CFDirectoryUtility.EnsureFolder(folder);

            string assetPath = $"{folder}/{fileName}.asset";

            return AssetDatabase.LoadAssetAtPath<T>(assetPath);
        }

        /// <summary>
        /// 创建编辑器配置
        /// </summary>
        public static T CreateEditorConfig<T>() where T : ScriptableObject
        {
            return CreateEditorConfig<T>(false);
        }
        
        /// <summary>
        /// 创建编辑器配置
        /// </summary>
        /// <param name="fromAutoCreate">来源为自动创建</param>
        public static T CreateEditorConfig<T>(bool fromAutoCreate) where T : ScriptableObject
        {
            var type = typeof(T);
            var attr = type.GetCustomAttribute<EditorConfigAttribute>();
            if(attr == null)
            {
                Debug.LogError($"配置类型 [{type.FullName}] 未标记 [EditorConfig] 特性");
                return null;
            }
            if(fromAutoCreate && !attr.AutoCreate)
            {
                return null;
            }
            string fileName = string.IsNullOrEmpty(attr.FileName) ? type.Name : attr.FileName;
            string folder = CFDirectoryKey.FrameworkEditorConfig;
            if(!string.IsNullOrEmpty(attr.SubFolder))
            {
                folder = Path.Combine(folder, attr.SubFolder).Replace("\\", "/");
            }
            CFDirectoryUtility.EnsureFolder(folder);

            string assetPath = $"{folder}/{fileName}.asset";

            return CreateConfigAsset<T>(assetPath);
        }

        /// <summary>
        /// 重新加载编辑器配置（清除缓存）
        /// </summary>
        public static void ReloadEditorConfig<T>() where T : ScriptableObject
        {
            var type = typeof(T);
            if(EditorCache.ContainsKey(type))
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
            CFDirectoryUtility.EnsureFolder(directory);

            EditorLogUtility.LogInfo($"创建配置文件: {assetPath}", "Config");
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return config;
        }

        #endregion
    }
}