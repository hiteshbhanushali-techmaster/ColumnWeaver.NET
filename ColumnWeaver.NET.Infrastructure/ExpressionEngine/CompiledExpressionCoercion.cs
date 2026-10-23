using System;

namespace ColumnWeaver.NET.Infrastructure.ExpressionEngine
{
    /// <summary>
    /// Type conversion and numeric/equality helpers for compiled LINQ expressions.
    /// Forwards to <see cref="ColumnWeaver.NET.Application.Execution.CompiledExpressionCoercion"/>.
    /// </summary>
    public static class CompiledExpressionCoercion
    {
        /// <summary>
        /// Coerces a value to a 64-bit floating-point number using invariant culture.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The double representation, or 0.0 on conversion failure.</returns>
        public static double ToDouble(object? value) => Application.Execution.CompiledExpressionCoercion.ToDouble(value);

        /// <summary>
        /// Coerces a value to a decimal number using invariant culture.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The decimal representation, or 0m on conversion failure.</returns>
        public static decimal ToDecimal(object? value) => Application.Execution.CompiledExpressionCoercion.ToDecimal(value);

        /// <summary>
        /// Coerces a value to a 32-bit signed integer using invariant culture.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The 32-bit integer representation, or 0 on conversion failure.</returns>
        public static int ToInt32(object? value) => Application.Execution.CompiledExpressionCoercion.ToInt32(value);

        /// <summary>
        /// Coerces a value to a 64-bit signed integer using invariant culture.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The 64-bit integer representation, or 0L on conversion failure.</returns>
        public static long ToInt64(object? value) => Application.Execution.CompiledExpressionCoercion.ToInt64(value);

        /// <summary>
        /// Evaluates the truthiness of a value, converting booleans, non-zero numbers, and non-empty strings to true.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><c>true</c> if truthy; otherwise, <c>false</c>.</returns>
        public static bool ToBoolean(object? value) => Application.Execution.CompiledExpressionCoercion.ToBoolean(value);

        /// <summary>
        /// Converts a value to its string representation using invariant culture, returning an empty string for null.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>A string representation of the value, or <c>string.Empty</c> if null.</returns>
        public static string ToString(object? value) => Application.Execution.CompiledExpressionCoercion.ToString(value);

        /// <summary>
        /// Determines whether the specified type is a supported numeric primitive or nullable numeric type.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns><c>true</c> if numeric; otherwise, <c>false</c>.</returns>
        public static bool IsNumericType(Type type) => Application.Execution.CompiledExpressionCoercion.IsNumericType(type);

        /// <summary>
        /// Determines the common widening numeric type between two operand types.
        /// </summary>
        /// <param name="left">The left operand type.</param>
        /// <param name="right">The right operand type.</param>
        /// <returns>The common numeric promotion type.</returns>
        public static Type GetCommonNumericType(Type left, Type right) => Application.Execution.CompiledExpressionCoercion.GetCommonNumericType(left, right);

        /// <summary>
        /// Performs addition or string concatenation on two operand values.
        /// </summary>
        /// <param name="left">The left operand value.</param>
        /// <param name="right">The right operand value.</param>
        /// <returns>The sum or concatenated string result.</returns>
        public static object? Add(object? left, object? right) => Application.Execution.CompiledExpressionCoercion.Add(left, right);

        /// <summary>
        /// Performs subtraction on two numeric operand values.
        /// </summary>
        /// <param name="left">The left operand value.</param>
        /// <param name="right">The right operand value.</param>
        /// <returns>The difference result.</returns>
        public static object? Subtract(object? left, object? right) => Application.Execution.CompiledExpressionCoercion.Subtract(left, right);

        /// <summary>
        /// Performs multiplication on two numeric operand values.
        /// </summary>
        /// <param name="left">The left operand value.</param>
        /// <param name="right">The right operand value.</param>
        /// <returns>The product result.</returns>
        public static object? Multiply(object? left, object? right) => Application.Execution.CompiledExpressionCoercion.Multiply(left, right);

        /// <summary>
        /// Performs division on two numeric operand values, returning 0 if the divisor is 0.
        /// </summary>
        /// <param name="left">The dividend value.</param>
        /// <param name="right">The divisor value.</param>
        /// <returns>The quotient result, or 0 if dividing by zero.</returns>
        public static object? Divide(object? left, object? right) => Application.Execution.CompiledExpressionCoercion.Divide(left, right);

        /// <summary>
        /// Performs modulo on two numeric operand values, returning 0 if the divisor is 0.
        /// </summary>
        /// <param name="left">The dividend value.</param>
        /// <param name="right">The divisor value.</param>
        /// <returns>The remainder result, or 0 if dividing by zero.</returns>
        public static object? Modulo(object? left, object? right) => Application.Execution.CompiledExpressionCoercion.Modulo(left, right);

        /// <summary>
        /// Determines equality between two operand values using numeric coercion or case-insensitive string comparison where applicable.
        /// </summary>
        /// <param name="left">The first operand value.</param>
        /// <param name="right">The second operand value.</param>
        /// <returns><c>true</c> if the operands are considered equal; otherwise, <c>false</c>.</returns>
        public static bool AreEqual(object? left, object? right) => Application.Execution.CompiledExpressionCoercion.AreEqual(left, right);

        /// <summary>
        /// Compares two operand values for relative ordering.
        /// </summary>
        /// <param name="left">The first operand value.</param>
        /// <param name="right">The second operand value.</param>
        /// <returns>A negative integer if <paramref name="left"/> is less than <paramref name="right"/>, zero if equal, or a positive integer if greater.</returns>
        public static int Compare(object? left, object? right) => Application.Execution.CompiledExpressionCoercion.Compare(left, right);
    }
}
