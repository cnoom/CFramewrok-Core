using System;

namespace CFramework.Core.Config
{
    /// <summary>
    /// 自动配置特性 - 用于运行时配置类
    /// 配置文件将自动创建到 CFDirectoryKey.FrameworkConfig 目录
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class AutoConfigAttribute : Attribute
    {
        /// <summary>
        /// 配置文件名称（不含 .asset 扩展名）
        /// 默认使用类名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 相对于 Config 目录的子文件夹路径
        /// 例如："Audio" 会生成到 Assets/CFramework/Config/Audio
        /// 默认为空，直接创建到 Config 目录
        /// </summary>
        public string SubFolder { get; set; }

        /// <summary>
        /// 是否在配置文件不存在时自动创建
        /// 默认为 true
        /// </summary>
        public bool AutoCreate { get; set; } = true;

        /// <summary>
        /// 是否为必需配置（不存在时报错）
        /// 默认为 false
        /// </summary>
        public bool Required { get; set; } = false;

        public AutoConfigAttribute()
        {
            FileName = string.Empty;
            SubFolder = string.Empty;
        }

        public AutoConfigAttribute(string fileName)
        {
            FileName = fileName;
            SubFolder = string.Empty;
        }

        public AutoConfigAttribute(string fileName, string subFolder)
        {
            FileName = fileName;
            SubFolder = subFolder;
        }
    }
}
