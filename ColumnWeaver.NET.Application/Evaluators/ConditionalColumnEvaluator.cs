using System;
using ColumnWeaver.NET.Application.Exceptions;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Interfaces;

namespace ColumnWeaver.NET.Application.Evaluators
{
    /// <summary>
    /// Evaluates a <see cref="ConditionalColumn"/> by evaluating its boolean condition
    /// and returning the appropriate branch value.
    /// </summary>
    /// <remarks>
    /// <b>Cross-column references:</b> All <c>${ColumnName}</c> tokens in
    /// <see cref="ConditionalColumn.ConditionExpression"/>, <see cref="ConditionalColumn.TrueValue"/>,
    /// and <see cref="ConditionalColumn.FalseValue"/> are expanded by
    /// <see cref="Pipeline.ColumnPipeline"/> before this evaluator is invoked.
    /// <para>
    /// <b>Branch value convention:</b>
    /// <list type="bullet">
    ///   <item>Literal values are returned as-is.</item>
    ///   <item>
    ///     Branch values prefixed with <c>=</c> are treated as dynamic expressions
    ///     and evaluated via <see cref="IExpressionEngine.Evaluate"/>.
    ///     Example: <c>TrueValue = "= Price * 0.9"</c>
    ///   </item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class ConditionalColumnEvaluator : IColumnEvaluator
    {
        private const string ExpressionPrefix = "=";

        private readonly IExpressionEngine _expressionEngine;

        /// <summary>
        /// Initializes the evaluator with the expression engine to use at runtime.
        /// </summary>
        public ConditionalColumnEvaluator(IExpressionEngine expressionEngine)
        {
            _expressionEngine = expressionEngine
                ?? throw new ArgumentNullException(nameof(expressionEngine));
        }

        /// <inheritdoc />
        public bool CanEvaluate(ColumnDefinition column) => column is ConditionalColumn;

        /// <inheritdoc />
        /// <exception cref="ColumnEvaluationException">
        /// Thrown when the condition expression is null/empty or the engine fails.
        /// </exception>
        public string Evaluate(ColumnDefinition column, IExecutionContext context)
        {
            if (column is not ConditionalColumn condCol)
                throw new ArgumentException(
                    $"Expected {nameof(ConditionalColumn)}, received '{column?.GetType().Name}'.",
                    nameof(column));

            if (string.IsNullOrWhiteSpace(condCol.ConditionExpression))
                throw new ColumnEvaluationException(
                    column.Id, column.Name,
                    "ConditionalColumn.ConditionExpression cannot be null or empty.");

            try
            {
                var conditionResult = _expressionEngine.EvaluateCondition(
                    condCol.ConditionExpression, context);

                var branchValue = conditionResult
                    ? condCol.TrueValue
                    : condCol.FalseValue;

                return ResolveBranchValue(branchValue, context, column);
            }
            catch (Exception ex) when (ex is not ColumnEvaluationException)
            {
                throw new ColumnEvaluationException(
                    column.Id, column.Name,
                    $"Conditional evaluation failed for condition '{condCol.ConditionExpression}': {ex.Message}",
                    ex);
            }
        }

        // -----------------------------------------------------------------
        //  Branch value resolution
        // -----------------------------------------------------------------

        /// <summary>
        /// Resolves a branch value: evaluates it as an expression when prefixed
        /// with <c>=</c>, otherwise returns it as a literal string.
        /// </summary>
        private string ResolveBranchValue(string? branchValue, IExecutionContext context, ColumnDefinition column)
        {
            if (string.IsNullOrEmpty(branchValue))
                return string.Empty;

            if (branchValue!.StartsWith(ExpressionPrefix, StringComparison.Ordinal))
            {
                var innerExpression = branchValue.Substring(ExpressionPrefix.Length).Trim();

                try
                {
                    return _expressionEngine.Evaluate(innerExpression, context);
                }
                catch (Exception ex) when (ex is not ColumnEvaluationException)
                {
                    throw new ColumnEvaluationException(
                        column.Id, column.Name,
                        $"Branch expression evaluation failed: '{innerExpression}'. {ex.Message}",
                        ex);
                }
            }

            return branchValue;
        }

    }
}
