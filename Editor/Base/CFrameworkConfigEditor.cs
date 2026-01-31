using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CFramework.Core.Attributes;
using CFramework.Core.Editor.Utilities;
using CFramework.Core.ModuleSystem;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Assembly = System.Reflection.Assembly;

namespace CFramework.Core.Editor.Base
{
    [CustomEditor(typeof(CFrameworkConfig))]
    public class CFrameworkConfigEditor : UnityEditor.Editor
    {

        private string[] _assemblyOptions = Array.Empty<string>();

        private bool _assemblyScanError;

        private SerializedProperty _assemblyWhitelistProperty;
        private SerializedProperty _autoDiscoverConfigProperty;

        private readonly List<AutoModuleInfo> _autoModuleInfos = new List<AutoModuleInfo>();
        private SerializedProperty _autoModulesProperty;
        private SerializedProperty _executionConfigProperty;

        // 用于检测白名单是否被修改
        private string _lastAssemblyWhitelistHash = string.Empty;
        private SerializedProperty _loggerConfigProperty;
        private bool _moduleScanError;

        // 模块配置折叠状态
        private bool _showModuleConfigs = true;

        private SerializedProperty _tagConfigProperty;

        private void OnEnable()
        {
            _tagConfigProperty = serializedObject.FindProperty("tagConfig");
            _loggerConfigProperty = serializedObject.FindProperty("loggerConfig");
            _executionConfigProperty = serializedObject.FindProperty("executionConfig");
            _autoDiscoverConfigProperty = serializedObject.FindProperty("autoDiscoverConfig");

            if(_autoDiscoverConfigProperty != null)
            {
                _assemblyWhitelistProperty = _autoDiscoverConfigProperty.FindPropertyRelative("assemblyWhitelist");
                _autoModulesProperty = _autoDiscoverConfigProperty.FindPropertyRelative("autoModules");

                // 初始化白名单哈希
                _lastAssemblyWhitelistHash = CalculateAssemblyWhitelistHash();
            }

            BuildAssemblyCache();
            ScanAutoModules();
        }

        /// <summary>
        ///     计算白名单内容的哈希值，用于检测内容变化
        /// </summary>
        private string CalculateAssemblyWhitelistHash()
        {
            if(_assemblyWhitelistProperty == null || _assemblyWhitelistProperty.arraySize == 0)
            {
                return "empty";
            }

            List<string> items = new List<string>();
            for(var i = 0; i < _assemblyWhitelistProperty.arraySize; i++)
            {
                items.Add(_assemblyWhitelistProperty.GetArrayElementAtIndex(i).stringValue);
            }

            items.Sort();
            return string.Join("|", items);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if(_tagConfigProperty != null)
            {
                EditorGUILayout.PropertyField(_tagConfigProperty, true);
                EditorGUILayout.Space();
            }

            if(_loggerConfigProperty != null)
            {
                EditorGUILayout.PropertyField(_loggerConfigProperty, true);
                EditorGUILayout.Space();
            }

            if(_executionConfigProperty != null)
            {
                EditorGUILayout.PropertyField(_executionConfigProperty, true);
                EditorGUILayout.Space();
            }

            if(_autoDiscoverConfigProperty != null)
            {
                DrawAutoDiscoverSection();
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("刷新程序集列表", GUILayout.Height(22)))
                {
                    BuildAssemblyCache();
                }

                if(GUILayout.Button("扫描自动模块", GUILayout.Height(22)))
                {
                    ScanAutoModules();
                }

                if(GUILayout.Button("重置为默认 Inspector", GUILayout.Height(22)))
                {
                    DrawDefaultInspector();
                }
            }

            // 检测白名单是否被修改，如果修改则自动重新扫描模块
            var whitelistChanged = false;
            if(_assemblyWhitelistProperty != null)
            {
                string currentHash = CalculateAssemblyWhitelistHash();
                if(currentHash != _lastAssemblyWhitelistHash)
                {
                    _lastAssemblyWhitelistHash = currentHash;
                    whitelistChanged = true;
                }
            }

