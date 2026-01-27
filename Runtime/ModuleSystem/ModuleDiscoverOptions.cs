using System;
using System.Collections.Generic;

namespace CFramework.Core.ModuleSystem
{
    [Serializable]
    public class ModuleDiscoverOptions
    {
        private string[] assemblyWhitelist;

        public ModuleDiscoverOptions(string[] assemblyWhitelist = null)
        {
            this.assemblyWhitelist = assemblyWhitelist ?? Array.Empty<string>();
        }

        /// <summary>
        /// 获取所有已启用的模块配置列表
        /// </summary>
        /// <returns>已启用模块的类型全名列表</returns>
        public Func<string[]> GetEnabledModules { get; set; }

        /// <summary>
        /// 获取模块的启用状态（向后兼容，用于扫描模式）
        /// </summary>
        /// <param name="moduleTypeFullName">模块类型的完整名称</param>
        /// <param name="getModuleEnabled">从配置中获取模块启用状态的回调函数</param>
        /// <returns>是否启用该模块</returns>
        public Func<string, bool> GetModuleEnabled { get; set; }

        public bool IsModuleEnabled(string moduleTypeFullName)
        {
            if (string.IsNullOrEmpty(moduleTypeFullName)) return true;

            // 如果没有设置获取回调，默认启用
            if (GetModuleEnabled == null) return true;

            return GetModuleEnabled(moduleTypeFullName);
        }

        public bool MatchAssembly(string fullName)
        {
            if (assemblyWhitelist == null || assemblyWhitelist.Length == 0) return true;

            foreach (var w in assemblyWhitelist)
            {
                if (!string.IsNullOrEmpty(w) && fullName.Contains(w)) return true;
            }

            return false;
        }
    }
}