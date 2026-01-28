using System;
using UnityEditor;

namespace CFramework.Core.Editor.Utilities
{
    public static class CFDirectoryUtility
    {
        public static void EnsureFolder(string folderPath)
        {
            if(string.IsNullOrEmpty(folderPath) || !folderPath.StartsWith("Assets", StringComparison.Ordinal))
            {
                EditorLogUtility.LogWarning($"EnsureFolder 路径非法: {folderPath}");
                return;
            }

            //逐层确定目录存在，不存在则生成
            string[] folders = folderPath.Split('/');
            string currentPath = folders[0];

            for(int i = 1; i < folders.Length; i++)
            {
                string nextPath = currentPath + "/" + folders[i];

                if(!AssetDatabase.IsValidFolder(nextPath))
                {
                    EditorLogUtility.LogInfo($"创建文件夹: {nextPath}");
                    string guid = AssetDatabase.CreateFolder(currentPath, folders[i]);
                    if(string.IsNullOrEmpty(guid))
                    {
                        EditorLogUtility.LogError($"创建文件夹失败: {nextPath}");
                        return;
                    }
                    AssetDatabase.Refresh();        
                }

                currentPath = nextPath;
            }

            AssetDatabase.Refresh();
        }
    }
}