namespace CFramework.Core.Editor.EditorFramework.Interfaces
{
    /// <summary>
    ///     编辑器项目变化接口,在项目资源发生变化时调用
    /// </summary>
    public interface IEditorProjectChange
    {
        void OnEditorProjectChanged();
    }
}