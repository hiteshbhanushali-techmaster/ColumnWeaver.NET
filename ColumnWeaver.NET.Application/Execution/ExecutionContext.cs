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

        public object? Source { get; private set; }
        public int ColumnCount { get; }
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

        public void Reset(object? source, object?[]? variables, IReadOnlyDictionary<string, int>? variableIndexes)
        {
            Reset(source);
            Variables = variables ?? Array.Empty<object?>();
            _variableIndexes = variableIndexes ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        public void SetResult(int index, object? rawValue, string formattedValue)
        {
            if (index > _highWaterMark) _highWaterMark = index;
            _results[index]   = rawValue;
            _formatted[index] = formattedValue;
            _evaluated[index] = true;
        }

        public void MarkSkipped(int index)
        {
            if (index > _highWaterMark) _highWaterMark = index;
            _skipped[index]   = true;
            _evaluated[index] = true;
        }

        public object? GetResult(int index) => _results[index];
        public string GetFormatted(int index) => _formatted[index] ?? string.Empty;
        public bool IsSkipped(int index) => _skipped[index];
        public bool IsEvaluated(int index) => _evaluated[index];

        public static string FormatValue(object? value, string? formatString)
        {
            if (value == null) return string.Empty;

            if (!string.IsNullOrEmpty(formatString) && value is IFormattable formattable)
                return formattable.ToString(formatString, CultureInfo.CurrentCulture);

            return value.ToString() ?? string.Empty;
        }
    }
}
