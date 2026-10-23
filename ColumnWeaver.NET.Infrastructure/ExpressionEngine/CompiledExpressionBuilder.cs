using System;
using System.Collections.Generic;
using ColumnWeaver.NET.Core.Interfaces;

namespace ColumnWeaver.NET.Infrastructure.ExpressionEngine
{
    /// <summary>
    /// Recursive-descent expression parser and LINQ expression tree compiler for
    /// formulas, variables, and conditions.
    /// Forwards to <see cref="ColumnWeaver.NET.Application.Execution.CompiledExpressionBuilder"/>.
    /// </summary>
    public static class CompiledExpressionBuilder
    {
        /// <summary>
        /// Compiles an expression string into an executable delegate that accepts an execution context and returns the evaluated result.
        /// </summary>
        /// <param name="expression">The expression string to compile.</param>
        /// <param name="sourceType">The optional source input type.</param>
        /// <param name="variableIndexes">The map of variable names to variable array indexes.</param>
        /// <param name="columnIndexes">The map of column names to column result array indexes.</param>
        /// <param name="variableValues">The list of compiled variable values.</param>
        /// <returns>A compiled delegate, or <c>null</c> if compilation fails.</returns>
        public static Func<IExecutionContext, object?>? Compile(
            string expression,
            Type? sourceType,
            IReadOnlyDictionary<string, int>? variableIndexes,
            IReadOnlyDictionary<string, int>? columnIndexes,
            IReadOnlyList<object?>? variableValues)
        {
            return Application.Execution.CompiledExpressionBuilder.Compile(
                expression, sourceType, variableIndexes, columnIndexes, variableValues);
        }

        /// <summary>
        /// Compiles a condition expression string into an executable delegate that evaluates to a boolean value.
        /// </summary>
        /// <param name="conditionExpression">The boolean condition expression string to compile.</param>
        /// <param name="sourceType">The optional source input type.</param>
        /// <param name="variableIndexes">The map of variable names to variable array indexes.</param>
        /// <param name="columnIndexes">The map of column names to column result array indexes.</param>
        /// <param name="variableValues">The list of compiled variable values.</param>
        /// <returns>A compiled boolean delegate, or <c>null</c> if compilation fails.</returns>
        public static Func<IExecutionContext, bool>? CompileCondition(
            string conditionExpression,
            Type? sourceType,
            IReadOnlyDictionary<string, int>? variableIndexes,
            IReadOnlyDictionary<string, int>? columnIndexes,
            IReadOnlyList<object?>? variableValues)
        {
            return Application.Execution.CompiledExpressionBuilder.CompileCondition(
                conditionExpression, sourceType, variableIndexes, columnIndexes, variableValues);
        }
    }
}