            serializedObject.ApplyModifiedProperties();

            // 在应用修改后扫描模块
            if(whitelistChanged)
            {
                ScanAutoModules();
            }
        }

        private void DrawAutoDiscoverSection()
        {
            EditorGUILayout.LabelField("自动发现配置", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            DrawAssemblyWhitelistSection();
            DrawAutoModulesSection();

            if(_assemblyScanError)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("扫描程序集时出现错误，已尽量收集可用项。若列表明显不完整，可点击上方按钮重新刷新。", MessageType.Warning);
            }

            if(_moduleScanError)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("扫描自动模块时出现错误，已尽量收集可用项。请点击\"扫描自动模块\"按钮重新扫描。", MessageType.Warning);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawAssemblyWhitelistSection()
        {

            if(_assemblyWhitelistProperty == null)
            {
                EditorGUILayout.HelpBox("程序集白名单配置未找到。", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("程序集白名单", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            if(_assemblyOptions == null || _assemblyOptions.Length == 0)
            {
                EditorGUILayout.HelpBox("未能找到可用程序集，请点击\"刷新程序集列表\"按钮。", MessageType.Warning);
                EditorGUILayout.PropertyField(_assemblyWhitelistProperty, GUIContent.none, true);
                EditorGUI.indentLevel--;
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("白名单状态");
            int size = _assemblyWhitelistProperty.arraySize;
            string statusText = size == 0 ? "空数组（仅允许当前项目程序集）" : $"已选 {size} 个程序集";
            EditorGUILayout.LabelField(statusText, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            for(var i = 0; i < _assemblyWhitelistProperty.arraySize; i++)
            {
                SerializedProperty elementProperty = _assemblyWhitelistProperty.GetArrayElementAtIndex(i);
                DrawAssemblyElement(elementProperty, i);
            }

            DrawAssemblyAddButton();

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 15);
            if(GUILayout.Button("清空白名单", GUILayout.Width(100)))
            {
                _assemblyWhitelistProperty.ClearArray();
            }
            if(GUILayout.Button("添加所有程序集", GUILayout.Width(120)))
            {
                _assemblyWhitelistProperty.ClearArray();
                for(var i = 0; i < _assemblyOptions.Length; i++)
                {
                    _assemblyWhitelistProperty.InsertArrayElementAtIndex(i);
                    SerializedProperty element = _assemblyWhitelistProperty.GetArrayElementAtIndex(i);
                    element.stringValue = _assemblyOptions[i];
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        private void DrawAssemblyElement(SerializedProperty elementProperty, int index)
        {
            if(elementProperty == null) return;

            string currentValue = elementProperty.stringValue;
            int currentIndex = Array.IndexOf(_assemblyOptions, currentValue);
            bool hasCustomValue = currentIndex < 0 && !string.IsNullOrEmpty(currentValue);

            EditorGUILayout.BeginHorizontal();

            if(hasCustomValue)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField(currentValue);
                }
                EditorGUILayout.LabelField("(自定义值)", EditorStyles.miniLabel, GUILayout.Width(60));
            }
            else
            {
                int newIndex = EditorGUILayout.Popup(currentIndex < 0 ? 0 : currentIndex, _assemblyOptions, GUILayout.ExpandWidth(true));
                elementProperty.stringValue = _assemblyOptions[newIndex];
            }

            if(GUILayout.Button("删除", GUILayout.Width(50)))
            {
                _assemblyWhitelistProperty.DeleteArrayElementAtIndex(index);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAssemblyAddButton()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 15);
            if(GUILayout.Button("添加程序集", GUILayout.Width(100)))
            {
                int newIndex = _assemblyWhitelistProperty.arraySize;
                _assemblyWhitelistProperty.InsertArrayElementAtIndex(newIndex);
                SerializedProperty newElement = _assemblyWhitelistProperty.GetArrayElementAtIndex(newIndex);
                newElement.stringValue = _assemblyOptions.Length > 0 ? _assemblyOptions[0] : string.Empty;
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAutoModulesSection()
        {
            if(_autoModulesProperty == null)
            {
                return;
            }

            EditorGUILayout.Space(8);

            // 折叠标题
            _showModuleConfigs = EditorGUILayout.Foldout(_showModuleConfigs, "自动模块配置", true, EditorStyles.boldLabel);

            if(!_showModuleConfigs)
            {
                return;
            }

            EditorGUI.indentLevel++;

            if(_autoModuleInfos.Count == 0)
            {
                EditorGUILayout.HelpBox("未找到自动模块。请点击\"扫描自动模块\"按钮重新扫描。", MessageType.Info);
                EditorGUI.indentLevel--;
                return;
            }

            EditorGUILayout.HelpBox($"已发现 {_autoModuleInfos.Count} 个自动模块。勾选启用对应模块。", MessageType.Info);
            EditorGUILayout.Space();

            foreach (AutoModuleInfo moduleInfo in _autoModuleInfos)
            {
                bool currentValue = GetModuleEnabled(moduleInfo.ModuleTypeFullName, true);
                bool newValue = EditorGUILayout.Toggle(new GUIContent(moduleInfo.ModuleName, moduleInfo.Description), currentValue);

                if(newValue != currentValue)
                {
                    SetModuleEnabled(moduleInfo.ModuleTypeFullName, newValue);
                }
            }

            EditorGUILayout.Space();
            EditorGUI.indentLevel--;
        }

        private bool GetModuleEnabled(string moduleTypeFullName, bool defaultValue)
        {
            if(_autoModulesProperty == null || string.IsNullOrEmpty(moduleTypeFullName))
                return defaultValue;

            for(var i = 0; i < _autoModulesProperty.arraySize; i++)
            {
                SerializedProperty element = _autoModulesProperty.GetArrayElementAtIndex(i);
                SerializedProperty typeProperty = element.FindPropertyRelative("moduleTypeFullName");
                SerializedProperty enabledProperty = element.FindPropertyRelative("enabled");

                if(typeProperty != null && typeProperty.stringValue == moduleTypeFullName)
                {
                    return enabledProperty != null ? enabledProperty.boolValue : defaultValue;
                }
            }

            return defaultValue;
        }

        private void SetModuleEnabled(string moduleTypeFullName, bool enabled)
        {
            if(_autoModulesProperty == null || string.IsNullOrEmpty(moduleTypeFullName))
                return;

            for(var i = 0; i < _autoModulesProperty.arraySize; i++)
            {
                SerializedProperty element = _autoModulesProperty.GetArrayElementAtIndex(i);
                SerializedProperty typeProperty = element.FindPropertyRelative("moduleTypeFullName");

                if(typeProperty != null && typeProperty.stringValue == moduleTypeFullName)
                {
                    SerializedProperty enabledProperty = element.FindPropertyRelative("enabled");
                    if(enabledProperty != null)
                    {
                        enabledProperty.boolValue = enabled;
                    }
                    return;
                }
            }

            // 如果不存在，添加新配置
            int newIndex = _autoModulesProperty.arraySize;
            _autoModulesProperty.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newElement = _autoModulesProperty.GetArrayElementAtIndex(newIndex);
            newElement.FindPropertyRelative("moduleTypeFullName").stringValue = moduleTypeFullName;
            newElement.FindPropertyRelative("moduleName").stringValue = string.Empty; // 将在扫描时填充
            newElement.FindPropertyRelative("description").stringValue = string.Empty; // 将在扫描时填充
            newElement.FindPropertyRelative("enabled").boolValue = enabled;
        }

        private void BuildAssemblyCache()
        {
            _assemblyScanError = false;

            try
            {
                _assemblyOptions = CompilationPipeline.GetAssemblies()
                    .Select(a => a.name)
                    .Where(n => !IsUnityAssemblyName(n))
                    .Distinct()
                    .OrderBy(n => n)
                    .ToArray();
            }
            catch (Exception)
            {
                _assemblyScanError = true;
                _assemblyOptions = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic)
                    .Select(a => a.GetName().Name)
                    .Where(n => !IsUnityAssemblyName(n))
                    .Distinct()
                    .OrderBy(n => n)
                    .ToArray();
            }
        }

        private void ScanAutoModules()
        {
            _moduleScanError = false;
            _autoModuleInfos.Clear();

            try
            {
                // 获取配置中的白名单
                string[] whitelist = null;
                if(_assemblyWhitelistProperty != null && _assemblyWhitelistProperty.arraySize > 0)
                {
                    whitelist = new string[_assemblyWhitelistProperty.arraySize];
                    for(var i = 0; i < _assemblyWhitelistProperty.arraySize; i++)
                    {
                        whitelist[i] = _assemblyWhitelistProperty.GetArrayElementAtIndex(i).stringValue;
                    }
                }

                List<string> assemblyNames = CompilationPipeline.GetAssemblies()
                    .Select(a => a.name)
                    .Where(n => !IsUnityAssemblyName(n))
                    .Where(n => MatchAssemblyWhitelist(n, whitelist))
                    .ToList();

                foreach (string assemblyName in assemblyNames)
                {
                    Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == assemblyName);

                    if(assembly == null) continue;

                    try
                    {
                        IEnumerable<Type> moduleTypes = assembly.GetTypes()
                            .Where(t => t.GetCustomAttribute<AutoModuleAttribute>() != null)
                            .Where(t => typeof(IModule).IsAssignableFrom(t))
                            .Where(t => !t.IsInterface && !t.IsAbstract)
                            .Where(t => t.GetConstructor(Type.EmptyTypes) != null);

                        foreach (Type moduleType in moduleTypes)
                        {
                            AutoModuleAttribute attr = moduleType.GetCustomAttribute<AutoModuleAttribute>();
                            if(attr != null)
                            {
                                _autoModuleInfos.Add(new AutoModuleInfo
                                {
                                    ModuleTypeFullName = moduleType.FullName,
                                    ModuleName = attr.ModuleName,
                                    Description = attr.ModuleDescription ?? string.Empty
                                });
                            }
                        }
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        EditorLogUtility.LogWarning($"程序集 [{assemblyName}] 类型加载异常: {ex.Message}", "CFramework.Module");
                    }
                    catch (Exception ex)
                    {
                        EditorLogUtility.LogWarning($"扫描程序集 [{assemblyName}] 时出错: {ex.Message}", "CFramework.Module");
                    }
                }

                // 更新现有配置
                UpdateExistingModuleConfigs();
            }
            catch (Exception ex)
            {
                _moduleScanError = true;
                EditorLogUtility.LogError($"扫描自动模块时发生错误: {ex.Message}", "CFramework.Module");
            }
        }

        private void UpdateExistingModuleConfigs()
        {
            if(_autoModulesProperty == null)
                return;

            // 如果没有扫描到模块，清空配置
            if(_autoModuleInfos.Count == 0)
            {
                if(_autoModulesProperty.arraySize > 0)
                {
                    _autoModulesProperty.ClearArray();
                    serializedObject.ApplyModifiedProperties();
                    EditorLogUtility.LogInfo("当前白名单下未发现任何模块，已清空模块配置。", "CFramework.Module");
                }
                return;
            }

            // 收集当前扫描到的所有模块类型全名
            HashSet<string> scannedModuleTypes = new HashSet<string>(_autoModuleInfos
                .Select(m => m.ModuleTypeFullName));

            // 删除不在当前扫描结果中的配置（倒序遍历避免索引问题）
            for(int i = _autoModulesProperty.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty element = _autoModulesProperty.GetArrayElementAtIndex(i);
                SerializedProperty typeProperty = element.FindPropertyRelative("moduleTypeFullName");

                if(typeProperty != null && !string.IsNullOrEmpty(typeProperty.stringValue))
                {
                    if(!scannedModuleTypes.Contains(typeProperty.stringValue))
                    {
                        EditorLogUtility.LogInfo($"模块配置 [{typeProperty.stringValue}] 不在当前白名单范围内，已从配置中移除。", "CFramework.Module");
                        _autoModulesProperty.DeleteArrayElementAtIndex(i);
                    }
                }
            }

            // 更新和添加新发现的模块配置
            foreach (AutoModuleInfo moduleInfo in _autoModuleInfos)
            {
                var exists = false;
                for(var i = 0; i < _autoModulesProperty.arraySize; i++)
                {
                    SerializedProperty element = _autoModulesProperty.GetArrayElementAtIndex(i);
                    SerializedProperty typeProperty = element.FindPropertyRelative("moduleTypeFullName");

                    if(typeProperty != null && typeProperty.stringValue == moduleInfo.ModuleTypeFullName)
                    {
                        exists = true;
                        // 更新名称和描述（如果为空）
                        SerializedProperty nameProperty = element.FindPropertyRelative("moduleName");
                        SerializedProperty descProperty = element.FindPropertyRelative("description");

                        if(nameProperty != null && string.IsNullOrEmpty(nameProperty.stringValue))
                        {
                            nameProperty.stringValue = moduleInfo.ModuleName;
                        }
                        if(descProperty != null && string.IsNullOrEmpty(descProperty.stringValue))
                        {
                            descProperty.stringValue = moduleInfo.Description;
                        }
                        break;
                    }
                }

                // 如果不存在，添加新配置（默认启用）
                if(!exists)
                {
                    int newIndex = _autoModulesProperty.arraySize;
                    _autoModulesProperty.InsertArrayElementAtIndex(newIndex);
                    SerializedProperty newElement = _autoModulesProperty.GetArrayElementAtIndex(newIndex);
                    newElement.FindPropertyRelative("moduleTypeFullName").stringValue = moduleInfo.ModuleTypeFullName;
                    newElement.FindPropertyRelative("moduleName").stringValue = moduleInfo.ModuleName;
                    newElement.FindPropertyRelative("description").stringValue = moduleInfo.Description;
                    newElement.FindPropertyRelative("enabled").boolValue = true; // 默认启用
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static bool IsUnityAssemblyName(string assemblyName)
        {
            if(string.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

            return assemblyName.Equals("UnityEngine", StringComparison.Ordinal) ||
                   assemblyName.Equals("UnityEditor", StringComparison.Ordinal) ||
                   assemblyName.StartsWith("Unity.", StringComparison.Ordinal) ||
                   assemblyName.StartsWith("UnityEngine.", StringComparison.Ordinal) ||
                   assemblyName.StartsWith("UnityEditor.", StringComparison.Ordinal);
        }

        /// <summary>
        ///     检查程序集是否匹配白名单
        /// </summary>
        /// <param name="assemblyName">程序集名称</param>
        /// <param name="whitelist">白名单数组（null或空表示允许所有）</param>
        /// <returns>是否匹配</returns>
        private static bool MatchAssemblyWhitelist(string assemblyName, string[] whitelist)
        {
            if(whitelist == null || whitelist.Length == 0) return true;

            foreach (string w in whitelist)
            {
                if(!string.IsNullOrEmpty(w) && assemblyName.Contains(w)) return true;
            }

            return false;
        }
        private class AutoModuleInfo
        {
            public string ModuleTypeFullName { get; set; }
            public string ModuleName { get; set; }
            public string Description { get; set; }
        }
    }
}