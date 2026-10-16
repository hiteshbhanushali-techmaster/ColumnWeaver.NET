using System;
using System.Collections.Generic;
using System.Linq;
using ColumnWeaver.NET.Application.Abstractions;
using ColumnWeaver.NET.Application.Evaluators;
using ColumnWeaver.NET.Application.Exceptions;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Interfaces;

namespace ColumnWeaver.NET.Application.Factory
{
    /// <summary>
    /// Default <see cref="IColumnEvaluatorFactory"/> implementation that resolves
    /// evaluators via a Chain-of-Responsibility across all registered
    /// <see cref="IColumnEvaluator"/> instances.
    /// </summary>
    /// <remarks>
    /// Two construction paths are available:
    /// <list type="number">
    ///   <item>
    ///     <b>Standard (DI-friendly):</b> Pass the Core infrastructure interfaces.
    ///     All five built-in evaluators are registered automatically.
    ///   </item>
    ///   <item>
    ///     <b>Advanced (custom evaluators):</b> Pass a fully composed list of
    ///     <see cref="IColumnEvaluator"/> instances. Useful for adding proprietary
    ///     column types or replacing built-in evaluators.
    ///   </item>
    /// </list>
    /// </remarks>
    public sealed class ColumnEvaluatorFactory : IColumnEvaluatorFactory
    {
        private readonly IReadOnlyList<IColumnEvaluator> _evaluators;

        // -----------------------------------------------------------------
        //  Constructor — Standard path (DI-friendly)
        // -----------------------------------------------------------------

        /// <summary>
        /// Initializes the factory with all built-in evaluators using the
        /// provided Core infrastructure interfaces.
        /// </summary>
        /// <param name="propertyResolver">
        /// Used by <see cref="PropertyColumnEvaluator"/>. Must not be null.
        /// </param>
        /// <param name="expressionEngine">
        /// Used by <see cref="FormulaColumnEvaluator"/> and
        /// <see cref="ConditionalColumnEvaluator"/>. Must not be null.
        /// </param>
        /// <param name="customFunctionRegistry">
        /// Used by <see cref="CustomFunctionColumnEvaluator"/>. May be null if
        /// no <see cref="Core.Columns.CustomFunctionColumn"/> columns are used.
        /// </param>
        public ColumnEvaluatorFactory(
            IPropertyResolver              propertyResolver,
            IExpressionEngine             expressionEngine,
            ICustomFunctionRegistry?      customFunctionRegistry = null)
        {
            if (propertyResolver  == null) throw new ArgumentNullException(nameof(propertyResolver));
            if (expressionEngine  == null) throw new ArgumentNullException(nameof(expressionEngine));

            _evaluators = new List<IColumnEvaluator>
            {
                new PropertyColumnEvaluator(propertyResolver),
                new FixedTextColumnEvaluator(),
                new FormulaColumnEvaluator(expressionEngine),
                new ConditionalColumnEvaluator(expressionEngine),
                new CustomFunctionColumnEvaluator(expressionEngine, customFunctionRegistry)
            }.AsReadOnly();
        }

        // -----------------------------------------------------------------
        //  Constructor — Advanced path (custom evaluator set)
        // -----------------------------------------------------------------

        /// <summary>
        /// Initializes the factory with a fully composed list of evaluators.
        /// Evaluators are queried in list order; the first one returning
        /// <c>true</c> from <see cref="IColumnEvaluator.CanEvaluate"/> is used.
        /// </summary>
        /// <param name="evaluators">
        /// The ordered list of evaluators to register. Must not be null or empty.
        /// </param>
        public ColumnEvaluatorFactory(IReadOnlyList<IColumnEvaluator> evaluators)
        {
            if (evaluators == null || evaluators.Count == 0)
                throw new ArgumentException(
                    "At least one evaluator must be provided.", nameof(evaluators));

            _evaluators = evaluators;
        }

        // -----------------------------------------------------------------
        //  IColumnEvaluatorFactory
        // -----------------------------------------------------------------

        /// <inheritdoc />
        public IColumnEvaluator GetEvaluator(ColumnDefinition column)
        {
            if (column == null) throw new ArgumentNullException(nameof(column));

            var evaluator = _evaluators.FirstOrDefault(e => e.CanEvaluate(column));

            if (evaluator == null)
                throw new ColumnEvaluationException(
                    column.Id, column.Name,
                    $"No evaluator is registered for column type '{column.GetType().Name}'. " +
                    $"Register a custom evaluator using the " +
                    $"{nameof(ColumnEvaluatorFactory)}(IReadOnlyList<IColumnEvaluator>) constructor.");

            return evaluator;
        }
    }
}
