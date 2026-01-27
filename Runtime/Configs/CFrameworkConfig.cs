using CFramework.Core.Config;
using UnityEngine;

namespace CFramework.Core
{
    /// <summary>
    /// CFramework 的统一配置对象。创建为 ScriptableObject 资产后，
    /// 在场景入口组件中引用该配置即可完成框架初始化配置。
    /// </summary>
    [AutoConfig("CFrameworkConfig")]
    public class CFrameworkConfig : ScriptableObject
    {
        [Header("Tag命名"), Space] public TagConfigSection tagConfig = new TagConfigSection();

        [Header("日志开关"), Space] public LoggerConfigSection loggerConfig = new LoggerConfigSection();

        [Header("执行策略"), Space] public ExecutionConfigSection executionConfig = new ExecutionConfigSection();

        [Header("自动发现配置"), Space] public AutoDiscoverConfigSection autoDiscoverConfig = new AutoDiscoverConfigSection();
    }
}