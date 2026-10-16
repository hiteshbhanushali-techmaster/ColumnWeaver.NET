using System;
using System.Collections.Generic;
using ColumnWeaver.NET.Application.Abstractions;
using ColumnWeaver.NET.Application.Exceptions;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Interfaces;

namespace ColumnWeaver.NET.Application.Evaluators
{
    /// <summary>
    /// Evaluates a <see cref="CustomFunctionColumn"/> by invoking a named function
    /// from the <see cref="ICustomFunctionRegistry"/>.
    /// </summary>
    /// <remarks>
    /// <b>Cross-column references in parameters:</b> All <c>${ColumnName}</c> tokens
    /// in <see cref="CustomFunctionColumn.Parameters"/> are expanded by
    /// <see cref="Pipeline.ColumnPipeline"/> before this evaluator is invoked.
    /// The registry receives fully-resolved parameter strings.
    /// <para>
    /// If no <see cref="ICustomFunctionRegistry"/> was provided at construction,
    /// any attempt to evaluate a <see cref="CustomFunctionColumn"/> will throw a
    /// <see cref="ColumnEvaluationException"/> with an actionable diagnostic message.
    /// </para>
    /// </remarks>
    public sealed class CustomFunctionColumnEvaluator : IColumnEvaluator
    {
        private const string ExpressionPrefix = "=";

        private readonly IExpressionEngine _expressionEngine;
        private readonly ICustomFunctionRegistry? _registry;

        /// <summary>
        /// Initializes the evaluator with an optional custom function registry.
        /// Pass <c>null</c> if no custom functions are required (any CustomFunctionColumn
        /// encountered will then throw at evaluation time).
        /// </summary>
        public CustomFunctionColumnEvaluator(
            IExpressionEngine expressionEngine,
            ICustomFunctionRegistry? registry = null)
        {
            _expressionEngine = expressionEngine ?? throw new ArgumentNullException(nameof(expressionEngine));
            _registry = registry;
        }

        /// <inheritdoc />
        public bool CanEvaluate(ColumnDefinition column) => column is CustomFunctionColumn;

        /// <inheritdoc />
        /// <exception cref="ColumnEvaluationException">
        /// Thrown when no registry is configured, the function is not registered,
        /// or the function invocation throws.
        /// </exception>
        public string Evaluate(ColumnDefinition column, IExecutionContext context)
        {
            if (column is not CustomFunctionColumn funcCol)
                throw new ArgumentException(
                    $"Expected {nameof(CustomFunctionColumn)}, received '{column?.GetType().Name}'.",
                    nameof(column));

            if (string.IsNullOrWhiteSpace(funcCol.FunctionName))
                throw new ColumnEvaluationException(
                    column.Id, column.Name,
                    "CustomFunctionColumn.FunctionName cannot be null or empty.");

            if (_registry == null)
                throw new ColumnEvaluationException(
                    column.Id, column.Name,
                    $"Cannot invoke function '{funcCol.FunctionName}': no ICustomFunctionRegistry " +
                    $"was registered. Provide one via {nameof(Factory.ColumnEvaluatorFactory)}.");

            if (!_registry.IsRegistered(funcCol.FunctionName))
                throw new ColumnEvaluationException(
                    column.Id, column.Name,
                    $"Function '{funcCol.FunctionName}' is not found in the custom function registry. " +
                    "Ensure it is registered before parsing.");

            try
            {
                IReadOnlyList<string> parameters = funcCol.Parameters as IReadOnlyList<string>
                    ?? new List<string>(funcCol.Parameters);

                var resolvedParams = new List<string>(parameters.Count);
                for (int i = 0; i < parameters.Count; i++)
                {
                    var p = parameters[i];
                    if (p != null && p.StartsWith(ExpressionPrefix, StringComparison.Ordinal))
                    {
                        var innerExp = p.Substring(ExpressionPrefix.Length).Trim();
                        try
                        {
                            resolvedParams.Add(_expressionEngine.Evaluate(innerExp, context));
                        }
                        catch (Exception ex) when (ex is not ColumnEvaluationException)
                        {
                            throw new ColumnEvaluationException(
                                column.Id, column.Name,
                                $"Branch expression evaluation failed for parameter '{p}': {ex.Message}",
                                ex);
                        }
                    }
                    else
                    {
                        resolvedParams.Add(p ?? string.Empty);
                    }
                }

                return _registry.Invoke(funcCol.FunctionName, context.Source, resolvedParams);
            }
            catch (Exception ex) when (ex is not ColumnEvaluationException)
            {
                throw new ColumnEvaluationException(
                    column.Id, column.Name,
                    $"Custom function '{funcCol.FunctionName}' threw an exception: {ex.Message}",
                    ex);
            }
        }

    }
}
