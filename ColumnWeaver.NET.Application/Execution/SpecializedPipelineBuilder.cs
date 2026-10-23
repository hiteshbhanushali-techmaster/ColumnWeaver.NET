using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using ColumnWeaver.NET.Application.Abstractions;
using ColumnWeaver.NET.Application.PostProcessing;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Enums;
using ColumnWeaver.NET.Core.Models;
using ColumnWeaver.NET.Core.ParserConfiguration;

namespace ColumnWeaver.NET.Application.Execution
{
    /// <summary>
    /// Compiles an entire parser configuration into a single, specialized, strongly-typed
    /// delegate: Func&lt;object?, StringBuilder, string&gt;.
    /// Eliminates per-node dispatch, array allocations, boxing, and dictionary lookups.
    /// </summary>
    public static class SpecializedPipelineBuilder
    {
        private static readonly MethodInfo _sbAppendString
            = typeof(StringBuilder).GetMethod(nameof(StringBuilder.Append), new[] { typeof(string) })!;
        private static readonly MethodInfo _sbAppendChar
            = typeof(StringBuilder).GetMethod(nameof(StringBuilder.Append), new[] { typeof(char) })!;
        private static readonly MethodInfo _sbAppendInt
            = typeof(StringBuilder).GetMethod(nameof(StringBuilder.Append), new[] { typeof(int) })!;
        private static readonly MethodInfo _sbAppendLong
            = typeof(StringBuilder).GetMethod(nameof(StringBuilder.Append), new[] { typeof(long) })!;
        private static readonly MethodInfo _appendIntMethod
            = typeof(SpecializedPipelineBuilder).GetMethod(nameof(AppendInt), BindingFlags.Public | BindingFlags.Static)!;
        private static readonly MethodInfo _appendLongMethod
            = typeof(SpecializedPipelineBuilder).GetMethod(nameof(AppendLong), BindingFlags.Public | BindingFlags.Static)!;
        private static readonly MethodInfo _sbToString
            = typeof(StringBuilder).GetMethod(nameof(StringBuilder.ToString), Type.EmptyTypes)!;

        private static readonly MethodInfo _doubleToStringWithCulture
            = typeof(double).GetMethod(nameof(double.ToString), new[] { typeof(string), typeof(IFormatProvider) })!;
        private static readonly MethodInfo _decimalToStringWithCulture
            = typeof(decimal).GetMethod(nameof(decimal.ToString), new[] { typeof(string), typeof(IFormatProvider) })!;
        private static readonly MethodInfo _intToStringWithCulture
            = typeof(int).GetMethod(nameof(int.ToString), new[] { typeof(string), typeof(IFormatProvider) })!;
        private static readonly MethodInfo _longToStringWithCulture
            = typeof(long).GetMethod(nameof(long.ToString), new[] { typeof(string), typeof(IFormatProvider) })!;
        private static readonly MethodInfo _formattableToString
            = typeof(IFormattable).GetMethod(nameof(IFormattable.ToString), new[] { typeof(string), typeof(IFormatProvider) })!;
        private static readonly MethodInfo _objToString
            = typeof(object).GetMethod(nameof(object.ToString), Type.EmptyTypes)!;

        private static readonly MethodInfo _appendFormattedColumnMethod
            = typeof(SpecializedPipelineBuilder).GetMethod(nameof(AppendFormattedColumn), BindingFlags.Public | BindingFlags.Static)!;

        private static readonly Formatter _formatter = new Formatter();
        private static readonly NullHandler _nullHandler = new NullHandler();
        private static readonly ReplacementApplicator _replacementApplicator = new ReplacementApplicator();

