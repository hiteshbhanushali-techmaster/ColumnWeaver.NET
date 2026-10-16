using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Interfaces;

namespace ColumnWeaver.NET.Application.Factory
{
    /// <summary>
    /// Application-layer abstraction for resolving the correct
    /// <see cref="IColumnEvaluator"/> for a given <see cref="ColumnDefinition"/> type.
    /// </summary>
    /// <remarks>
    /// The default implementation uses a Chain-of-Responsibility pattern across
    /// all registered evaluators. Custom implementations may use a registry,
    /// a dictionary, or any other resolution strategy.
    /// </remarks>
    public interface IColumnEvaluatorFactory
    {
        /// <summary>
        /// Returns the <see cref="IColumnEvaluator"/> capable of handling the
        /// specified <paramref name="column"/> definition.
        /// </summary>
        /// <param name="column">The column definition to resolve an evaluator for.</param>
        /// <returns>A matching <see cref="IColumnEvaluator"/>.</returns>
        /// <exception cref="Exceptions.ColumnEvaluationException">
        /// Thrown when no evaluator is registered that can handle the given column type.
        /// </exception>
        IColumnEvaluator GetEvaluator(ColumnDefinition column);
    }
}
