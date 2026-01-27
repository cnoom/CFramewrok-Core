using System;
using System.Linq;
using System.Reflection;

namespace CFramework.Core
{
    public static class ReflectUtil
    {
        public static Type GetActionType(MethodInfo method)
        {
            var parameters = method.GetParameters();
            Type[] typeArgs = parameters.Select(p => p.ParameterType).ToArray();

            switch (typeArgs.Length)
            {
                case 0: return typeof(Action);
                case 1: return typeof(Action<>).MakeGenericType(typeArgs);
                case 2: return typeof(Action<,>).MakeGenericType(typeArgs);
                case 3: return typeof(Action<,,>).MakeGenericType(typeArgs);
                case 4: return typeof(Action<,,,>).MakeGenericType(typeArgs);
                case 5: return typeof(Action<,,,,>).MakeGenericType(typeArgs);
                case 6: return typeof(Action<,,,,,>).MakeGenericType(typeArgs);
                case 7: return typeof(Action<,,,,,,>).MakeGenericType(typeArgs);
                case 8: return typeof(Action<,,,,,,,>).MakeGenericType(typeArgs);
                default: throw new NotSupportedException("参数过多，不支持创建 Action 委托");
            }
        }

        public static Type GetFuncType(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var returnType = method.ReturnType;

            // 注意：Func 必须包含返回类型，且返回类型不能是 void
            if (returnType == typeof(void))
                throw new InvalidOperationException($"方法 {method.Name} 返回 void，无法用于 Func 委托。");

            // 组合：所有参数类型 + 返回类型（最后一个）
            Type[] typeArgs = parameters.Select(p => p.ParameterType)
                                        .Concat(new[] { returnType })
                                        .ToArray();

            switch (typeArgs.Length)
            {
                case 1: return typeof(Func<>).MakeGenericType(typeArgs);                    // () => TResult
                case 2: return typeof(Func<,>).MakeGenericType(typeArgs);                  // (T1) => TResult
                case 3: return typeof(Func<,,>).MakeGenericType(typeArgs);                 // (T1,T2) => TResult
                case 4: return typeof(Func<,,,>).MakeGenericType(typeArgs);                // (T1,T2,T3) => TResult
                case 5: return typeof(Func<,,,,>).MakeGenericType(typeArgs);
                case 6: return typeof(Func<,,,,,>).MakeGenericType(typeArgs);
                case 7: return typeof(Func<,,,,,,>).MakeGenericType(typeArgs);
                case 8: return typeof(Func<,,,,,,,>).MakeGenericType(typeArgs);
                case 9: return typeof(Func<,,,,,,,,>).MakeGenericType(typeArgs);
                default: throw new NotSupportedException("参数过多，不支持创建 Func 委托");
            }
        }
        
        public static MethodInfo[] GetAllInstanceMethods(Type type)
        {
            var methods = new System.Collections.Generic.List<MethodInfo>();
            var current = type;
            while (current != null && current != typeof(object))
            {
                var declared = current.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                methods.AddRange(declared);
                current = current.BaseType;
            }
            return methods.ToArray();
        }
    }
}
