namespace CFramework.Core.Editor.EditorFramework.Interfaces
{
    /// <summary>
    ///     编辑器播放模式变化接口,在进入/退出播放模式时调用
    /// </summary>
    public interface IEditorPlayModeChange
    {
        void OnEditorPlayModeStateChanged(EditorPlayModeStateChange playModeStateChange);
    }

    /// <summary>
    ///     播放模式状态
    /// </summary>
    public enum EditorPlayModeStateChange
    {
        /// <summary>进入播放模式前</summary>
        EnteredEditMode,
        /// <summary>正在进入播放模式</summary>
        EnteringPlayMode,
        /// <summary>已进入播放模式</summary>
        EnteredPlayMode,
        /// <summary>正在退出播放模式</summary>
        ExitingPlayMode
    }
}