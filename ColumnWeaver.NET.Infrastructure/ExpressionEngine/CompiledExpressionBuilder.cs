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
