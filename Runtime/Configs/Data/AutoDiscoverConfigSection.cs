using System;
using System.Collections.Generic;
using UnityEngine;

namespace CFramework.Core
{
    [Serializable]
    public class AutoDiscoverConfigSection
    {
        [Tooltip("程序集白名单（为空表示允许所有程序集）")]
        public string[] assemblyWhitelist;

        [Header("自动模块配置"), Space, Tooltip("自动发现的模块配置列表（通过编辑器自动扫描填充）")]
        public ModuleConfig[] autoModules = Array.Empty<ModuleConfig>();

        /// <summary>
        ///     获取指定模块的启用状态
        /// </summary>
        /// <param name="moduleTypeFullName">模块类型的完整名称</param>
        /// <returns>是否启用，默认返回 true</returns>
        public bool IsModuleEnabled(string moduleTypeFullName)
        {
            if(string.IsNullOrEmpty(moduleTypeFullName)) return true;

            foreach (ModuleConfig config in autoModules)
            {
                if(config.moduleTypeFullName == moduleTypeFullName)
                {
                    return config.enabled;
                }
            }

            return true; // 默认启用
        }

        /// <summary>
        ///     设置模块的启用状态
        /// </summary>
        public void SetModuleEnabled(string moduleTypeFullName, bool enabled)
        {
            if(string.IsNullOrEmpty(moduleTypeFullName)) return;

            for(var i = 0; i < autoModules.Length; i++)
            {
                if(autoModules[i].moduleTypeFullName == moduleTypeFullName)
                {
                    autoModules[i].enabled = enabled;
                    return;
                }
            }
        }

        /// <summary>
        ///     添加或更新模块配置
        /// </summary>
        public void AddOrUpdateModuleConfig(string moduleTypeFullName, string moduleName, string description, bool enabled = true)
        {
            if(string.IsNullOrEmpty(moduleTypeFullName)) return;

            for(var i = 0; i < autoModules.Length; i++)
            {
                if(autoModules[i].moduleTypeFullName == moduleTypeFullName)
                {
                    autoModules[i].moduleName = moduleName;
                    autoModules[i].description = description;
                    return;
                }
            }

            // 添加新配置
            ModuleConfig newConfig = new ModuleConfig
            {
                moduleTypeFullName = moduleTypeFullName,
                moduleName = moduleName,
                description = description,
                enabled = enabled
            };

            ModuleConfig[] newList = new ModuleConfig[autoModules.Length + 1];
            autoModules.CopyTo(newList, 0);
            newList[autoModules.Length] = newConfig;
            autoModules = newList;
        }

        /// <summary>
        ///     获取所有模块配置
        /// </summary>
        public ModuleConfig[] GetAllModuleConfigs()
        {
            return autoModules;
        }

        /// <summary>
        ///     获取所有已启用模块的类型全名列表
        /// </summary>
        /// <returns>已启用模块的类型全名数组</returns>
        public string[] GetEnabledModules()
        {
            if(autoModules == null || autoModules.Length == 0)
                return Array.Empty<string>();

            List<string> enabledList = new List<string>(autoModules.Length);
            foreach (ModuleConfig config in autoModules)
            {
                if(!string.IsNullOrEmpty(config.moduleTypeFullName) && config.enabled)
                {
                    enabledList.Add(config.moduleTypeFullName);
                }
            }
            return enabledList.ToArray();
        }
    }

    /// <summary>
    ///     自动模块的配置项
    /// </summary>
    [Serializable]
    public class ModuleConfig
    {
        [Tooltip("模块类型的完整名称（用于唯一标识模块）")]
        public string moduleTypeFullName;

        [Tooltip("模块显示名称（来自 AutoModuleAttribute）")]
        public string moduleName;

        [Tooltip("模块描述（来自 AutoModuleAttribute）"), TextArea(2, 4)]
        public string description;

        [Tooltip("是否启用该模块")]
        public bool enabled = true;
    }
}