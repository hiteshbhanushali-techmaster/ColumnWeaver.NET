using System;
using System.Collections.Generic;
using System.Globalization;
using ColumnWeaver.NET.Core.Interfaces;

namespace ColumnWeaver.NET.Application.Execution
{
    /// <summary>
    /// V2.5 — Holds all per-parse state for a single execution of an
    /// <see cref="ExecutionPlan"/>: the source object, raw typed results, and
    /// formatted string values for every column in the plan.
    /// </summary>
    public sealed class ExecutionContext : IExecutionContext
    {
        private readonly object?[] _results;
        private readonly string?[] _formatted;
        private readonly bool[]    _skipped;
        private readonly bool[]    _evaluated;

        private int _highWaterMark = -1;
        private int _generation;

        /// <summary>
        /// Gets the source input object being processed.
        /// </summary>
        public object? Source { get; private set; }

        /// <summary>
        /// Gets the total number of column slots allocated in this context.
        /// </summary>
        public int ColumnCount { get; }

        /// <summary>
        /// Gets the array of variable values available during execution.
        /// </summary>
        public object?[] Variables { get; private set; }
        private IReadOnlyDictionary<string, int> _variableIndexes;

        internal ExecutionContext(int columnCount, object?[]? variables = null, IReadOnlyDictionary<string, int>? variableIndexes = null)
        {
            if (columnCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(columnCount), "Must be > 0.");

            ColumnCount = columnCount;
            _results    = new object?[columnCount];
            _formatted  = new string?[columnCount];
            _skipped    = new bool[columnCount];
            _evaluated  = new bool[columnCount];
            Variables   = variables ?? Array.Empty<object?>();
            _variableIndexes = variableIndexes ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Attempts to retrieve a variable value by name.
        /// </summary>
        /// <param name="name">The variable name.</param>
        /// <param name="value">When this method returns, contains the variable value if found; otherwise, <c>null</c>.</param>
        /// <returns><c>true</c> if the variable exists; otherwise, <c>false</c>.</returns>
        public bool TryGetVariable(string name, out object? value)
        {
            if (_variableIndexes.TryGetValue(name, out int index))
            {
                value = Variables[index];
                return true;
            }
            value = null;
            return false;
        }

        /// <summary>
        /// V2.5 — Resets this context to process a new source object.
        /// Clears only the columns that were actually used (up to high-water mark)
        /// to minimise array clearing overhead in large-column-count scenarios.
        /// </summary>
        public void Reset(object? source)
        {
            Source = source;
            _generation++;

            if (_highWaterMark >= 0)
            {
                Array.Clear(_results,   0, _highWaterMark + 1);
                Array.Clear(_formatted, 0, _highWaterMark + 1);
                Array.Clear(_skipped,   0, _highWaterMark + 1);
                Array.Clear(_evaluated, 0, _highWaterMark + 1);
            }
            _highWaterMark = -1;
        }

        /// <summary>
        /// Resets this context with a new source object, updated variables, and variable index map.
        /// </summary>
        /// <param name="source">The new source object.</param>
        /// <param name="variables">The updated variables array.</param>
        /// <param name="variableIndexes">The updated variable index dictionary.</param>
        public void Reset(object? source, object?[]? variables, IReadOnlyDictionary<string, int>? variableIndexes)
        {
            Reset(source);
            Variables = variables ?? Array.Empty<object?>();
            _variableIndexes = variableIndexes ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Stores the raw evaluation result and formatted string output for the specified column index.
        /// </summary>
        /// <param name="index">The zero-based column index.</param>
        /// <param name="rawValue">The raw evaluated object value.</param>
        /// <param name="formattedValue">The formatted string representation.</param>
        public void SetResult(int index, object? rawValue, string formattedValue)
        {
            if (index > _highWaterMark) _highWaterMark = index;
            _results[index]   = rawValue;
            _formatted[index] = formattedValue;
            _evaluated[index] = true;
        }

        /// <summary>
        /// Marks the column at the specified index as skipped.
        /// </summary>
        /// <param name="index">The zero-based column index.</param>
        public void MarkSkipped(int index)
        {
            if (index > _highWaterMark) _highWaterMark = index;
            _skipped[index]   = true;
            _evaluated[index] = true;
        }

        /// <summary>
        /// Retrieves the raw evaluated result for the column at the specified index.
        /// </summary>
        /// <param name="index">The zero-based column index.</param>
        /// <returns>The raw result object, or <c>null</c>.</returns>
        public object? GetResult(int index) => _results[index];

        /// <summary>
        /// Retrieves the formatted string result for the column at the specified index.
        /// </summary>
        /// <param name="index">The zero-based column index.</param>
        /// <returns>The formatted string, or <c>string.Empty</c> if not present.</returns>
        public string GetFormatted(int index) => _formatted[index] ?? string.Empty;

        /// <summary>
        /// Gets a value indicating whether the column at the specified index was skipped.
        /// </summary>
        /// <param name="index">The zero-based column index.</param>
        /// <returns><c>true</c> if skipped; otherwise, <c>false</c>.</returns>
        public bool IsSkipped(int index) => _skipped[index];

        /// <summary>
        /// Gets a value indicating whether the column at the specified index has been evaluated.
        /// </summary>
        /// <param name="index">The zero-based column index.</param>
        /// <returns><c>true</c> if evaluated; otherwise, <c>false</c>.</returns>
        public bool IsEvaluated(int index) => _evaluated[index];

        /// <summary>
        /// Formats an object value using an optional format string and the current culture.
        /// </summary>
        /// <param name="value">The object value to format.</param>
        /// <param name="formatString">The format string, or <c>null</c>.</param>
        /// <returns>The formatted string, or <c>string.Empty</c> if value is null.</returns>
        public static string FormatValue(object? value, string? formatString)
        {
            if (value == null) return string.Empty;

            if (!string.IsNullOrEmpty(formatString) && value is IFormattable formattable)
                return formattable.ToString(formatString, CultureInfo.CurrentCulture);

            return value.ToString() ?? string.Empty;
        }
    }
}
