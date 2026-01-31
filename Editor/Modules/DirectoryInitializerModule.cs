using CFramework.Core.Editor.Base;
using CFramework.Core.Editor.EditorFramework;
using CFramework.Core.Editor.EditorFramework.Interfaces;
using CFramework.Core.Editor.Utilities;

namespace CFramework.Core.Editor.Modules
{
    [AutoEditorModule("DirectoryInitializerModule", 1)]
    public class DirectoryInitializerModule : IEditorModule, IEditorFrameworkInitialize
    {
        public void OnEditorFrameworkInitialize()
        {
            InitializeDirectories();
        }

        private void InitializeDirectories()
        {
            string[] directories =
            {
                CFDirectoryKey.FrameworkRoot,
                CFDirectoryKey.FrameworkGenerate,
                CFDirectoryKey.FrameworkConfig,
                CFDirectoryKey.FrameworkEditorConfig
            };

            foreach (string dir in directories)
            {
                CFDirectoryUtility.EnsureFolder(dir);
            }
        }
    }
}