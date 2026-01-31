using System;
using UnityEngine.Scripting;

namespace CFramework.Core.Attributes
{
    /// <summary>
    ///     QueryHandlerAttribute 是一个用于标记处理查询请求的方法的属性。它允许框架或应用程序识别并调用特定的方法来处理对应的查询逻辑。
    ///     该属性应该被应用于类中的方法，以表明这些方法是专门用来处理某种类型的查询请求的。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class QueryHandlerAttribute : PreserveAttribute
    {
    }
}