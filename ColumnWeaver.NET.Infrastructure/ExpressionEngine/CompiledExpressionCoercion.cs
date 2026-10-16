using System;

namespace ColumnWeaver.NET.Infrastructure.ExpressionEngine
{
    /// <summary>
    /// Type conversion and numeric/equality helpers for compiled LINQ expressions.
    /// Forwards to <see cref="ColumnWeaver.NET.Application.Execution.CompiledExpressionCoercion"/>.
    /// </summary>
    public static class CompiledExpressionCoercion
    {
        public static double ToDouble(object? value) => Application.Execution.CompiledExpressionCoercion.ToDouble(value);
        public static decimal ToDecimal(object? value) => Application.Execution.CompiledExpressionCoercion.ToDecimal(value);
        public static int ToInt32(object? value) => Application.Execution.CompiledExpressionCoercion.ToInt32(value);
        public static long ToInt64(object? value) => Application.Execution.CompiledExpressionCoercion.ToInt64(value);
        public static bool ToBoolean(object? value) => Application.Execution.CompiledExpressionCoercion.ToBoolean(value);
        public static string ToString(object? value) => Application.Execution.CompiledExpressionCoercion.ToString(value);
        public static bool IsNumericType(Type type) => Application.Execution.CompiledExpressionCoercion.IsNumericType(type);
        public static Type GetCommonNumericType(Type left, Type right) => Application.Execution.CompiledExpressionCoercion.GetCommonNumericType(left, right);
        public static object? Add(object? left, object? right) => Application.Execution.CompiledExpressionCoercion.Add(left, right);
        public static object? Subtract(object? left, object? right) => Application.Execution.CompiledExpressionCoercion.Subtract(left, right);
        public static object? Multiply(object? left, object? right) => Application.Execution.CompiledExpressionCoercion.Multiply(left, right);
        public static object? Divide(object? left, object? right) => Application.Execution.CompiledExpressionCoercion.Divide(left, right);
        public static object? Modulo(object? left, object? right) => Application.Execution.CompiledExpressionCoercion.Modulo(left, right);
        public static bool AreEqual(object? left, object? right) => Application.Execution.CompiledExpressionCoercion.AreEqual(left, right);
        public static int Compare(object? left, object? right) => Application.Execution.CompiledExpressionCoercion.Compare(left, right);
    }
}
