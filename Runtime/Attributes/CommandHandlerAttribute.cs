using System;
using UnityEngine.Scripting;

namespace CFramework.Core.Attributes
{
    /// <summary>
    ///     CommandHandlerAttribute 是一个用于标记方法的属性，表明该方法可以处理特定命令。此属性通常应用于那些需要响应特定业务逻辑或用户命令的方法上。
    ///     通过使用这个属性，可以在运行时动态地发现和调用这些命令处理器，从而增强应用程序的灵活性和可扩展性。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CommandHandlerAttribute : PreserveAttribute
    {
    }
}