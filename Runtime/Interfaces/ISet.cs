namespace CFramework.Core.Interfaces
{
    public interface ISet
    {
    }

    public interface ISet<in T> : ISet
    {
        void Set(T value);
    }

    public interface ISet<in T, in T2> : ISet
    {
        void Set(T value, T2 value2);
    }

    public interface ISet<in T, in T2, in T3> : ISet
    {
        void Set(T value, T2 value2, T3 value3);
    }

    public interface ISet<in T, in T2, in T3, in T4> : ISet
    {
        void Set(T value, T2 value2, T3 value3, T4 value4);
    }
}