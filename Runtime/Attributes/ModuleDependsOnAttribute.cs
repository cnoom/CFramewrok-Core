using System;

namespace CFramework.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ModuleDependsOnAttribute : Attribute
    {
        public ModuleDependsOnAttribute(params Type[] dependencies)
        {
            Dependencies = dependencies ?? Array.Empty<Type>();
        }
        public Type[] Dependencies { get; }
    }
}