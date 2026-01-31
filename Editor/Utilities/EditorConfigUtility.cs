using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CFramework.Core.Editor.Utilities
{
    /// <summary>
    ///     CFramework编辑器配置工具类,提供统一的配置加载、保存和管理功能
    /// </summary>
    /// <typeparam name="T">配置类型</typeparam>
    public abstract class EditorConfigUtility<T> : ScriptableObject where T : ScriptableObject
    {
        /// <summary>
        ///     获取配置的默认Asset路径(子类可选重写)
        /// </summary>
        protected virtual string DefaultAssetPath => string.Empty;

        /// <summary>
        ///     配置加载完成后的回调(子类可选重写)
        /// </summary>
        protected virtual void OnConfigLoaded() { }

        /// <summary>
        ///     加载或创建配置实例
        /// </summary>
        /// <returns>配置实例</returns>
        public static T LoadOrCreate()
        {
            Type configType = typeof(T);
            EditorConfigUtility<T> instance = CreateInstance<T>() as EditorConfigUtility<T>;
            string assetPath = instance?.DefaultAssetPath ?? string.Empty;

            if(string.IsNullOrEmpty(assetPath))
            {
                EditorLogUtility.LogError($"配置类型 {configType.Name} 未配置Asset路径", "CFramework.Config");
                return CreateInstance<T>();
            }

            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if(!asset)
            {
                asset = CreateInstance<T>();
                string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
                if(!string.IsNullOrEmpty(dir))
                {
                    CFDirectoryUtility.EnsureFolder(dir);
                }
                AssetDatabase.CreateAsset(asset, assetPath);
                AssetDatabase.SaveAssets();
            }

            EditorConfigUtility<T> config = asset as EditorConfigUtility<T>;
            config?.OnConfigLoaded();

            return asset;
        }

        /// <summary>
        ///     从自定义路径加载配置
        /// </summary>
        /// <param name="customPath">自定义路径</param>
        /// <returns>配置实例</returns>
        public static T LoadFromPath(string customPath)
        {
            if(string.IsNullOrEmpty(customPath))
            {
                return LoadOrCreate();
            }

            T asset = AssetDatabase.LoadAssetAtPath<T>(customPath);
            if(!asset)
            {
                asset = CreateInstance<T>();
                string dir = Path.GetDirectoryName(customPath).Replace('\\', '/');
                if(!string.IsNullOrEmpty(dir))
                {
                    CFDirectoryUtility.EnsureFolder(dir);
                }
                AssetDatabase.CreateAsset(asset, customPath);
                AssetDatabase.SaveAssets();
            }

            EditorConfigUtility<T> config = asset as EditorConfigUtility<T>;
            config?.OnConfigLoaded();

            return asset;
        }

        /// <summary>
        ///     保存配置更改
        /// </summary>
        public void Save()
        {
            if(!this)
            {
                EditorLogUtility.LogWarning("尝试保存空配置实例", "CFramework.Config");
                return;
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            EditorLogUtility.LogInfo($"已保存配置: {GetType().Name}", "CFramework.Config");
        }

        /// <summary>
        ///     查找项目中的所有配置资产
        /// </summary>
        /// <returns>配置资产GUID列表</returns>
        public static string[] FindAssets()
        {
            Type configType = typeof(T);
            return AssetDatabase.FindAssets($"t:{configType.Name}");
        }

        /// <summary>
        ///     获取第一个找到的配置资产(如果没有则加载默认配置)
        /// </summary>
        /// <returns>配置实例</returns>
        public static T FindFirst()
        {
            string[] guids = FindAssets();
            if(guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<T>(path);
            }

            return LoadOrCreate();
        }
    }
}