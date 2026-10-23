using System;
using System.Globalization;

namespace ColumnWeaver.NET.Application.Execution
{
    /// <summary>
    /// Type conversion and numeric/equality helpers for compiled LINQ expressions.
    /// </summary>
    public static class CompiledExpressionCoercion
    {
        /// <summary>
        /// Coerces a value to a 64-bit floating-point number using invariant culture, returning 0.0 on conversion failure.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The double representation, or 0.0 if null or unparseable.</returns>
        public static double ToDouble(object? value)
        {
            if (value == null) return 0.0;
            if (value is double d) return d;
            if (value is int i) return i;
            if (value is decimal dec) return (double)dec;
            if (value is float f) return f;
            if (value is long l) return l;
            if (value is short s) return s;
            if (value is byte b) return b;
            try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch { return 0.0; }
        }

        /// <summary>
        /// Coerces a value to a decimal number using invariant culture, returning 0m on conversion failure.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The decimal representation, or 0m if null or unparseable.</returns>
        public static decimal ToDecimal(object? value)
        {
            if (value == null) return 0m;
            if (value is decimal dec) return dec;
            if (value is int i) return i;
            if (value is double d) return (decimal)d;
            if (value is float f) return (decimal)f;
            if (value is long l) return l;
            if (value is short s) return s;
            if (value is byte b) return b;
            try { return Convert.ToDecimal(value, CultureInfo.InvariantCulture); }
            catch { return 0m; }
        }

        /// <summary>
        /// Coerces a value to a 32-bit signed integer using invariant culture, returning 0 on conversion failure.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The 32-bit integer representation, or 0 if null or unparseable.</returns>
        public static int ToInt32(object? value)
        {
            if (value == null) return 0;
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is double d) return (int)d;
            if (value is decimal dec) return (int)dec;
            if (value is float f) return (int)f;
            if (value is short s) return s;
            if (value is byte b) return b;
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        /// <summary>
        /// Coerces a value to a 64-bit signed integer using invariant culture, returning 0L on conversion failure.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The 64-bit integer representation, or 0L if null or unparseable.</returns>
        public static long ToInt64(object? value)
        {
            if (value == null) return 0L;
            if (value is long l) return l;
            if (value is int i) return i;
            if (value is double d) return (long)d;
            if (value is decimal dec) return (long)dec;
            if (value is float f) return (long)f;
            if (value is short s) return s;
            if (value is byte b) return b;
            try { return Convert.ToInt64(value, CultureInfo.InvariantCulture); }
            catch { return 0L; }
        }

