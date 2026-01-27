using System;
using UnityEngine;

namespace CFramework.Core.Execution
{
    public enum ConcurrencyMode
    {
        /// <summary>
        /// 顺序模式。在该模式下，所有任务或操作将按照它们被添加或请求的顺序依次执行，一个接一个地完成。
        /// 这种模式适用于那些需要保证操作顺序性的场景，例如当后一个操作依赖于前一个操作的结果时。
        /// </summary>
        [InspectorName("顺序执行")] Sequential = 0,

        /// <summary>
        /// 并发模式。在该模式下，所有任务或操作将同时执行，无需等待前一个任务完成即可开始下一个任务。
        /// 这种模式适用于那些可以并行处理的任务，能够充分利用多核处理器的性能，加快处理速度。
        /// </summary>
        [InspectorName("并发执行")] Concurrent = 1
    }

    public enum ErrorPolicy
    {
        [InspectorName("继续执行")] Continue = 0,    // 记录异常，继续执行其他处理器
        [InspectorName("遇错停止")] StopOnError = 1  // 首次异常即停止
    }

    public enum CancellationPolicy
    {
        [InspectorName("跳过当前")] SkipCurrent = 0, // 当前被取消则跳过该处理器，继续后续
        [InspectorName("取消全部")] CancelAll = 1    // 取消发生时，中止整条链（默认）
    }

    [Serializable]
    public class CFExecutionOptions
    {
        public bool EnsureMainThread = true;
        public ConcurrencyMode BroadcastConcurrency = ConcurrencyMode.Concurrent;
        public ErrorPolicy ErrorPolicy = ErrorPolicy.Continue;
        public CancellationPolicy CancellationPolicy = CancellationPolicy.CancelAll;

        // 超时时间（秒）
        public float OverallTimeoutSeconds = 10f;
        public float PerHandlerTimeoutSeconds = 3f;

        // 查询系统增强
        public bool QueryDeduplicateEnabled = false;     // 是否对相同查询并发请求进行去重
        public bool QueryCacheEnabled = false;           // 是否启用简单缓存
        public float QueryCacheTtlSeconds = 0f;          // 缓存TTL（秒，<=0 表示不过期直至手动失效）

        // 可观测性（初级占位，后续可扩展为多 Sink/限流）
        public bool MetricsEnabled = false;              // 简单指标计数是否启用
        public bool LogStructuredEnabled = true;         // 输出结构化键值日志（CorrelationId、耗时等）

        // 便捷获取 TimeSpan
        public TimeSpan OverallTimeout => OverallTimeoutSeconds > 0 ? TimeSpan.FromSeconds(OverallTimeoutSeconds) : TimeSpan.Zero;
        public TimeSpan PerHandlerTimeout => PerHandlerTimeoutSeconds > 0 ? TimeSpan.FromSeconds(PerHandlerTimeoutSeconds) : TimeSpan.Zero;
        public TimeSpan QueryCacheTtl => QueryCacheTtlSeconds > 0 ? TimeSpan.FromSeconds(QueryCacheTtlSeconds) : TimeSpan.Zero;
    }
}