        /// <summary>
        /// Attempts to compile a specialized row executor delegate for the given configuration and source type.
        /// </summary>
        /// <param name="config">The parser configuration.</param>
        /// <param name="sourceType">The source object type.</param>
        /// <param name="evaluationOrder">The execution nodes in topological evaluation order.</param>
        /// <param name="assemblyOrder">The execution nodes in final assembly order.</param>
        /// <param name="allColumns">All column definitions in the configuration.</param>
        /// <param name="nameToIndex">Map of column names to indices.</param>
        /// <param name="variableIndexes">Map of variable names to indices.</param>
        /// <param name="compiledVariables">Array of compiled variable values.</param>
        /// <param name="registry">The custom function registry, or <c>null</c>.</param>
        /// <returns>A compiled row executor delegate, or <c>null</c> if specialized compilation is not applicable.</returns>
        public static Func<object?, StringBuilder, string>? TryBuild(
            ParserConfiguration config,
            Type? sourceType,
            IReadOnlyList<ExecutionNode> evaluationOrder,
            IReadOnlyList<ExecutionNode> assemblyOrder,
            IReadOnlyList<ColumnDefinition> allColumns,
            IReadOnlyDictionary<string, int> nameToIndex,
            IReadOnlyDictionary<string, int> variableIndexes,
            object?[] compiledVariables,
            ICustomFunctionRegistry? registry)
        {
            if (sourceType == null || config == null || allColumns == null || allColumns.Count == 0)
                return null;

            if (evaluationOrder == null || assemblyOrder == null)
                return null;

            // NullHandlingStrategy.Skip conditionally removes separator slots at runtime;
            // fall back to node-based pipeline for 100% dynamic behavior.
            for (int i = 0; i < allColumns.Count; i++)
            {
                if (allColumns[i]?.NullHandling?.Strategy == NullHandlingStrategy.Skip)
                    return null;
            }

            try
            {
                var varValuesByName = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                if (config.Variables != null)
                {
                    foreach (var kvp in config.Variables)
                        varValuesByName[kvp.Key] = kvp.Value;
                }

                var sourceObjParam = Expression.Parameter(typeof(object), "sourceObj");
                var sbParam = Expression.Parameter(typeof(StringBuilder), "sb");
                var srcVar = Expression.Variable(sourceType, "src");

                var localVars = new List<ParameterExpression> { srcVar };
                var blockStatements = new List<Expression>();

                var returnTarget = Expression.Label(typeof(string), "returnTarget");

                // Guard: sourceObj == null -> return null (fallback to standard execution)
                blockStatements.Add(
                    Expression.IfThen(
                        Expression.Equal(sourceObjParam, Expression.Constant(null)),
                        Expression.Return(returnTarget, Expression.Constant(null, typeof(string)))
                    )
                );

                // Guard: !(sourceObj is TSource) -> return null
                blockStatements.Add(
                    Expression.IfThen(
                        Expression.Not(Expression.TypeIs(sourceObjParam, sourceType)),
                        Expression.Return(returnTarget, Expression.Constant(null, typeof(string)))
                    )
                );

                // src = (TSource)sourceObj;
                blockStatements.Add(Expression.Assign(srcVar, Expression.Convert(sourceObjParam, sourceType)));

                var colVarByIndex = new ParameterExpression[allColumns.Count];
                var colExprByName = new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase);

                // 1. Evaluate columns in topological EvaluationOrder
                foreach (var node in evaluationOrder)
                {
                    int colIdx = node.ColumnIndex;
                    var col = allColumns[colIdx];

                    Expression? colExpr = null;
                    switch (col)
                    {
                        case FixedTextColumn ft:
                            colExpr = Expression.Constant(ft.Text ?? string.Empty, typeof(string));
                            break;

                        case PropertyColumn pc:
                            if (string.IsNullOrWhiteSpace(pc.PropertyPath))
                            {
                                colExpr = Expression.Constant(null, typeof(object));
                            }
                            else
                            {
                                colExpr = PropertyAccessorBuilder.BuildPropertyExpression(sourceType, pc.PropertyPath, srcVar);
                                if (colExpr == null) return null;
                            }
                            break;

                        case FormulaColumn fc:
                            var expr = fc.Expression;
                            if (expr == null || string.IsNullOrWhiteSpace(expr)) return null;

                            var trimmedExpr = expr.Trim();
                            if (trimmedExpr.StartsWith("${") && trimmedExpr.EndsWith("}"))
                            {
                                var varName = trimmedExpr.Substring(2, trimmedExpr.Length - 3).Trim();
                                if (varValuesByName.TryGetValue(varName, out var vVal))
                                {
                                    colExpr = Expression.Constant(vVal, vVal != null ? vVal.GetType() : typeof(object));
                                    break;
                                }
                            }

                            colExpr = CompiledExpressionBuilder.BuildTypedExpression(expr, sourceType, srcVar, colExprByName, varValuesByName);
                            if (colExpr == null) return null;
                            break;

                        case ConditionalColumn cc:
                            var condExprStr = cc.ConditionExpression;
                            if (condExprStr == null || string.IsNullOrWhiteSpace(condExprStr)) return null;

                            var condExpr = CompiledExpressionBuilder.BuildTypedExpression(condExprStr, sourceType, srcVar, colExprByName, varValuesByName);
                            if (condExpr == null) return null;

                            Expression boolCond = condExpr.Type == typeof(bool)
                                ? condExpr
                                : Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToBoolean), null, Expression.Convert(condExpr, typeof(object)));

                            var trueExpr = BuildBranchExpression(cc.TrueValue, sourceType, srcVar, colExprByName, varValuesByName);
                            var falseExpr = BuildBranchExpression(cc.FalseValue, sourceType, srcVar, colExprByName, varValuesByName);
                            if (trueExpr == null || falseExpr == null) return null;

                            if (trueExpr.Type == falseExpr.Type)
                            {
                                colExpr = Expression.Condition(boolCond, trueExpr, falseExpr);
                            }
                            else
                            {
                                colExpr = Expression.Condition(boolCond, Expression.Convert(trueExpr, typeof(object)), Expression.Convert(falseExpr, typeof(object)));
                            }
                            break;

                        case CustomFunctionColumn cfc:
                            if (registry != null && registry.IsRegistered(cfc.FunctionName))
                            {
                                var paramExprs = new List<Expression>();
                                if (cfc.Parameters != null)
                                {
                                    foreach (var p in cfc.Parameters)
                                    {
                                        var resolvedP = ResolveTextParameter(p, colExprByName, varValuesByName);
                                        if (resolvedP == null) return null;
                                        paramExprs.Add(resolvedP);
                                    }
                                }
                                var paramArr = Expression.NewArrayInit(typeof(string), paramExprs);
                                var invokeRawMethod = typeof(ICustomFunctionRegistry).GetMethod(
                                    nameof(ICustomFunctionRegistry.InvokeRaw),
                                    new[] { typeof(string), typeof(object), typeof(IReadOnlyList<string>) })!;

                                colExpr = Expression.Call(
                                    Expression.Constant(registry),
                                    invokeRawMethod,
                                    Expression.Constant(cfc.FunctionName),
                                    Expression.Convert(srcVar, typeof(object)),
                                    paramArr);
                            }
                            else
                            {
                                return null;
                            }
                            break;

                        default:
                            return null;
                    }

