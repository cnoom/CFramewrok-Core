using CFramework.Core.Editor.Attributes;
using UnityEngine;

namespace CFramework.Core.Editor.Base
{
    [EditorConfig("CFrameworkEditorConfig")]
    public class CFrameworkEditorConfig : ScriptableObject
    {
        [Header("初始化")] [Tooltip("是否执行过框架初始化")]
        public bool isInitialized;

    }
}