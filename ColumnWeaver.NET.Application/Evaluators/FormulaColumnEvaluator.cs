using System;
using System.Globalization;
using ColumnWeaver.NET.Application.Exceptions;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Interfaces;

namespace ColumnWeaver.NET.Application.Evaluators
{
    /// <summary>
    /// Evaluates a <see cref="FormulaColumn"/> by passing its expression string
    /// to the configured <see cref="IExpressionEngine"/>.
    /// </summary>
    /// <remarks>
    /// <b>Cross-column references:</b> All <c>${ColumnName}</c> tokens in the expression
    /// are expanded by <see cref="Pipeline.ColumnPipeline"/> <i>before</i> this evaluator
    /// is called. This evaluator always receives a fully-resolved expression string.
    /// <para>
    /// <b>FormatString:</b> <see cref="Core.Models.FormattingOptions.FormatString"/> is
    /// applied here via <see cref="IExpressionEngine.EvaluateRaw"/> while the result is
    /// still a typed CLR value (double, DateTime, etc.). This is consistent with
    /// <see cref="PropertyColumnEvaluator"/>, which applies FormatString the same way.
    /// </para>
    /// </remarks>
    public sealed class FormulaColumnEvaluator : IColumnEvaluator
    {
        private readonly IExpressionEngine _expressionEngine;

        /// <summary>
        /// Initializes the evaluator with the expression engine to use at runtime.
        /// </summary>
        public FormulaColumnEvaluator(IExpressionEngine expressionEngine)
        {
            _expressionEngine = expressionEngine
                ?? throw new ArgumentNullException(nameof(expressionEngine));
        }

        /// <inheritdoc />
        public bool CanEvaluate(ColumnDefinition column) => column is FormulaColumn;

        /// <inheritdoc />
        /// <exception cref="ColumnEvaluationException">
        /// Thrown when the expression is missing or the expression engine fails.
        /// </exception>
        public string Evaluate(ColumnDefinition column, IExecutionContext context)
        {
            if (column is not FormulaColumn formulaCol)
                throw new ArgumentException(
                    $"Expected {nameof(FormulaColumn)}, received '{column?.GetType().Name}'.",
                    nameof(column));

            if (string.IsNullOrWhiteSpace(formulaCol.Expression))
                throw new ColumnEvaluationException(
                    column.Id, column.Name,
                    "FormulaColumn.Expression cannot be null or empty.");

            try
            {
                // EvaluateRaw keeps the result as a typed CLR value (double, DateTime, etc.)
                // so FormatString can be applied accurately before string conversion.
                var rawResult    = _expressionEngine.EvaluateRaw(formulaCol.Expression, context);
                var formatString = column.Formatting?.FormatString;

                if (!string.IsNullOrEmpty(formatString) && rawResult is IFormattable formattable)
                    return formattable.ToString(formatString, CultureInfo.CurrentCulture);

                return rawResult?.ToString() ?? string.Empty;
            }
            catch (Exception ex) when (ex is not ColumnEvaluationException)
            {
                throw new ColumnEvaluationException(
                    column.Id, column.Name,
                    $"Expression engine failed to evaluate: '{formulaCol.Expression}'. {ex.Message}",
                    ex);
            }
        }

    }
}
