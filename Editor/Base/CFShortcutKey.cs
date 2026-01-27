namespace CFramework.Core.Editor.Base
{
    /// <summary>
    /// CFramework 全局快捷键 ID 定义入口。
    /// 规范说明：
    /// 1. 所有快捷键 ID 必须以 "CFramework/" 开头，并按子系统分组，例如：CFramework/Core/xxx、CFramework/Addressables/xxx。
    /// 2. 所有调用方必须通过本类提供的常量访问 ID，不得在代码中硬编码字符串。
    /// 3. 键位分配建议（仅作为默认方案）：
    ///    - Core 级全局操作：优先使用 Action + Shift + F9~F12；
    ///    - 各子系统主窗口：优先使用 Action + Shift + 数字键（1、2、3...）；
    ///    - 子系统内部重操作（生成代码、批量修改等）：优先使用 Action + Shift + F10~F12 或 Action + Shift + Alt + 字母键。
    /// 4. 快捷键实际键位如需调整，应通过 Unity 的 Edit > Shortcuts 面板修改绑定，不修改本类中的 ID 字符串，以保持代码与文档一致。
    /// </summary>
    public static class CFShortcutKey
    {
        
        public const string Base = "CFramework/";

        /// <summary>
        /// UI 子系统快捷键 ID
        /// </summary>
        public static class UI
        {
            /// <summary>
            /// 生成UI层级常量
            /// </summary>
            public const string GenerateUILayers = "CFramework/UI/Generate UI Layers";
        }
    }
}
