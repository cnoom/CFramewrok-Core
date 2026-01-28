using System;
using System.Collections.Generic;
using UnityEditor;

namespace CFramework.Core.Editor.Utilities
{
    public static class CFDirectoryUtility
    {
        private static readonly HashSet<string> _CreatedFolders = new HashSet<string>();

        public static void EnsureFolder(string folderPath)
        {
            if(string.IsNullOrEmpty(folderPath) || !folderPath.StartsWith("Assets", StringComparison.Ordinal))
            {
                EditorLogUtility.LogWarning($"EnsureFolder 路径非法: {folderPath}");
                return;
            }

            // 逐层确定目录存在，不存在则生成
            string[] folders = folderPath.Split('/');
            string currentPath = folders[0];

            for(int i = 1; i < folders.Length; i++)
            {
                string nextPath = currentPath + "/" + folders[i];

                if(!AssetDatabase.IsValidFolder(nextPath) && !_CreatedFolders.Contains(nextPath))
                {
                    EditorLogUtility.LogInfo($"创建文件夹: {nextPath}");
                    string guid = AssetDatabase.CreateFolder(currentPath, folders[i]);
                    if(string.IsNullOrEmpty(guid))
                    {
                        EditorLogUtility.LogError($"创建文件夹失败: {nextPath}");
                        return;
                    }
                    _CreatedFolders.Add(nextPath);
                    AssetDatabase.Refresh();
                }

                currentPath = nextPath;
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 清除已创建文件夹的缓存（在重新加载场景或重新导入时调用）
        /// </summary>
        public static void ClearCreatedFoldersCache()
        {
            _CreatedFolders.Clear();
        }
    }
}