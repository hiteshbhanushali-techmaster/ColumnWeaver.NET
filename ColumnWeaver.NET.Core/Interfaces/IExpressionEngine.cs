using System;
using System.Collections.Generic;

namespace ColumnWeaver.NET.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for evaluating dynamic string expressions
    /// (used by <see cref="Columns.FormulaColumn"/> and
    /// <see cref="Columns.ConditionalColumn"/>) against a source object context.
    /// </summary>
    /// <remarks>
    /// The expression syntax, feature set, and runtime behaviour are
    /// entirely determined by the concrete implementation (e.g., Roslyn scripting,
    /// NCalc, DynamicExpresso, or a custom evaluator). The Core layer is decoupled
    /// from any specific expression library.
    /// </remarks>
    public interface IExpressionEngine
    {
        /// <summary>
        /// Evaluates the given <paramref name="expression"/> in the context of the
        /// execution <paramref name="context"/> and returns the result as a string.
        /// </summary>
        /// <param name="expression">
        /// The expression string to evaluate. Must not be null or empty.
        /// </param>
        /// <param name="context">
        /// The execution context exposing the source object and variables.
        /// May have a null Source; the implementation must handle this gracefully.
        /// </param>
        /// <returns>
        /// The string result of evaluating the expression. Returns an empty string
        /// when the expression evaluates to null.
        /// </returns>
        string Evaluate(string expression, IExecutionContext context);

        /// <summary>
        /// Evaluates the given <paramref name="expression"/> and returns the raw,
        /// unboxed CLR result <b>before</b> any string conversion.
        /// </summary>
        /// <param name="expression">The expression to evaluate. Must not be null or empty.</param>
        /// <param name="context">The execution context exposing the source and variables. May have a null Source.</param>
        /// <returns>
        /// The unboxed CLR result (e.g., <c>double</c>, <c>int</c>, <c>DateTime</c>),
        /// or <c>null</c> when the expression evaluates to null.
        /// </returns>
        object? EvaluateRaw(string expression, IExecutionContext context);

        /// <summary>
        /// Evaluates a boolean condition expression against the
        /// <paramref name="context"/>.
        /// </summary>
        /// <param name="conditionExpression">
        /// The boolean expression to evaluate. Must not be null or empty.
        /// </param>
        /// <param name="context">
        /// The execution context exposing the source object and variables.
        /// May have a null Source; the implementation must handle this gracefully.
        /// </param>
        /// <returns>
        /// <c>true</c> when the condition expression evaluates to a truthy result;
        /// otherwise, <c>false</c>.
        /// </returns>
        bool EvaluateCondition(string conditionExpression, IExecutionContext context);

        /// <summary>
        /// Validates the syntax and semantics of an expression without executing it.
        /// </summary>
        /// <param name="expression">The expression string to validate.</param>
        /// <returns>
        /// <c>true</c> if the expression is valid and can be evaluated;
        /// otherwise, <c>false</c>.
        /// </returns>
        bool ValidateExpression(string expression);

        /// <summary>
        /// V2.5 — Compiles an expression into a pre-bound delegate that operates on an
        /// <see cref="IExecutionContext"/>.
        /// </summary>
        /// <remarks>
        /// When <see cref="EngineCapabilities.SupportsCompilation"/> is <c>true</c>,
        /// this method produces a <c>Func&lt;IExecutionContext, object?&gt;</c> that:
        /// <list type="bullet">
        ///   <item>Reads <c>ctx.Source</c> directly for property access.</item>
        ///   <item>Reads dependency values via <c>ctx.GetResult(index)</c>.</item>
        ///   <item>Uses NO dictionary lookups, NO string substitution, NO reflection
        ///         per execution call.</item>
        ///   <item>Returns the unboxed CLR result — no string conversion occurs here.</item>
        /// </list>
        /// When <see cref="EngineCapabilities.SupportsCompilation"/> is <c>false</c>,
        /// this method returns <c>null</c>.
        /// </remarks>
        Func<IExecutionContext, object?>? CompileExpression(
            string               expression,
            Type?                sourceType,
            IReadOnlyList<int>   dependencyIndexes,
            IReadOnlyDictionary<string, int>? variableIndexes = null,
            IReadOnlyDictionary<string, int>? columnIndexes = null,
            IReadOnlyList<object?>? variableValues = null);

        /// <summary>
        /// V2.5 — Compiles a boolean condition expression into a pre-bound delegate.
        /// </summary>
        Func<IExecutionContext, bool>? CompileCondition(
            string               conditionExpression,
            Type?                sourceType,
            IReadOnlyList<int>   dependencyIndexes,
            IReadOnlyDictionary<string, int>? variableIndexes = null,
            IReadOnlyDictionary<string, int>? columnIndexes = null,
            IReadOnlyList<object?>? variableValues = null);

        /// <summary>
        /// Gets the compile-time capabilities of this expression engine.
        /// </summary>
        EngineCapabilities Capabilities { get; }
    }
}