        /// <summary>
        /// Evaluates the truthiness of a value, converting booleans, non-zero numbers, and non-empty strings to true.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><c>true</c> if the value is truthy; otherwise, <c>false</c>.</returns>
        public static bool ToBoolean(object? value)
        {
            if (value == null) return false;
            if (value is bool b) return b;
            if (value is int i) return i != 0;
            if (value is long l) return l != 0;
            if (value is double d) return d != 0.0;
            if (value is decimal dec) return dec != 0m;
            if (value is string s)
            {
                if (bool.TryParse(s, out var parsed)) return parsed;
                if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var num)) return num != 0.0;
                return !string.IsNullOrEmpty(s);
            }
            try { return Convert.ToBoolean(value, CultureInfo.InvariantCulture); }
            catch { return false; }
        }

        /// <summary>
        /// Converts a value to its string representation using invariant culture, returning an empty string for null.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>A string representation of the value, or <c>string.Empty</c> if null.</returns>
        public static string ToString(object? value)
        {
            if (value == null) return string.Empty;
            if (value is string s) return s;
            if (value is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
            return value.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Determines whether the specified type is a supported numeric primitive or nullable numeric type.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns><c>true</c> if the type is numeric; otherwise, <c>false</c>.</returns>
        public static bool IsNumericType(Type type)
        {
            if (type == null) return false;
            var nnType = Nullable.GetUnderlyingType(type) ?? type;
            return nnType == typeof(int) ||
                   nnType == typeof(double) ||
                   nnType == typeof(decimal) ||
                   nnType == typeof(long) ||
                   nnType == typeof(float) ||
                   nnType == typeof(short) ||
                   nnType == typeof(byte);
        }

        /// <summary>
        /// Determines the common widening numeric type between two operand types.
        /// </summary>
        /// <param name="left">The left operand type.</param>
        /// <param name="right">The right operand type.</param>
        /// <returns>The common numeric promotion type.</returns>
        public static Type GetCommonNumericType(Type left, Type right)
        {
            var l = Nullable.GetUnderlyingType(left) ?? left;
            var r = Nullable.GetUnderlyingType(right) ?? right;

            if (l == typeof(double) || r == typeof(double)) return typeof(double);
            if (l == typeof(decimal) || r == typeof(decimal))
            {
                if (l == typeof(double) || r == typeof(double) || l == typeof(float) || r == typeof(float))
                    return typeof(double);
                return typeof(decimal);
            }
            if (l == typeof(float) || r == typeof(float)) return typeof(float);
            if (l == typeof(long) || r == typeof(long)) return typeof(long);
            return typeof(int);
        }

        /// <summary>
        /// Performs addition or string concatenation on two operand values.
        /// </summary>
        /// <param name="left">The left operand value.</param>
        /// <param name="right">The right operand value.</param>
        /// <returns>The sum or concatenated string result.</returns>
        public static object? Add(object? left, object? right)
        {
            if (left is string || right is string)
                return ToString(left) + ToString(right);

            if (left is decimal || right is decimal)
                return ToDecimal(left) + ToDecimal(right);

            return ToDouble(left) + ToDouble(right);
        }

        /// <summary>
        /// Performs subtraction on two numeric operand values.
        /// </summary>
        /// <param name="left">The left operand value.</param>
        /// <param name="right">The right operand value.</param>
        /// <returns>The difference result.</returns>
        public static object? Subtract(object? left, object? right)
        {
            if (left is decimal || right is decimal)
                return ToDecimal(left) - ToDecimal(right);

            return ToDouble(left) - ToDouble(right);
        }

        /// <summary>
        /// Performs multiplication on two numeric operand values.
        /// </summary>
        /// <param name="left">The left operand value.</param>
        /// <param name="right">The right operand value.</param>
        /// <returns>The product result.</returns>
        public static object? Multiply(object? left, object? right)
        {
            if (left is decimal || right is decimal)
                return ToDecimal(left) * ToDecimal(right);

            return ToDouble(left) * ToDouble(right);
        }

        /// <summary>
        /// Performs division on two numeric operand values, returning 0 if the divisor is 0.
        /// </summary>
        /// <param name="left">The dividend value.</param>
        /// <param name="right">The divisor value.</param>
        /// <returns>The quotient result, or 0 if dividing by zero.</returns>
        public static object? Divide(object? left, object? right)
        {
            if (left is decimal || right is decimal)
            {
                var rDec = ToDecimal(right);
                return rDec == 0m ? 0m : ToDecimal(left) / rDec;
            }

            var rDbl = ToDouble(right);
            return rDbl == 0.0 ? 0.0 : ToDouble(left) / rDbl;
        }

        /// <summary>
        /// Performs modulo on two numeric operand values, returning 0 if the divisor is 0.
        /// </summary>
        /// <param name="left">The dividend value.</param>
        /// <param name="right">The divisor value.</param>
        /// <returns>The remainder result, or 0 if dividing by zero.</returns>
        public static object? Modulo(object? left, object? right)
        {
            var rDbl = ToDouble(right);
            return rDbl == 0.0 ? 0.0 : ToDouble(left) % rDbl;
        }

        /// <summary>
        /// Determines equality between two operand values using numeric coercion or case-insensitive string comparison where applicable.
        /// </summary>
        /// <param name="left">The first operand value.</param>
        /// <param name="right">The second operand value.</param>
        /// <returns><c>true</c> if the operands are considered equal; otherwise, <c>false</c>.</returns>
        public static bool AreEqual(object? left, object? right)
        {
            if (left == null && right == null) return true;
            if (left == null || right == null) return false;

            if (IsNumericType(left.GetType()) && IsNumericType(right.GetType()))
                return ToDouble(left).Equals(ToDouble(right));

            if (left is string s1 && right is string s2)
                return string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);

            if (left is bool b1 && right is bool b2)
                return b1 == b2;

            return Equals(left, right);
        }

        /// <summary>
        /// Compares two operand values for relative ordering.
        /// </summary>
        /// <param name="left">The first operand value.</param>
        /// <param name="right">The second operand value.</param>
        /// <returns>A negative integer if <paramref name="left"/> is less than <paramref name="right"/>, zero if equal, or a positive integer if greater.</returns>
        public static int Compare(object? left, object? right)
        {
            if (left == null && right == null) return 0;
            if (left == null) return -1;
            if (right == null) return 1;

            if (IsNumericType(left.GetType()) && IsNumericType(right.GetType()))
                return ToDouble(left).CompareTo(ToDouble(right));

            if (left is string s1 && right is string s2)
                return string.Compare(s1, s2, StringComparison.OrdinalIgnoreCase);

            if (left is IComparable comp)
            {
                try { return comp.CompareTo(right); }
                catch { }
            }

            return 0;
        }
    }
}
