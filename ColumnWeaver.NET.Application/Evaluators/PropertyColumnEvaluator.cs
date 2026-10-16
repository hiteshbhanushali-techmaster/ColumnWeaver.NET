using System;
using ColumnWeaver.NET.Application.Exceptions;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Interfaces;

namespace ColumnWeaver.NET.Application.Evaluators
{
    /// <summary>
    /// Evaluates a <see cref="PropertyColumn"/> by navigating a dot-separated
    /// property path on the source object using the injected <see cref="IPropertyResolver"/>.
    /// </summary>
    /// <remarks>
    /// <b>Null convention:</b> This evaluator returns <c>null</c> (via <c>null!</c>) when
    /// <list type="bullet">
    ///   <item>The source object itself is null.</item>
    ///   <item>An intermediate or terminal property in the path is null.</item>
    /// </list>
    /// The pipeline's <see cref="PostProcessing.NullHandler"/> is responsible for
    /// applying the column's <see cref="Core.Models.NullHandlingConfig"/> to that null signal.
    /// <para>
    /// <b>FormatString:</b> Applied here while the resolved value is still typed
    /// (before <c>ToString()</c>). This allows date, decimal, and numeric format
    /// strings to work correctly.
    /// </para>
    /// </remarks>
    public sealed class PropertyColumnEvaluator : IColumnEvaluator
    {
        private readonly IPropertyResolver _resolver;

        /// <summary>
        /// Initializes the evaluator with the property resolver to use at runtime.
        /// </summary>
        public PropertyColumnEvaluator(IPropertyResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        /// <inheritdoc />
        public bool CanEvaluate(ColumnDefinition column) => column is PropertyColumn;

        /// <inheritdoc />
        /// <exception cref="ColumnEvaluationException">
        /// Thrown when the property path is missing or the resolver reports a runtime error.
        /// </exception>
        public string Evaluate(ColumnDefinition column, IExecutionContext context)
        {
            if (column is not PropertyColumn propCol)
                throw new ArgumentException(
                    $"Expected {nameof(PropertyColumn)}, received '{column?.GetType().Name}'.",
                    nameof(column));

            // Null source → signal null to the pipeline.
            if (context.Source == null)
                return null!;

            if (string.IsNullOrWhiteSpace(propCol.PropertyPath))
                throw new ColumnEvaluationException(
                    column.Id, column.Name,
                    "PropertyPath cannot be null or empty.");

            try
            {
                var resolved = _resolver.Resolve(context.Source, propCol.PropertyPath);

                // Propagate null to the pipeline's NullHandler.
                if (resolved == null)
                    return null!;

                // Apply FormatString while we still have the typed value.
                var formatString = column.Formatting.FormatString;
                if (!string.IsNullOrEmpty(formatString) && resolved is IFormattable formattable)
                    return formattable.ToString(formatString, null);

                return resolved.ToString() ?? string.Empty;
            }
            catch (Exception ex) when (ex is not ColumnEvaluationException)
            {
                throw new ColumnEvaluationException(
                    column.Id, column.Name,
                    $"Failed to resolve property path '{propCol.PropertyPath}': {ex.Message}",
                    ex);
            }
        }
    }
}
