using CFramework.Core.Editor.Base;
using CFramework.Core.Editor.EditorFramework.Interfaces;
using CFramework.Core.Editor.Utilities;
using UnityEditor;

namespace CFramework.Core.Editor.EditorFramework
{
    /// <summary>
    /// 编辑器框架类,负责初始化和管理整个编辑器框架的生命周期
    /// </summary>
    [InitializeOnLoad]
    public class EditorCFramework
    {
        [MenuItem(CFMenuKey.Base + "/重新执行框架初始化")]
        private static void RetryFrameworkInitialize()
        {
            ConfigUtility.ClearEditorCache();
            TryFrameworkInitialize(true);
        }

        static EditorCFramework()
        {
            Initialize();
        }

        private static void Initialize()
        {
            EditorModuleManager.Instance.Initialize();

            EditorModuleDiscover.DiscoverAndRegisterModules();

            TryFrameworkInitialize();

            EditorModuleManager.Instance.CallInitialize();

            SetupEditorCallbacks();

            EditorLogUtility.LogInfo("CFramework 编辑器已初始化。");
        }

        private static void TryFrameworkInitialize(bool force = false)
        {
            var config = ConfigUtility.GetEditorConfig<CFrameworkEditorConfig>();
            if (!config.isInitialized || force)
            {
                config.isInitialized = true;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssetIfDirty(config);
                EditorModuleManager.Instance.CallFrameworkInitialize();
            }
        }

        private static void SetupEditorCallbacks()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.delayCall += OnEditorGUI;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.quitting += OnQuitting;
        }

        private static void OnEditorUpdate()
        {
            EditorModuleManager.Instance.CallUpdate();
        }

        private static void OnEditorGUI()
        {
            EditorModuleManager.Instance.CallGUI();
        }

        private static void OnHierarchyChanged()
        {
            EditorModuleManager.Instance.CallHierarchyChanged();
        }

        private static void OnProjectChanged()
        {
            EditorModuleManager.Instance.CallProjectChanged();
        }

        private static void OnSelectionChanged()
        {
            EditorModuleManager.Instance.CallSelectionChanged();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            EditorPlayModeStateChange editorState;
            switch (state)
            {
                case PlayModeStateChange.EnteredEditMode:
                    editorState = Interfaces.EditorPlayModeStateChange.EnteredEditMode;
                    break;
                case PlayModeStateChange.ExitingEditMode:
                    editorState = Interfaces.EditorPlayModeStateChange.EnteringPlayMode;
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    editorState = Interfaces.EditorPlayModeStateChange.EnteredPlayMode;
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    editorState = Interfaces.EditorPlayModeStateChange.ExitingPlayMode;
                    break;
                default:
                    return;
            }

            EditorModuleManager.Instance.CallPlayModeStateChanged(editorState);
        }

        private static void OnQuitting()
        {
            EditorModuleManager.Instance.CallDispose();
        }
    }
}