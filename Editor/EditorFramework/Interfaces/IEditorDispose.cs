namespace CFramework.Core.Editor.EditorFramework.Interfaces
{
    /// <summary>
    ///     编辑器释放接口,在编辑器关闭或重新编译时调用
    /// </summary>
    public interface IEditorDispose
    {
        void OnEditorDispose();
    }
}