                    var colVar = Expression.Variable(colExpr.Type, "col_" + colIdx);
                    localVars.Add(colVar);
                    blockStatements.Add(Expression.Assign(colVar, colExpr));
                    colVarByIndex[colIdx] = colVar;

                    if (!string.IsNullOrWhiteSpace(col.Name)) colExprByName[col.Name] = colVar;
                    if (!string.IsNullOrWhiteSpace(col.Alias)) colExprByName[col.Alias!] = colVar;
                }

                // 2. Assemble output in AssemblyOrder
                int lastActiveIdx = -1;
                if (config.PreserveSeparatorForDisabledColumns)
                {
                    lastActiveIdx = assemblyOrder.Count - 1;
                }
                else
                {
                    for (int i = assemblyOrder.Count - 1; i >= 0; i--)
                    {
                        if (assemblyOrder[i].IsEnabled)
                        {
                            lastActiveIdx = i;
                            break;
                        }
                    }
                }

                if (lastActiveIdx < 0)
                {
                    blockStatements.Add(Expression.Return(returnTarget, Expression.Constant(string.Empty)));
                }
                else
                {
                    for (int i = 0; i <= lastActiveIdx; i++)
                    {
                        var node = assemblyOrder[i];
                        int colIdx = node.ColumnIndex;
                        var col = allColumns[colIdx];

                        if (!node.IsEnabled)
                        {
                            if (config.PreserveSeparatorForDisabledColumns)
                            {
                                if (i < lastActiveIdx)
                                {
                                    var sep = node.SeparatorOverride ?? config.GlobalSeparator ?? string.Empty;
                                    if (!string.IsNullOrEmpty(sep))
                                    {
                                        if (sep.Length == 1)
                                            blockStatements.Add(Expression.Call(sbParam, _sbAppendChar, Expression.Constant(sep[0])));
                                        else
                                            blockStatements.Add(Expression.Call(sbParam, _sbAppendString, Expression.Constant(sep)));
                                    }
                                }
                            }
                            continue;
                        }

                        var colVar = colVarByIndex[colIdx];
                        EmitAppendColumn(blockStatements, sbParam, colVar, col);

                        if (i < lastActiveIdx)
                        {
                            var sep = node.SeparatorOverride ?? config.GlobalSeparator ?? string.Empty;
                            if (!string.IsNullOrEmpty(sep))
                            {
                                if (sep.Length == 1)
                                    blockStatements.Add(Expression.Call(sbParam, _sbAppendChar, Expression.Constant(sep[0])));
                                else
                                    blockStatements.Add(Expression.Call(sbParam, _sbAppendString, Expression.Constant(sep)));
                            }
                        }
                    }
                }

