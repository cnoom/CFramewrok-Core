namespace CFramework.Core.Editor.EditorFramework.Interfaces
{
    /// <summary>
    ///     编辑器层级变化接口,在场景层级发生变化时调用
    /// </summary>
    public interface IEditorHierarchyChange
    {
        void OnEditorHierarchyChanged();
    }
}