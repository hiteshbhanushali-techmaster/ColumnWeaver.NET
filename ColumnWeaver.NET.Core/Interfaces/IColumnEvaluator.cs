using ColumnWeaver.NET.Core.Columns;

namespace ColumnWeaver.NET.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for evaluating a single <see cref="ColumnDefinition"/>
    /// against a source object and producing the column's string fragment.
    /// </summary>
    /// <remarks>
    /// Implementations are responsible for:
    /// <list type="bullet">
    ///   <item>Dispatching to the correct evaluation strategy based on column type.</item>
    ///   <item>Applying null-handling, formatting, and replacement rules from the column config.</item>
    ///   <item>Returning the final formatted string fragment for that column.</item>
    /// </list>
    /// Each column type (Property, Formula, Conditional, etc.) should be handled
    /// by the concrete evaluator, typically via a visitor or type-switch pattern.
    /// </remarks>
    public interface IColumnEvaluator
    {
        /// <summary>
        /// Evaluates the given <paramref name="column"/> definition against the
        /// <paramref name="context"/> evaluation context and returns the resulting string fragment.
        /// </summary>
        /// <param name="column">
        /// The column definition to evaluate. Must not be null.
        /// </param>
        /// <param name="context">
        /// The evaluation context containing the source object and variables.
        /// </param>
        /// <returns>
        /// The formatted string fragment contributed by this column,
        /// after applying null handling, formatting, and replacement rules.
        /// </returns>
        string Evaluate(ColumnDefinition column, IExecutionContext context);

        /// <summary>
        /// Determines whether this evaluator can handle the given column type.
        /// Used in composite or chain-of-responsibility evaluator implementations.
        /// </summary>
        /// <param name="column">The column definition to inspect.</param>
        /// <returns>
        /// <c>true</c> if this evaluator supports the column; otherwise, <c>false</c>.
        /// </returns>
        bool CanEvaluate(ColumnDefinition column);
    }
}