                blockStatements.Add(Expression.Return(returnTarget, Expression.Call(sbParam, _sbToString)));
                blockStatements.Add(Expression.Label(returnTarget, Expression.Constant(string.Empty)));

                var block = Expression.Block(localVars, blockStatements);

                var tryCatch = Expression.TryCatch(
                    block,
                    Expression.Catch(typeof(Exception), Expression.Constant(null, typeof(string)))
                );

                var lambda = Expression.Lambda<Func<object?, StringBuilder, string>>(tryCatch, sourceObjParam, sbParam);
                return lambda.Compile();
            }
            catch
            {
                return null;
            }
        }

        private static Expression? BuildBranchExpression(
            string? branch,
            Type sourceType,
            Expression srcVar,
            IReadOnlyDictionary<string, Expression> colExprByName,
            IReadOnlyDictionary<string, object?> varValuesByName)
        {
            if (branch == null || branch.Length == 0)
                return Expression.Constant(string.Empty, typeof(string));

            if (branch.StartsWith("="))
            {
                var expr = branch.Substring(1).Trim();
                if (expr.Length == 0) return null;
                return CompiledExpressionBuilder.BuildTypedExpression(expr, sourceType, srcVar, colExprByName, varValuesByName);
            }

            return ResolveTextParameter(branch, colExprByName, varValuesByName);
        }

        private static Expression? ResolveTextParameter(
            string text,
            IReadOnlyDictionary<string, Expression> colExprByName,
            IReadOnlyDictionary<string, object?> varValuesByName)
        {
            if (string.IsNullOrEmpty(text))
                return Expression.Constant(string.Empty, typeof(string));

            var trimmed = text.Trim();
            if (trimmed.StartsWith("${") && trimmed.EndsWith("}"))
            {
                var name = trimmed.Substring(2, trimmed.Length - 3).Trim();
                if (varValuesByName.TryGetValue(name, out var vVal))
                {
                    return Expression.Constant(vVal?.ToString() ?? string.Empty, typeof(string));
                }
                if (colExprByName.TryGetValue(name, out var colExp))
                {
                    if (colExp.Type == typeof(string)) return colExp;
                    return Expression.Call(typeof(CompiledExpressionCoercion), nameof(CompiledExpressionCoercion.ToString), null, Expression.Convert(colExp, typeof(object)));
                }
            }

            return Expression.Constant(text, typeof(string));
        }

        private static void EmitAppendColumn(
            List<Expression> blockStatements,
            ParameterExpression sbParam,
            ParameterExpression colVar,
            ColumnDefinition col)
        {
            var nh = col.NullHandling;
            var fo = col.Formatting;
            var rr = col.ReplacementRules;
            var formatString = fo?.FormatString;

            bool isNullDefault = nh == null || (nh.Strategy == NullHandlingStrategy.EmptyString && string.IsNullOrEmpty(nh.DefaultValue) && string.IsNullOrEmpty(nh.ExceptionMessage));
            bool isFoDefault = fo == null || (fo.TrimBehavior == TrimBehavior.None && fo.CasingTransform == CasingTransform.None && fo.MaxLength <= 0 && fo.PaddedWidth <= 0 && string.IsNullOrEmpty(fo.Prefix) && string.IsNullOrEmpty(fo.Suffix));
            bool hasRr = rr != null && rr.Count > 0;

            if (colVar.Type == typeof(string) && isFoDefault && !hasRr && nh != null && nh.Strategy == NullHandlingStrategy.UseDefault && string.IsNullOrEmpty(formatString))
            {
                var defaultVal = nh.DefaultValue ?? string.Empty;
                var coalesced = Expression.Coalesce(colVar, Expression.Constant(defaultVal, typeof(string)));
                blockStatements.Add(Expression.Call(sbParam, _sbAppendString, coalesced));
                return;
            }

            if (isNullDefault && isFoDefault && !hasRr)
            {
                if (string.IsNullOrEmpty(formatString))
                {
                    if (colVar.Type == typeof(string))
                    {
                        blockStatements.Add(
                            Expression.IfThen(
                                Expression.NotEqual(colVar, Expression.Constant(null, typeof(string))),
                                Expression.Call(sbParam, _sbAppendString, colVar)
                            )
                        );
                    }
                    else if (colVar.Type == typeof(int))
                    {
                        blockStatements.Add(Expression.Call(_appendIntMethod, sbParam, colVar));
                    }
                    else if (colVar.Type == typeof(long))
                    {
                        blockStatements.Add(Expression.Call(_appendLongMethod, sbParam, colVar));
                    }
                    else if (colVar.Type == typeof(double))
                    {
                        blockStatements.Add(
                            Expression.Call(sbParam, _sbAppendString,
                                Expression.Call(colVar, _doubleToStringWithCulture, Expression.Constant(null, typeof(string)), Expression.Constant(CultureInfo.CurrentCulture)))
                        );
                    }
                    else if (colVar.Type == typeof(decimal))
                    {
                        blockStatements.Add(
                            Expression.Call(sbParam, _sbAppendString,
                                Expression.Call(colVar, _decimalToStringWithCulture, Expression.Constant(null, typeof(string)), Expression.Constant(CultureInfo.CurrentCulture)))
                        );
                    }
                    else if (colVar.Type == typeof(bool))
                    {
                        blockStatements.Add(
                            Expression.Call(sbParam, _sbAppendString,
                                Expression.Condition(colVar, Expression.Constant("True"), Expression.Constant("False")))
                        );
                    }
                    else if (typeof(IFormattable).IsAssignableFrom(colVar.Type))
                    {
                        if (colVar.Type.IsValueType)
                        {
                            blockStatements.Add(
                                Expression.Call(sbParam, _sbAppendString,
                                    Expression.Call(colVar, _formattableToString, Expression.Constant(null, typeof(string)), Expression.Constant(CultureInfo.CurrentCulture)))
                            );
                        }
                        else
                        {
                            blockStatements.Add(
                                Expression.IfThen(
                                    Expression.NotEqual(colVar, Expression.Constant(null, colVar.Type)),
                                    Expression.Call(sbParam, _sbAppendString,
                                        Expression.Call(colVar, _formattableToString, Expression.Constant(null, typeof(string)), Expression.Constant(CultureInfo.CurrentCulture)))
                                )
                            );
                        }
                    }
                    else
                    {
                        if (colVar.Type.IsValueType)
                        {
                            blockStatements.Add(
                                Expression.Call(sbParam, _sbAppendString,
                                    Expression.Call(Expression.Convert(colVar, typeof(object)), _objToString))
                            );
                        }
                        else
                        {
                            blockStatements.Add(
                                Expression.IfThen(
                                    Expression.NotEqual(colVar, Expression.Constant(null, colVar.Type)),
                                    Expression.Call(sbParam, _sbAppendString,
                                        Expression.Call(Expression.Convert(colVar, typeof(object)), _objToString))
                                )
                            );
                        }
                    }
                }
                else
                {
                    if (colVar.Type == typeof(double))
                    {
                        blockStatements.Add(
                            Expression.Call(sbParam, _sbAppendString,
                                Expression.Call(colVar, _doubleToStringWithCulture, Expression.Constant(formatString), Expression.Constant(CultureInfo.CurrentCulture)))
                        );
                    }
                    else if (colVar.Type == typeof(decimal))
                    {
                        blockStatements.Add(
                            Expression.Call(sbParam, _sbAppendString,
                                Expression.Call(colVar, _decimalToStringWithCulture, Expression.Constant(formatString), Expression.Constant(CultureInfo.CurrentCulture)))
                        );
                    }
                    else if (colVar.Type == typeof(int))
                    {
                        blockStatements.Add(
                            Expression.Call(sbParam, _sbAppendString,
                                Expression.Call(colVar, _intToStringWithCulture, Expression.Constant(formatString), Expression.Constant(CultureInfo.CurrentCulture)))
                        );
                    }
                    else if (colVar.Type == typeof(long))
                    {
                        blockStatements.Add(
                            Expression.Call(sbParam, _sbAppendString,
                                Expression.Call(colVar, _longToStringWithCulture, Expression.Constant(formatString), Expression.Constant(CultureInfo.CurrentCulture)))
                        );
                    }
                    else if (typeof(IFormattable).IsAssignableFrom(colVar.Type))
                    {
                        blockStatements.Add(
                            Expression.Call(sbParam, _sbAppendString,
                                Expression.Call(colVar, _formattableToString, Expression.Constant(formatString), Expression.Constant(CultureInfo.CurrentCulture)))
                        );
                    }
                    else
                    {
                        blockStatements.Add(
                            Expression.Call(sbParam, _sbAppendString,
                                Expression.Call(Expression.Convert(colVar, typeof(object)), _objToString))
                        );
                    }
                }
            }
            else
            {
                blockStatements.Add(
                    Expression.Call(
                        _appendFormattedColumnMethod,
                        sbParam,
                        Expression.Convert(colVar, typeof(object)),
                        Expression.Constant(formatString, typeof(string)),
                        Expression.Constant(col, typeof(ColumnDefinition))
                    )
                );
            }
        }

        /// <summary>
        /// Formats and appends a single column value to the string builder applying null-handling, formatting, and replacements.
        /// </summary>
        /// <param name="sb">The target string builder.</param>
        /// <param name="rawValue">The raw evaluated object value.</param>
        /// <param name="formatString">The optional format string.</param>
        /// <param name="col">The column definition containing post-processing options.</param>
        public static void AppendFormattedColumn(
            StringBuilder sb,
            object? rawValue,
            string? formatString,
            ColumnDefinition col)
        {
            string? rawString = rawValue == null
                ? null
                : (rawValue is IFormattable f && !string.IsNullOrEmpty(formatString)
                    ? f.ToString(formatString, CultureInfo.CurrentCulture)
                    : rawValue.ToString());

            var handled = _nullHandler.Handle(rawString, col);
            if (handled == null) return;

            var formatted = _formatter.Apply(handled, col);
            var final = _replacementApplicator.Apply(formatted, col.ReplacementRules);
            if (!string.IsNullOrEmpty(final))
                sb.Append(final);
        }

        [ThreadStatic]
        private static char[]? _intBuffer;
        [ThreadStatic]
        private static char[]? _longBuffer;

        /// <summary>
        /// Appends an integer value to the string builder without intermediate string allocations.
        /// </summary>
        public static void AppendInt(StringBuilder sb, int value)
        {
            if (value >= 0 && value < 10)
            {
                sb.Append((char)('0' + value));
                return;
            }
            if (value == int.MinValue)
            {
                sb.Append("-2147483648");
                return;
            }
            var buffer = _intBuffer ?? (_intBuffer = new char[16]);
            int pos = 16;
            bool negative = value < 0;
            if (negative) value = -value;

            while (value > 0)
            {
                int rem = value % 10;
                buffer[--pos] = (char)('0' + rem);
                value /= 10;
            }
            if (negative)
            {
                buffer[--pos] = '-';
            }
            sb.Append(buffer, pos, 16 - pos);
        }

        /// <summary>
        /// Appends a 64-bit integer value to the string builder without intermediate string allocations.
        /// </summary>
        public static void AppendLong(StringBuilder sb, long value)
        {
            if (value >= 0 && value < 10)
            {
                sb.Append((char)('0' + value));
                return;
            }
            if (value == long.MinValue)
            {
                sb.Append("-9223372036854775808");
                return;
            }
            var buffer = _longBuffer ?? (_longBuffer = new char[24]);
            int pos = 24;
            bool negative = value < 0;
            if (negative) value = -value;

            while (value > 0)
            {
                long rem = value % 10;
                buffer[--pos] = (char)('0' + rem);
                value /= 10;
            }
            if (negative)
            {
                buffer[--pos] = '-';
            }
            sb.Append(buffer, pos, 24 - pos);
        }
    }
}
