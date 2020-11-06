using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal static class EnumerableMethods
    {
        /// <summary>
        /// Enumerable.Average<TSource>(IEnumerable<TSource>, Func<TSource, {aggregateType}>)
        /// </summary>
        public static MethodInfo AverageWithSelector(Type aggregateType) =>
            GetMethod("Average", 1, args => GetEnumerableAndSelectorParameterTypes(args[0], aggregateType));
        public static MethodInfo Count() =>
            GetMethod("Count", 1, args => GetEnumerableParameterTypes(args[0]));
        public static MethodInfo CountWithFilter() =>
            GetMethod("Count", 1, args => GetEnumerableAndFilterParameterTypes(args[0]));
        public static MethodInfo MaxWithSelector() =>
            GetMethod("Max", 2, args => GetEnumerableAndSelectorParameterTypes(args[0], args[1]));
        public static MethodInfo MaxWithSelector(Type aggregateType) =>
            GetMethod("Max", 1, args => GetEnumerableAndSelectorParameterTypes(args[0], aggregateType));
        public static MethodInfo MinWithSelector() =>
            GetMethod("Min", 2, args => GetEnumerableAndSelectorParameterTypes(args[0], args[1]));
        public static MethodInfo MinWithSelector(Type aggregateType) =>
            GetMethod("Min", 1, args => GetEnumerableAndSelectorParameterTypes(args[0], aggregateType));
        public static MethodInfo SumWithSelector(Type aggregateType) =>
            GetMethod("Sum", 1, args => GetEnumerableAndSelectorParameterTypes(args[0], aggregateType));
        public static MethodInfo Where() =>
            GetMethod("Where", 1, args => GetEnumerableAndFilterParameterTypes(args[0]));

        private static Type[] GetEnumerableParameterTypes(Type sourceType)
        {
            return new Type[]
            {
                typeof(IEnumerable<>).MakeGenericType(sourceType)
            };
        }

        private static Type[] GetEnumerableAndFilterParameterTypes(Type sourceType)
        {
            return new Type[]
            {
                typeof(IEnumerable<>).MakeGenericType(sourceType),
                typeof(Func<,>).MakeGenericType(sourceType, typeof(bool))
            };
        }

        private static Type[] GetEnumerableAndSelectorParameterTypes(Type sourceType, Type aggregateType)
        {
            return new[]
            {
                typeof(IEnumerable<>).MakeGenericType(sourceType),
                typeof(Func<,>).MakeGenericType(sourceType, aggregateType)
            };
        }

        private static MethodInfo GetMethod(string name, int genericArgumentCount, Func<Type[], Type[]> parametersFunc)
        {
            foreach (MethodInfo methodInfo in typeof(Enumerable).GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                if (name != methodInfo.Name)
                {
                    continue;
                }

                Type[] genericArgumentTypes = methodInfo.GetGenericArguments();
                if (genericArgumentCount != genericArgumentTypes.Length)
                {
                    continue;
                }

                Type[] parameterTypes = parametersFunc(genericArgumentTypes);

                ParameterInfo[] parameterInfos = methodInfo.GetParameters();
                if (parameterInfos.Length != parameterTypes.Length)
                {
                    continue;
                }

                if (parameterTypes.SequenceEqual(parameterInfos.Select(p => p.ParameterType)))
                {
                    return methodInfo;
                }
            }
            return null;
        }
    }
}
