using System;
using UnityEngine.Scripting;

namespace CFramework.Core.Editor.EditorFramework
{
    /// <summary>
    ///     自动注册编辑器模块特性,标记了此特性的类会被自动注册到编辑器框架中
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoEditorModuleAttribute : PreserveAttribute
    {

        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="moduleName">模块名称</param>
        /// <param name="priority">模块优先级,默认为100</param>
        public AutoEditorModuleAttribute(string moduleName, int priority = 100)
        {
            ModuleName = moduleName;
            Priority = priority;
        }

        /// <summary>
        ///     模块名称
        /// </summary>
        public string ModuleName { get; }

        /// <summary>
        ///     模块优先级,优先级低的模块会优先被加载
        /// </summary>
        public int Priority { get; }
    }
}