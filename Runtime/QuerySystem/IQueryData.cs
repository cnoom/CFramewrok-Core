namespace CFramework.Core.QuerySystem
{
    public interface IQueryData
    {
    }

    /// <summary>
    /// 绑定查询结果类型的查询数据接口。
    /// </summary>
    /// <typeparam name="TResult">查询结果类型。</typeparam>
    public interface IQueryData<TResult> : IQueryData
    {
    }
}