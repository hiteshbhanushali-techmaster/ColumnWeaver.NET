using System;
using System.Globalization;

namespace ColumnWeaver.NET.Application.Execution
{
    /// <summary>
    /// Type conversion and numeric/equality helpers for compiled LINQ expressions.
    /// </summary>
    public static class CompiledExpressionCoercion
    {
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

        public static string ToString(object? value)
        {
            if (value == null) return string.Empty;
            if (value is string s) return s;
            if (value is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
            return value.ToString() ?? string.Empty;
        }

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

        public static object? Add(object? left, object? right)
        {
            if (left is string || right is string)
                return ToString(left) + ToString(right);

            if (left is decimal || right is decimal)
                return ToDecimal(left) + ToDecimal(right);

            return ToDouble(left) + ToDouble(right);
        }

        public static object? Subtract(object? left, object? right)
        {
            if (left is decimal || right is decimal)
                return ToDecimal(left) - ToDecimal(right);

            return ToDouble(left) - ToDouble(right);
        }

        public static object? Multiply(object? left, object? right)
        {
            if (left is decimal || right is decimal)
                return ToDecimal(left) * ToDecimal(right);

            return ToDouble(left) * ToDouble(right);
        }

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

        public static object? Modulo(object? left, object? right)
        {
            var rDbl = ToDouble(right);
            return rDbl == 0.0 ? 0.0 : ToDouble(left) % rDbl;
        }

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
