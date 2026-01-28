using CFramework.Core.Editor.Base;
using CFramework.Core.Editor.EditorFramework;
using CFramework.Core.Editor.EditorFramework.Interfaces;
using CFramework.Core.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace CFramework.Core.Editor.Modules
{
    [AutoEditorModule("DirectoryInitializerModule", 10)]
    public class CFrameworkConfigModule : IEditorModule, IEditorFrameworkInitialize
    {
        private readonly static string ConfigPath = CFDirectoryKey.FrameworkConfig + "/CFrameworkConfig.asset";

        public void OnEditorFrameworkInitialize()
        {
            EnsureCFrameworkConfig();
        }

        private void EnsureCFrameworkConfig()
        {
            CFDirectoryUtility.EnsureFolder(CFDirectoryKey.FrameworkConfig);
            var config = AssetDatabase.LoadAssetAtPath<CFrameworkConfig>(ConfigPath);
            if (config != null) return;
            CreateCFrameworkConfig();
        }

        [MenuItem(CFMenuKey.Base + "生成框架配置")]
        private static void CommandCreateCFrameworkConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<CFrameworkConfig>(ConfigPath);
            if (config != null) return;
            CreateCFrameworkConfig();
        }

        private static void CreateCFrameworkConfig()
        {
            var config = ScriptableObject.CreateInstance<CFrameworkConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorLogUtility.LogInfo($"创建 CFrameworkConfig: {ConfigPath}");
        }
    }
}