using System;
using ColumnWeaver.NET.Application.Exceptions;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Enums;

namespace ColumnWeaver.NET.Application.PostProcessing
{
    /// <summary>
    /// Applies the null-resolution strategy from a column's
    /// <see cref="Core.Models.NullHandlingConfig"/> to a raw evaluated value.
    /// </summary>
    /// <remarks>
    /// Called by <see cref="Pipeline.ColumnPipeline"/> immediately after raw evaluation.
    /// A return value of <c>null</c> signals to the pipeline that the column should
    /// be skipped entirely (no output, no separator).
    /// </remarks>
    public sealed class NullHandler
    {
        /// <summary>
        /// Handles a potentially-null <paramref name="rawValue"/> according to
        /// the <paramref name="column"/>'s null strategy.
        /// </summary>
        /// <param name="rawValue">
        /// The raw string value returned by the column evaluator.
        /// A <c>null</c> value indicates the underlying property or expression was null.
        /// </param>
        /// <param name="column">The column definition supplying the null-handling config.</param>
        /// <returns>
        /// <list type="bullet">
        ///   <item>A non-null string → use as the column's value.</item>
        ///   <item><c>null</c> → skip this column entirely (caller must honour this).</item>
        /// </list>
        /// </returns>
        /// <exception cref="ColumnEvaluationException">
        /// Thrown when <see cref="NullHandlingStrategy.ThrowException"/> is configured
        /// and <paramref name="rawValue"/> is null.
        /// </exception>
        public string? Handle(string? rawValue, ColumnDefinition column)
        {
            // Non-null value — pass through unchanged; no null strategy needed.
            if (rawValue != null)
                return rawValue;

            var config = column.NullHandling;

            switch (config.Strategy)
            {
                case NullHandlingStrategy.EmptyString:
                    return string.Empty;

                case NullHandlingStrategy.UseDefault:
                    return config.DefaultValue ?? string.Empty;

                case NullHandlingStrategy.Skip:
                    // Return null to signal the pipeline to skip this column.
                    return null;

                case NullHandlingStrategy.LiteralNull:
                    return "NULL";

                case NullHandlingStrategy.ThrowException:
                    var message = !string.IsNullOrWhiteSpace(config.ExceptionMessage)
                        ? config.ExceptionMessage!
                        : $"Column '{column.DisplayName}' produced a null value and is configured to throw.";

                    throw new ColumnEvaluationException(column.Id, column.Name, message);

                default:
                    return string.Empty;
            }
        }
    }
}
