using System;
using UnityEngine.Scripting;

namespace CFramework.Core.Attributes
{
    /// <summary>
    ///     AutoModuleAttribute 是一个用于标记模块类的特性，以便在程序运行时自动发现并注册这些模块。
    ///     特性主要用于自动初始化功能，减少手动配置的工作量。
    /// </summary>
    /// <remarks>
    ///     此属性仅能应用于类级别。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoModuleAttribute : PreserveAttribute
    {

        public AutoModuleAttribute(string moduleName, string moduleDescription = null)
        {
            ModuleName = moduleName;
            ModuleDescription = moduleDescription ?? string.Empty;
        }
        public string ModuleName { get; private set; }
        public string ModuleDescription { get; private set; }
    }
}