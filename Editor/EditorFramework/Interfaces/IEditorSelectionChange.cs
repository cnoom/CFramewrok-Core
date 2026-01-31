namespace CFramework.Core.Editor.EditorFramework.Interfaces
{
    /// <summary>
    ///     编辑器选择变化接口,在选择对象发生变化时调用
    /// </summary>
    public interface IEditorSelectionChange
    {
        void OnEditorSelectionChanged();
    }
}