using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace ColumnWeaver.NET.Application.Execution
{
    /// <summary>
    /// Compiles typed property getters using LINQ Expression trees to eliminate
    /// runtime reflection on the execution hot path.
    /// </summary>
    public static class PropertyAccessorBuilder
    {
        private static readonly ConcurrentDictionary<(Type, string), Func<object, object?>?> _cache
            = new ConcurrentDictionary<(Type, string), Func<object, object?>?>();

        /// <summary>
        /// Builds a strongly-typed LINQ <see cref="Expression"/> for accessing a simple or nested property path on a source expression.
        /// </summary>
        /// <param name="sourceType">The CLR type of the source object.</param>
        /// <param name="path">The dot-delimited property path (e.g. "Customer.Address.City").</param>
        /// <param name="sourceExpr">The parameter or expression representing the source object.</param>
        /// <returns>A LINQ expression tree accessing the target property with null-propagation, or <c>null</c> if resolution fails.</returns>
        public static Expression? BuildPropertyExpression(Type sourceType, string path, Expression sourceExpr)
        {
            if (sourceType == null || string.IsNullOrWhiteSpace(path) || sourceExpr == null) return null;

            try
            {
                var segments = path.Split('.');
                Expression current = Expression.Convert(sourceExpr, sourceType);
                Type currentType = sourceType;

                if (segments.Length == 1)
                {
                    var prop = currentType.GetProperty(segments[0].Trim(),
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop == null || !prop.CanRead) return null;

                    return Expression.Property(current, prop);
                }

                Expression condition = Expression.Constant(true);
                Expression currentAccess = current;

                for (int i = 0; i < segments.Length; i++)
                {
                    var seg = segments[i].Trim();
                    if (string.IsNullOrEmpty(seg)) return null;

                    var prop = currentType.GetProperty(seg,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop == null || !prop.CanRead) return null;

                    if (i < segments.Length - 1)
                    {
                        currentAccess = Expression.Property(currentAccess, prop);
                        if (!prop.PropertyType.IsValueType)
                        {
                            var notNull = Expression.NotEqual(currentAccess, Expression.Constant(null, prop.PropertyType));
                            condition = condition is ConstantExpression c && (bool)c.Value!
                                ? notNull
                                : Expression.AndAlso(condition, notNull);
                        }
                        currentType = prop.PropertyType;
                    }
                    else
                    {
                        currentAccess = Expression.Property(currentAccess, prop);
                    }
                }

                if (condition is ConstantExpression ce && (bool)ce.Value!)
                {
                    return currentAccess;
                }

                Type targetType = currentAccess.Type;
                if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                {
                    targetType = typeof(Nullable<>).MakeGenericType(targetType);
                    currentAccess = Expression.Convert(currentAccess, targetType);
                }

                return Expression.Condition(condition, currentAccess, Expression.Default(targetType));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Compiles and caches a property getter delegate for the specified source type and property path.
        /// </summary>
        /// <param name="sourceType">The CLR type of the source object.</param>
        /// <param name="path">The dot-delimited property path.</param>
        /// <returns>A compiled getter delegate, or <c>null</c> if resolution fails.</returns>
        public static Func<object, object?>? BuildGetter(Type sourceType, string path)
        {
            if (sourceType == null || string.IsNullOrWhiteSpace(path)) return null;

            return _cache.GetOrAdd((sourceType, path), key => CreateGetter(key.Item1, key.Item2));
        }

        private static Func<object, object?>? CreateGetter(Type sourceType, string path)
        {
            try
            {
                var segments = path.Split('.');
                var param = Expression.Parameter(typeof(object), "src");
                Expression current = Expression.Convert(param, sourceType);
                Type currentType = sourceType;

                if (segments.Length == 1)
                {
                    var prop = currentType.GetProperty(segments[0].Trim(),
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop == null || !prop.CanRead) return null;

                    var access = Expression.Property(current, prop);
                    var boxed = Expression.Convert(access, typeof(object));
                    return Expression.Lambda<Func<object, object?>>(boxed, param).Compile();
                }

                // Nested property path with null safety (e.g. Customer.Name)
                Expression condition = Expression.Constant(true);
                Expression currentAccess = current;

                for (int i = 0; i < segments.Length; i++)
                {
                    var seg = segments[i].Trim();
                    if (string.IsNullOrEmpty(seg)) return null;

                    var prop = currentType.GetProperty(seg,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop == null || !prop.CanRead) return null;

                    if (i < segments.Length - 1)
                    {
                        currentAccess = Expression.Property(currentAccess, prop);
                        if (!prop.PropertyType.IsValueType)
                        {
                            var notNull = Expression.NotEqual(currentAccess, Expression.Constant(null, prop.PropertyType));
                            condition = condition is ConstantExpression c && (bool)c.Value!
                                ? notNull
                                : Expression.AndAlso(condition, notNull);
                        }
                        currentType = prop.PropertyType;
                    }
                    else
                    {
                        currentAccess = Expression.Property(currentAccess, prop);
                    }
                }

                var finalBoxed = Expression.Convert(currentAccess, typeof(object));
                Expression body;
                if (condition is ConstantExpression ce && (bool)ce.Value!)
                {
                    body = finalBoxed;
                }
                else
                {
                    body = Expression.Condition(condition, finalBoxed, Expression.Constant(null, typeof(object)));
                }

                return Expression.Lambda<Func<object, object?>>(body, param).Compile();
            }
            catch
            {
                return null;
            }
        }
    }
}
