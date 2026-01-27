using System;
using CFramework.Core.Execution;
using UnityEngine;

namespace CFramework.Core
{
    [Serializable]
    public class ExecutionConfigSection
    {
        [Tooltip("是否确保在主线程执行（Unity API 访问等需要主线程）")]
        public bool ensureMainThread = true;

        [Tooltip("广播并发执行（true：并发；false：顺序）")]
        public bool broadcastConcurrent = true;

        [Tooltip("整体执行超时时间（秒）")]
        public float overallTimeoutSeconds = 10f;

        [Tooltip("单个处理器超时时间（秒）")]
        public float perHandlerTimeoutSeconds = 3f;

        [Tooltip("错误处理策略")]
        public ErrorPolicy errorPolicy = ErrorPolicy.Continue;

        [Tooltip("取消策略")]
        public CancellationPolicy cancellationPolicy = CancellationPolicy.CancelAll;
    }
}