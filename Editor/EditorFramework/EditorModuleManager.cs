using System;
using System.Collections.Generic;
using System.Linq;
using CFramework.Core.Editor.EditorFramework.Interfaces;
using CFramework.Core.Editor.Utilities;

namespace CFramework.Core.Editor.EditorFramework
{
    /// <summary>
    ///     编辑器模块管理器,负责管理编辑器模块的生命周期
    /// </summary>
    public class EditorModuleManager
    {
        private static EditorModuleManager _instance;

        private readonly Dictionary<Type, IEditorModule> _modules = new Dictionary<Type, IEditorModule>();
        private readonly List<IEditorModule> _sortedModules = new List<IEditorModule>();


        private EditorModuleManager() { }
        public static EditorModuleManager Instance => _instance ??= new EditorModuleManager();

        /// <summary>
        ///     初始化模块管理器
        /// </summary>
        public void Initialize()
        {
            EditorLogUtility.LogInfo("编辑器模块 初始化...");
        }

        /// <summary>
        ///     注册模块
        /// </summary>
        public void RegisterModule(IEditorModule module)
        {
            if(module == null) return;

            Type type = module.GetType();
            if(_modules.ContainsKey(type))
            {
                EditorLogUtility.LogWarning($"模块 {type.Name} 已注册，跳过。");
                return;
            }

            _modules[type] = module;
            _sortedModules.Add(module);
            SortModules();
            EditorLogUtility.LogInfo($"模块 {type.Name} 已注册。");
        }

        /// <summary>
        ///     注销模块
        /// </summary>
        public void UnregisterModule(IEditorModule module)
        {
            if(module == null) return;

            Type type = module.GetType();
            if(!_modules.Remove(type))
            {
                EditorLogUtility.LogWarning($"模块 {type.Name} 未找到，无法注销。");
                return;
            }

            _sortedModules.Remove(module);
            EditorLogUtility.LogInfo($"模块 {type.Name} 已注销。");
        }

        /// <summary>
        ///     获取模块
        /// </summary>
        public T GetModule<T>() where T : class, IEditorModule
        {
            Type type = typeof(T);
            if(_modules.TryGetValue(type, out IEditorModule module))
            {
                return module as T;
            }

            return null;
        }

        /// <summary>
        ///     调用模块初始化
        /// </summary>
        public void CallFrameworkInitialize()
        {
            foreach (IEditorModule module in _sortedModules)
            {
                if(module is IEditorFrameworkInitialize initialize)
                {
                    try
                    {
                        initialize.OnEditorFrameworkInitialize();
                    }
                    catch (Exception ex)
                    {
                        EditorLogUtility.LogError($"错误 {module.GetType().Name}.OnEditorFrameworkInitialize: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        ///     调用模块初始化
        /// </summary>
        public void CallInitialize()
        {
            foreach (IEditorModule module in _sortedModules)
            {
                if(module is IEditorInitialize initialize)
                {
                    try
                    {
                        initialize.OnEditorInitialize();
                    }
                    catch (Exception ex)
                    {
                        EditorLogUtility.LogError($"错误 {module.GetType().Name}.OnEditorInitialize: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        ///     调用模块更新
        /// </summary>
        public void CallUpdate()
        {
            foreach (IEditorModule module in _sortedModules)
            {
                if(module is IEditorUpdate update)
                {
                    try
                    {
                        update.OnEditorUpdate();
                    }
                    catch (Exception ex)
                    {
                        EditorLogUtility.LogError($"错误 {module.GetType().Name}.OnEditorUpdate: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        ///     调用模块GUI
        /// </summary>
        public void CallGUI()
        {
            foreach (IEditorModule module in _sortedModules)
            {
                if(module is IEditorGUI gui)
                {
                    try
                    {
                        gui.OnEditorGUI();
                    }
                    catch (Exception ex)
                    {
                        EditorLogUtility.LogError($"错误 {module.GetType().Name}.OnEditorGUI: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        ///     调用模块层级变化
        /// </summary>
        public void CallHierarchyChanged()
        {
            foreach (IEditorModule module in _sortedModules)
            {
                if(module is IEditorHierarchyChange hierarchyChange)
                {
                    try
                    {
                        hierarchyChange.OnEditorHierarchyChanged();
                    }
                    catch (Exception ex)
                    {
                        EditorLogUtility.LogError($"错误 {module.GetType().Name}.OnEditorHierarchyChanged: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        ///     调用模块项目变化
        /// </summary>
        public void CallProjectChanged()
        {
            foreach (IEditorModule module in _sortedModules)
            {
                if(module is IEditorProjectChange projectChange)
                {
                    try
                    {
                        projectChange.OnEditorProjectChanged();
                    }
                    catch (Exception ex)
                    {
                        EditorLogUtility.LogError($"错误 {module.GetType().Name}.OnEditorProjectChanged: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        ///     调用模块选择变化
        /// </summary>
        public void CallSelectionChanged()
        {
            foreach (IEditorModule module in _sortedModules)
            {
                if(module is IEditorSelectionChange selectionChange)
                {
                    try
                    {
                        selectionChange.OnEditorSelectionChanged();
                    }
                    catch (Exception ex)
                    {
                        EditorLogUtility.LogError($"错误 {module.GetType().Name}.OnEditorSelectionChanged: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        ///     调用模块播放模式状态变化
        /// </summary>
        public void CallPlayModeStateChanged(EditorPlayModeStateChange playModeStateChange)
        {
            foreach (IEditorModule module in _sortedModules)
            {
                if(module is IEditorPlayModeChange playModeChange)
                {
                    try
                    {
                        playModeChange.OnEditorPlayModeStateChanged(playModeStateChange);
                    }
                    catch (Exception ex)
                    {
                        EditorLogUtility.LogError($"错误 {module.GetType().Name}.OnEditorPlayModeStateChanged: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        ///     调用模块释放
        /// </summary>
        public void CallDispose()
        {
            foreach (IEditorModule module in _sortedModules)
            {
                if(module is IEditorDispose dispose)
                {
                    try
                    {
                        dispose.OnEditorDispose();
                    }
                    catch (Exception ex)
                    {
                        EditorLogUtility.LogError($"错误 {module.GetType().Name}.OnEditorDispose: {ex.Message}");
                    }
                }
            }
            EditorLogUtility.LogInfo($"已为 {_sortedModules.Count(m => m is IEditorDispose)} 个模块调用 OnEditorDispose。");
        }

        /// <summary>
        ///     按优先级排序模块
        /// </summary>
        private void SortModules()
        {
            _sortedModules.Sort((a, b) =>
            {
                AutoEditorModuleAttribute attrA = a.GetType().GetCustomAttributes(typeof(AutoEditorModuleAttribute), false).FirstOrDefault() as AutoEditorModuleAttribute;
                AutoEditorModuleAttribute attrB = b.GetType().GetCustomAttributes(typeof(AutoEditorModuleAttribute), false).FirstOrDefault() as AutoEditorModuleAttribute;

                int priorityA = attrA?.Priority ?? 0;
                int priorityB = attrB?.Priority ?? 0;

                return priorityA.CompareTo(priorityB);
            });
        }
    }
}