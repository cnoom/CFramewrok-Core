using System;
using CFramework.Core.BroadcastSystem;

namespace CFramework.Core.ModuleSystem
{
    public static class ModuleManageBroadcasts
    {
        /// <summary>
        /// 模块注册
        /// </summary>
        public struct ModuleRegister : IBroadcastData
        {
            public Type ModuleType;
            public ModuleRegister(Type moduleType) => ModuleType = moduleType;
        }

        /// <summary>
        /// 模块注销
        /// </summary>
        public struct ModuleUnregister : IBroadcastData
        {
            public Type ModuleType;
            public ModuleUnregister(Type moduleType) => ModuleType = moduleType;
        }

        /// <summary>
        /// 自动模块发现完成
        /// </summary>
        public struct AutoModuleDiscoveryComplete : IBroadcastData
        {
        }
    }
}