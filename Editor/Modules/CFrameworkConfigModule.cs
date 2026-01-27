using CFramework.Core.Editor.Base;
using CFramework.Core.Editor.EditorFramework;
using CFramework.Core.Editor.EditorFramework.Interfaces;
using CFramework.Core.Editor.Utilities;
using UnityEditor;

namespace CFramework.Core.Editor.Modules
{
    [AutoEditorModule("DirectoryInitializerModule", 10)]
    public class CFrameworkConfigModule : IEditorModule, IEditorFrameworkInitialize
    {
        public void OnEditorFrameworkInitialize()
        {
            EnsureCFrameworkConfig();
        }

        private void EnsureCFrameworkConfig()
        {
            ConfigUtility.GetRuntimeConfig<CFrameworkConfig>();
        }

        [MenuItem(CFMenuKey.Base + "生成框架配置")]
        private static void CreateCFrameworkConfig()
        {
            ConfigUtility.GetRuntimeConfig<CFrameworkConfig>();
        }
    }
}