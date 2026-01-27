using System;
using System.Collections.Generic;
using System.Reflection;
using CFramework.Core.Editor.EditorFramework.Interfaces;
using CFramework.Core.Editor.Utilities;
using UnityEngine;

namespace CFramework.Core.Editor.EditorFramework
{
    /// <summary>
    /// 编辑器模块发现器,负责自动发现并注册带有AutoEditorModule特性的模块
    /// </summary>
    public static class EditorModuleDiscover
    {
        /// <summary>
        /// 发现并注册所有编辑器模块
        /// </summary>
        public static void DiscoverAndRegisterModules()
        {
            EditorLogUtility.LogInfo("开始发现编辑器模块...");

            var modules = FindModules();

            if (modules.Count == 0)
            {
                EditorLogUtility.LogInfo("未找到编辑器模块。");
                return;
            }

            EditorLogUtility.LogInfo($"发现 {modules.Count} 个编辑器模块。");

            foreach (var moduleInfo in modules)
            {
                try
                {
                    var module = Activator.CreateInstance(moduleInfo.type) as IEditorModule;
                    if (module != null)
                    {
                        EditorModuleManager.Instance.RegisterModule(module);
                    }
                }
                catch (Exception ex)
                {
                    EditorLogUtility.LogError($"创建模块 {moduleInfo.moduleName} 失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 查找所有编辑器模块类型
        /// </summary>
        private static List<(Type type, string moduleName, int priority)> FindModules()
        {
            var modules = new List<(Type type, string moduleName, int priority)>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                if (ShouldScanAssembly(assembly))
                {
                    var assemblyModules = ScanAssemblyForModules(assembly);
                    modules.AddRange(assemblyModules);
                }
            }

            modules.Sort((a, b) => b.priority.CompareTo(a.priority));

            return modules;
        }

        /// <summary>
        /// 判断是否应该扫描程序集
        /// </summary>
        private static bool ShouldScanAssembly(Assembly assembly)
        {
            if (assembly.IsDynamic) return false;

            var name = assembly.GetName().Name;

            // 排除Unity和系统程序集
            if (name.StartsWith("Unity.") || name.StartsWith("UnityEngine.")) return false;
            if (name.StartsWith("nunit") || name.StartsWith("JetBrains")) return false;
            if (name.StartsWith("System.") || name.StartsWith("Microsoft.")) return false;
            if (name.StartsWith("Mono.")) return false;
            if (name.StartsWith("mscorlib") || name.StartsWith("netstandard")) return false;

            // 只扫描Editor程序集
            if (!name.Contains("Editor")) return false;

            return true;
        }

        /// <summary>
        /// 扫描程序集中的模块类型
        /// </summary>
        private static List<(Type type, string moduleName, int priority)> ScanAssemblyForModules(Assembly assembly)
        {
            var modules = new List<(Type type, string moduleName, int priority)>();

            try
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    if (!type.IsClass || type.IsAbstract) continue;
                    if (!typeof(IEditorModule).IsAssignableFrom(type)) continue;

                    var attr = type.GetCustomAttribute<AutoEditorModuleAttribute>();
                    if (attr != null)
                    {
                        modules.Add((type, attr.ModuleName, attr.Priority));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"扫描程序集 {assembly.GetName().Name} 失败: {ex.Message}");
            }

            return modules;
        }
    }
}
