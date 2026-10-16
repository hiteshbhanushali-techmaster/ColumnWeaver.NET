using System;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Interfaces;

namespace ColumnWeaver.NET.Application.Evaluators
{
    /// <summary>
    /// Evaluates a <see cref="FixedTextColumn"/> by returning its configured
    /// static <see cref="FixedTextColumn.Text"/> value unchanged.
    /// </summary>
    /// <remarks>
    /// This evaluator is source-agnostic — it ignores the source object entirely.
    /// The returned value is never null; an empty string is returned when
    /// <see cref="FixedTextColumn.Text"/> is null or unset.
    /// </remarks>
    public sealed class FixedTextColumnEvaluator : IColumnEvaluator
    {
        /// <inheritdoc />
        public bool CanEvaluate(ColumnDefinition column) => column is FixedTextColumn;

        /// <inheritdoc />
        public string Evaluate(ColumnDefinition column, IExecutionContext context)
        {
            if (column is not FixedTextColumn fixedCol)
                throw new ArgumentException(
                    $"Expected {nameof(FixedTextColumn)}, received '{column?.GetType().Name}'.",
                    nameof(column));

            return fixedCol.Text ?? string.Empty;
        }
    }
}
