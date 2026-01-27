using System;
using System.Collections.Generic;

namespace CFramework.Core.ModuleSystem
{
    /// <summary>
    /// 模块集合注册器
    /// </summary>
    public class ModulesRegistry
    {
        internal List<Type> ModuleTypes = new();

        public ModulesRegistry RecordModule<TModule>() where TModule : IModule, new()
        {
            Type type = typeof(TModule);
            if (ModuleTypes.Contains(type))
            {
                CF.LogWarning($"模块 {type.FullName} 已存在，将被忽略。");
                return this;
            }

            ModuleTypes.Add(type);
            return this;
        }
    }
}