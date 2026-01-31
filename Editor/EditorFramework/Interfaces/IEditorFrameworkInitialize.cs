namespace CFramework.Core.Editor.EditorFramework.Interfaces
{
    /// <summary>
    ///     框架初始化，根据框架编辑器配置的是否初始化标记决定是否触发
    /// </summary>
    public interface IEditorFrameworkInitialize
    {
        /// <summary>
        ///     框架初始化，根据框架编辑器配置的是否初始化标记决定是否触发
        /// </summary>
        void OnEditorFrameworkInitialize();
    }
}