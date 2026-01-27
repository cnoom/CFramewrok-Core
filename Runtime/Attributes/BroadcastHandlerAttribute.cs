using System;
using CFramework.Core.BroadcastSystem;
using UnityEngine.Scripting;

namespace CFramework.Core.Attributes
{
    /// <summary>
    /// 用于标记广播事件处理方法的特性。通过此特性，可以指定一个方法作为特定广播消息的处理器，并且可以通过设置优先级来控制该处理器在多个处理器中的执行顺序。
    /// </summary>
    /// <remarks>
    /// 该特性应应用于实现了<see cref="IBroadcastData"/>接口的数据类型的方法上，以便这些方法能够被<see cref="BroadcastManager"/>识别并注册为广播处理器。
    /// 方法必须接收单个参数，且该参数类型需实现<see cref="IBroadcastData"/>接口。这样确保了广播系统中数据的一致性和兼容性。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class BroadcastHandlerAttribute : PreserveAttribute
    {
        public int Priority { get; set; }
        public bool RequiresMainThread { get; set; } = true; // 默认主线程，按需可在方法上设置
        public BroadcastHandlerAttribute(int priority = 100)
        {
            Priority = priority;
        }
    }
}