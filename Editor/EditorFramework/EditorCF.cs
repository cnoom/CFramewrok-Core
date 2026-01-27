using UnityEngine;

namespace CFramework.Core.Editor.EditorFramework
{
    /// <summary>
    /// 编辑器框架静态访问类,提供统一的API访问编辑器框架功能
    /// </summary>
    public static class EditorCF
    {
        /// <summary>
        /// 获取模块
        /// </summary>
        public static T GetModule<T>() where T : class, Interfaces.IEditorModule
        {
            return EditorModuleManager.Instance.GetModule<T>();
        }

        /// <summary>
        /// 手动注册模块
        /// </summary>
        public static void RegisterModule(Interfaces.IEditorModule module)
        {
            EditorModuleManager.Instance.RegisterModule(module);
        }

        /// <summary>
        /// 手动注销模块
        /// </summary>
        public static void UnregisterModule(Interfaces.IEditorModule module)
        {
            EditorModuleManager.Instance.UnregisterModule(module);
        }
    }
}
