using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ColumnWeaver.NET.Application.Abstractions;
using ColumnWeaver.NET.Application.Exceptions;
using ColumnWeaver.NET.Application.Pipeline;
using ColumnWeaver.NET.Application.PostProcessing;
using ColumnWeaver.NET.Application.Validation;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Interfaces;
using ColumnWeaver.NET.Core.ParserConfiguration;

namespace ColumnWeaver.NET.Application.Execution
{
    /// <summary>
    /// V2.5 — Transforms a <see cref="ParserConfiguration"/> into a compiled, cached
    /// <see cref="ExecutionPlan"/> ready for zero-overhead repeated execution.
    /// </summary>
    /// <remarks>
    /// <b>Compile steps:</b>
    /// <list type="number">
    ///   <item>Full configuration validation via <see cref="ConfigurationValidator"/>.</item>
    ///   <item>Sort columns by <see cref="ColumnDefinition.Order"/> if requested.</item>
    ///   <item>Assign integer indexes to each column.</item>
    ///   <item>Extract cross-column references and resolve to integer dependency indexes.</item>
    ///   <item>Topological sort (DFS) on integer indexes.</item>
    ///   <item>Infer <see cref="ColumnDefinition.OutputType"/> per column via <see cref="TypeInferrer"/>.</item>
    ///   <item>Build reverse-dependency map for future parallel execution.</item>
    ///   <item>Compile a <c>Func&lt;ExecutionContext, object?&gt;</c> delegate per column:
    ///     <list type="bullet">
    ///       <item>IF engine supports compilation → pre-bind all deps, compile to delegate</item>
    ///       <item>ELSE → fallback to <c>EvaluateRaw</c> at runtime</item>
    ///     </list>
    ///   </item>
    ///   <item>Build <see cref="ExecutionPlan"/> with evaluation and assembly orders.</item>
    /// </list>
    ///
    /// <b>V2.5 hot-path contract:</b>
    /// Compiled delegates MUST use only <c>context.Source</c> and
    /// <c>context.GetResult(int)</c>. They MUST NOT call any factory, perform
    /// repeated reflection, or do string-keyed dictionary lookups.
    /// </remarks>
    public sealed class ExecutionPlanCompiler
    {
        private readonly IPropertyResolver              _resolver;
        private readonly IExpressionEngine             _engine;
        private readonly ICustomFunctionRegistry?      _registry;
        private readonly Formatter                      _formatter;
        private readonly NullHandler                    _nullHandler;
        private readonly ReplacementApplicator          _replacementApplicator;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionPlanCompiler"/> class.
        /// </summary>
        /// <param name="resolver">The property resolver used for property columns.</param>
        /// <param name="engine">The expression engine used for dynamic formulas and conditions.</param>
        /// <param name="registry">The optional custom function registry.</param>
        public ExecutionPlanCompiler(
            IPropertyResolver         resolver,
            IExpressionEngine        engine,
            ICustomFunctionRegistry? registry = null)
        {
            _resolver              = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _engine                = engine   ?? throw new ArgumentNullException(nameof(engine));
            _registry              = registry;
            _formatter             = new Formatter();
            _nullHandler           = new NullHandler();
            _replacementApplicator = new ReplacementApplicator();
        }

        /// <summary>
        /// Compiles the specified configuration into an executable <see cref="ExecutionPlan"/>.
        /// </summary>
        /// <param name="config">The parser configuration to compile.</param>
        /// <param name="sourceType">The optional source input type.</param>
        /// <returns>A compiled <see cref="ExecutionPlan"/>.</returns>
        /// <exception cref="AggregateValidationException">Thrown if configuration validation fails with errors.</exception>
        public ExecutionPlan Compile(ParserConfiguration config, Type? sourceType = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var validationResult = ConfigurationValidator.Validate(
                config, sourceType, _resolver, _engine, _registry);

            if (!validationResult.IsValid)
                throw new AggregateValidationException(validationResult.Errors);

            var allColumns = new List<ColumnDefinition>(config.Columns.Count);
            foreach (var col in config.Columns)
                if (col != null) allColumns.Add(col);

            if (config.AutoSortByOrder)
                allColumns = allColumns.OrderBy(a => a.Order).ToList();

            int total = allColumns.Count;

            var nameToIndex = BuildNameIndex(allColumns);
            var dependencyIndexes = BuildDependencyIndexes(allColumns, nameToIndex);

            var enabledColumns = allColumns.FindAll(c => c.IsEnabled);
            var enabledIndexes = new HashSet<int>();
            foreach (var col in enabledColumns)
                enabledIndexes.Add(nameToIndex.TryGetValue(col.Name, out var idx) ? idx : -1);

            var topoOrder = TopologicalSort(allColumns, enabledColumns, dependencyIndexes);

            var typeInferrer = new TypeInferrer(_resolver, _engine, _registry, allColumns, nameToIndex);
            typeInferrer.InferAll(sourceType);

            if (typeInferrer.TypeGraph.TypeErrors.Count > 0)
                throw new AggregateValidationException(typeInferrer.TypeGraph.TypeErrors);

            var reverseDeps = BuildReverseDependencies(total, dependencyIndexes);

            var variablesList = config.Variables != null ? config.Variables.ToList() : new List<KeyValuePair<string, object?>>();
            var compiledVariables = new object?[variablesList.Count];
            var variableIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < variablesList.Count; i++)
            {
                compiledVariables[i] = variablesList[i].Value;
                variableIndexes[variablesList[i].Key] = i;
            }

            var nodeByIndex = new ExecutionNode[total];
            for (int i = 0; i < allColumns.Count; i++)
            {
                var col   = allColumns[i];
                var deps  = dependencyIndexes[i];
                var kind  = ClassifyColumn(col);
                var del   = CompileDelegate(col, i, deps, variableIndexes, col.OutputType, allColumns, sourceType, nameToIndex, compiledVariables);

                nodeByIndex[i] = new ExecutionNode(
                    columnId:            col.Id,
                    columnName:          col.DisplayName,
                    columnIndex:         i,
                    isEnabled:          col.IsEnabled,
                    outputType:         col.OutputType,
                    separatorOverride:  col.SeparatorOverride,
                    formatString:       col.Formatting.FormatString,
                    dependencyIndexes:  deps,
                    reverseDependencies: reverseDeps[i],
                    execute:            del,
                    kind:               kind);
            }

            var evaluationOrder = topoOrder
                .Select(col => nodeByIndex[nameToIndex.TryGetValue(col.Name, out var idx) ? idx : IndexOf(allColumns, col)])
                .ToList()
                .AsReadOnly();

            IReadOnlyList<ExecutionNode> assemblyOrder;
            if (config.PreserveSeparatorForDisabledColumns)
                assemblyOrder = allColumns.Select(col => nodeByIndex[IndexOf(allColumns, col)]).ToList().AsReadOnly();
            else
                assemblyOrder = enabledColumns.Select(col => nodeByIndex[IndexOf(allColumns, col)]).ToList().AsReadOnly();

            Func<object?, System.Text.StringBuilder, string>? fastRowExecutor = null;
            if (sourceType != null)
            {
                fastRowExecutor = SpecializedPipelineBuilder.TryBuild(
                    config,
                    sourceType,
                    evaluationOrder,
                    assemblyOrder,
                    allColumns,
                    nameToIndex,
                    variableIndexes,
                    compiledVariables,
                    _registry);
            }

            return new ExecutionPlan(
                evaluationOrder:                     evaluationOrder,
                assemblyOrder:                       assemblyOrder,
                globalSeparator:                     config.GlobalSeparator ?? string.Empty,
                preserveSeparatorForDisabledColumns: config.PreserveSeparatorForDisabledColumns,
                totalColumnCount:                    total > 0 ? total : 1,
                compiledVariables:                   compiledVariables,
                variableIndexes:                     variableIndexes,
                fastRowExecutor:                     fastRowExecutor);
        }

        private static ColumnKind ClassifyColumn(ColumnDefinition col) => col switch
        {
            FixedTextColumn     => ColumnKind.FixedText,
            PropertyColumn      => ColumnKind.Property,
            FormulaColumn       => ColumnKind.Formula,
            ConditionalColumn   => ColumnKind.Conditional,
            CustomFunctionColumn=> ColumnKind.CustomFunction,
            _                   => ColumnKind.Unknown
        };

        private static Dictionary<string, int> BuildNameIndex(List<ColumnDefinition> columns)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                if (!string.IsNullOrWhiteSpace(col.Name))  map[col.Name]  = i;
                if (!string.IsNullOrWhiteSpace(col.Alias)) map[col.Alias!] = i;
            }
            return map;
        }

        private static IReadOnlyList<int>[] BuildDependencyIndexes(
            List<ColumnDefinition> allColumns,
            Dictionary<string, int> nameToIndex)
        {
            var result = new IReadOnlyList<int>[allColumns.Count];

            for (int i = 0; i < allColumns.Count; i++)
            {
                var refs = GetColumnReferences(allColumns[i]);
                if (refs.Count == 0)
                {
                    result[i] = Array.Empty<int>();
                    continue;
                }

                var indexes = new List<int>(refs.Count);
                foreach (var name in refs)
                {
                    if (nameToIndex.TryGetValue(name, out var depIdx))
                        indexes.Add(depIdx);
                }
                result[i] = indexes.Count > 0
                    ? indexes.AsReadOnly()
                    : (IReadOnlyList<int>)Array.Empty<int>();
            }

            return result;
        }

        private static IReadOnlyList<string> GetColumnReferences(ColumnDefinition col)
        {
            switch (col)
            {
                case FormulaColumn fc:
                    return ColumnReferenceParser.ExtractColumnReferences(fc.Expression);

                case ConditionalColumn cc:
                {
                    var all = new List<string>();
                    all.AddRange(ColumnReferenceParser.ExtractColumnReferences(cc.ConditionExpression));
                    all.AddRange(ColumnReferenceParser.ExtractColumnReferences(cc.TrueValue));
                    all.AddRange(ColumnReferenceParser.ExtractColumnReferences(cc.FalseValue));
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var unique = new List<string>(all.Count);
                    foreach (var s in all) if (seen.Add(s)) unique.Add(s);
                    return unique.AsReadOnly();
                }

                case CustomFunctionColumn cfc:
                {
                    var all = new List<string>();
                    foreach (var p in cfc.Parameters)
                        all.AddRange(ColumnReferenceParser.ExtractColumnReferences(p));
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var unique = new List<string>(all.Count);
                    foreach (var s in all) if (seen.Add(s)) unique.Add(s);
                    return unique.AsReadOnly();
                }

                default:
                    return Array.Empty<string>();
            }
        }

        private static List<ColumnDefinition> TopologicalSort(
            List<ColumnDefinition> allColumns,
            List<ColumnDefinition> enabledColumns,
            IReadOnlyList<int>[]  depIndexes)
        {
            var result = new List<ColumnDefinition>(enabledColumns.Count);
            var color  = new byte[allColumns.Count];
            var posMap = new Dictionary<ColumnDefinition, int>(enabledColumns.Count);
            for (int i = 0; i < allColumns.Count; i++) posMap[allColumns[i]] = i;

            foreach (var col in enabledColumns)
            {
                int pos = posMap[col];
                if (color[pos] == 0)
                    DfsVisit(col, pos, allColumns, posMap, depIndexes, color, result);
            }

            return result;
        }

        private static void DfsVisit(
            ColumnDefinition        col,
            int                     pos,
            List<ColumnDefinition>  allColumns,
            Dictionary<ColumnDefinition, int> posMap,
            IReadOnlyList<int>[]   depIndexes,
            byte[]                  color,
            List<ColumnDefinition>  result)
        {
            if (color[pos] == 2) return;

            if (color[pos] == 1)
                throw new CircularDependencyException(new List<string> { col.DisplayName });

            color[pos] = 1;

            foreach (var depIdx in depIndexes[pos])
            {
                if (depIdx < 0 || depIdx >= allColumns.Count) continue;
                var dep = allColumns[depIdx];
                if (!dep.IsEnabled) continue;
                int depPos = posMap.TryGetValue(dep, out var p) ? p : -1;
                if (depPos >= 0) DfsVisit(dep, depPos, allColumns, posMap, depIndexes, color, result);
            }

            color[pos] = 2;
            result.Add(col);
        }

        private static IReadOnlyList<int>[] BuildReverseDependencies(
            int total, IReadOnlyList<int>[] depIndexes)
        {
            var reverse = new List<int>[total];
            for (int i = 0; i < total; i++) reverse[i] = new List<int>();

            for (int i = 0; i < depIndexes.Length; i++)
            {
                foreach (var dep in depIndexes[i])
                {
                    if (dep >= 0 && dep < total)
                        reverse[dep].Add(i);
                }
            }

            var result = new IReadOnlyList<int>[total];
            for (int i = 0; i < total; i++)
                result[i] = reverse[i].AsReadOnly();
            return result;
        }

        private Func<ExecutionContext, object?> CompileDelegate(
            ColumnDefinition        col,
            int                     colIdx,
            IReadOnlyList<int>      deps,
            IReadOnlyDictionary<string, int> variableIndexes,
            Type                    outputType,
            List<ColumnDefinition>  allColumns,
            Type?                   sourceType,
            IReadOnlyDictionary<string, int> nameToIndex,
            IReadOnlyList<object?>  variableValues)
        {
            switch (col)
            {
                case FixedTextColumn ft:
                    return CompileFixedText(ft, colIdx, outputType);

                case PropertyColumn pc:
                    return CompileProperty(pc, colIdx, outputType, sourceType);

                case FormulaColumn fc:
                    return CompileFormula(fc, colIdx, deps, variableIndexes, outputType, allColumns, sourceType, nameToIndex, variableValues);

                case ConditionalColumn cc:
                    return CompileConditional(cc, colIdx, deps, variableIndexes, outputType, allColumns, sourceType, nameToIndex, variableValues);

                case CustomFunctionColumn cfc:
                    return CompileCustomFunction(cfc, colIdx, deps, variableIndexes, outputType, allColumns, sourceType);

                default:
                    throw new InvalidOperationException(
                        $"No delegate compiler for column type '{col.GetType().Name}'.");
            }
        }

        private Func<ExecutionContext, object?> CompileFixedText(
            FixedTextColumn ft, int colIdx, Type outputType)
        {
            var text = ft.Text ?? string.Empty;
            return WrapWithPostProcessing(ft, colIdx, colIdx,
                _ => text,
                outputType,
                ft.NullHandling, ft.Formatting, ft.ReplacementRules);
        }

        private Func<ExecutionContext, object?> CompileProperty(
            PropertyColumn pc, int colIdx, Type outputType, Type? sourceType)
        {
            var path     = pc.PropertyPath;
            var resolver = _resolver;
            var srcType  = sourceType;
            var getter   = srcType != null ? PropertyAccessorBuilder.BuildGetter(srcType, path) : null;

            return WrapWithPostProcessing(pc, colIdx, colIdx,
                ctx =>
                {
                    var src = ctx.Source;
                    if (src == null) return null;

                    if (getter != null)
                    {
                        try
                        {
                            return getter(src);
                        }
                        catch { return null; }
                    }

                    try
                    {
                        return resolver.Resolve(src, path);
                    }
                    catch { return null; }
                },
                outputType,
                pc.NullHandling, pc.Formatting, pc.ReplacementRules);
        }

        private Func<ExecutionContext, object?> CompileFormula(
            FormulaColumn          fc,
            int                    colIdx,
            IReadOnlyList<int>     deps,
            IReadOnlyDictionary<string, int> variableIndexes,
            Type                   outputType,
            List<ColumnDefinition> allColumns,
            Type?                  sourceType,
            IReadOnlyDictionary<string, int> nameToIndex,
            IReadOnlyList<object?> variableValues)
        {
            var expr   = fc.Expression;
            var engine = _engine;
            var depIdxs = deps;

            // Direct variable reference optimization: ${Var} -> ctx.Variables[vIdx]
            if (deps.Count == 0 && !string.IsNullOrEmpty(expr))
            {
                var trimmed = expr.Trim();
                if (trimmed.StartsWith("${") && trimmed.EndsWith("}"))
                {
                    var varName = trimmed.Substring(2, trimmed.Length - 3).Trim();
                    if (variableIndexes != null && variableIndexes.TryGetValue(varName, out int vIdx))
                    {
                        return WrapWithPostProcessing(fc, colIdx, colIdx,
                            ctx => ctx.Variables[vIdx],
                            outputType,
                            fc.NullHandling, fc.Formatting, fc.ReplacementRules);
                    }
                }
            }

            var compiled = engine.Capabilities.SupportsCompilation
                ? engine.CompileExpression(expr, sourceType, depIdxs, variableIndexes, nameToIndex, variableValues)
                : null;

            if (compiled != null)
            {
                return WrapWithPostProcessing(fc, colIdx, colIdx,
                    ctx => compiled(ctx),
                    outputType,
                    fc.NullHandling, fc.Formatting, fc.ReplacementRules);
            }

            return WrapWithPostProcessing(fc, colIdx, colIdx,
                ctx =>
                {
                    var expanded = ExpandForExpression(expr, depIdxs, allColumns, ctx);
                    return engine.EvaluateRaw(expanded, ctx);
                },
                outputType,
                fc.NullHandling, fc.Formatting, fc.ReplacementRules);
        }

        private Func<ExecutionContext, object?> CompileConditional(
            ConditionalColumn      cc,
            int                    colIdx,
            IReadOnlyList<int>     deps,
            IReadOnlyDictionary<string, int> variableIndexes,
            Type                   outputType,
            List<ColumnDefinition> allColumns,
            Type?                  sourceType,
            IReadOnlyDictionary<string, int> nameToIndex,
            IReadOnlyList<object?> variableValues)
        {
            var condExpr = cc.ConditionExpression;
            var trueVal  = cc.TrueValue;
            var falseVal = cc.FalseValue;
            var engine   = _engine;
            var depIdxs  = deps;
            const string EXPR_PREFIX = "=";

            var compiledCond = engine.Capabilities.SupportsCompilation
                ? engine.CompileCondition(condExpr, sourceType, depIdxs, variableIndexes, nameToIndex, variableValues)
                : null;

            bool hasTrueExpr  = !string.IsNullOrEmpty(trueVal)  && trueVal.StartsWith(EXPR_PREFIX);
            bool hasFalseExpr = !string.IsNullOrEmpty(falseVal) && falseVal.StartsWith(EXPR_PREFIX);

            Func<IExecutionContext, object?>? trueCompiled  = hasTrueExpr
                ? (engine.Capabilities.SupportsCompilation
                    ? engine.CompileExpression(trueVal.Substring(1).Trim(), sourceType, depIdxs, variableIndexes, nameToIndex, variableValues)
                    : null)
                : (ctx => trueVal);

            Func<IExecutionContext, object?>? falseCompiled = hasFalseExpr
                ? (engine.Capabilities.SupportsCompilation
                    ? engine.CompileExpression(falseVal.Substring(1).Trim(), sourceType, depIdxs, variableIndexes, nameToIndex, variableValues)
                    : null)
                : (ctx => falseVal);

            if (compiledCond != null && trueCompiled != null && falseCompiled != null)
            {
                return WrapWithPostProcessing(cc, colIdx, colIdx,
                    ctx => compiledCond(ctx) ? trueCompiled(ctx) : falseCompiled(ctx),
                    outputType,
                    cc.NullHandling, cc.Formatting, cc.ReplacementRules);
            }

            return WrapWithPostProcessing(cc, colIdx, colIdx,
                ctx =>
                {
                    bool condition;
                    string trimmedCond = condExpr?.Trim() ?? string.Empty;
                    bool isDirectColumnRef = false;
                    int directDepIdx = -1;

                    if (depIdxs.Count == 1 && trimmedCond.StartsWith("${") && trimmedCond.EndsWith("}"))
                    {
                        var depCol = allColumns[depIdxs[0]];
                        if (string.Equals(trimmedCond, $"${{{depCol.Name}}}", StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(depCol.Alias) && string.Equals(trimmedCond, $"${{{depCol.Alias}}}", StringComparison.OrdinalIgnoreCase)))
                        {
                            isDirectColumnRef = true;
                            directDepIdx = depIdxs[0];
                        }
                    }

                    if (isDirectColumnRef)
                    {
                        var rawResult = ctx.GetResult(directDepIdx);
                        if (rawResult is bool b) condition = b;
                        else condition = Convert.ToBoolean(rawResult);
                    }
                    else if (depIdxs.Count == 0 && trimmedCond.StartsWith("${") && trimmedCond.EndsWith("}"))
                    {
                        var varName = trimmedCond.Substring(2, trimmedCond.Length - 3).Trim();
                        if (ctx.TryGetVariable(varName, out var varVal))
                        {
                            if (varVal is bool b) condition = b;
                            else condition = varVal != null && Convert.ToBoolean(varVal);
                        }
                        else
                        {
                            var expandedCond = ExpandForExpression(condExpr ?? string.Empty, depIdxs, allColumns, ctx);
                            condition = engine.EvaluateCondition(expandedCond, ctx);
                        }
                    }
                    else
                    {
                        var expandedCond = ExpandForExpression(condExpr ?? string.Empty, depIdxs, allColumns, ctx);
                        condition = engine.EvaluateCondition(expandedCond, ctx);
                    }

                    var branch = condition ? trueVal : falseVal;

                    if (!string.IsNullOrEmpty(branch) && branch.StartsWith(EXPR_PREFIX, StringComparison.Ordinal))
                    {
                        var expr = branch.Substring(1).Trim();
                        var expandedExpr = ExpandForExpression(expr, depIdxs, allColumns, ctx);
                        return engine.EvaluateRaw(expandedExpr, ctx);
                    }

                    return ExpandForText(branch ?? string.Empty, depIdxs, allColumns, ctx);
                },
                outputType,
                cc.NullHandling, cc.Formatting, cc.ReplacementRules);
        }

        private Func<ExecutionContext, object?> CompileCustomFunction(
            CustomFunctionColumn   cfc,
            int                    colIdx,
            IReadOnlyList<int>     deps,
            IReadOnlyDictionary<string, int> variableIndexes,
            Type                   outputType,
            List<ColumnDefinition> allColumns,
            Type?                  sourceType)
        {
            var funcName      = cfc.FunctionName;
            var paramTemplates = cfc.Parameters;
            var registry      = _registry;
            var depIdxs       = deps;
            var depCount      = depIdxs.Count;

            var meta = registry?.GetMetadata(funcName);

            if (meta != null && registry != null)
            {
                return WrapWithPostProcessing(cfc, colIdx, colIdx,
                    ctx =>
                    {
                        var resolvedParams = new string[paramTemplates.Count];
                        for (int i = 0; i < paramTemplates.Count; i++)
                        {
                            resolvedParams[i] = ExpandForText(paramTemplates[i], depIdxs, allColumns, ctx);
                        }
                        return registry!.InvokeRaw(funcName, ctx.Source, resolvedParams);
                    },
                    outputType,
                    cfc.NullHandling, cfc.Formatting, cfc.ReplacementRules);
            }

            return WrapWithPostProcessing(cfc, colIdx, colIdx,
                ctx =>
                {
                    var resolvedParams = new List<string>(paramTemplates.Count);
                    foreach (var tmpl in paramTemplates)
                    {
                        var expanded = ExpandForText(tmpl, depIdxs, allColumns, ctx);
                        resolvedParams.Add(expanded);
                    }
                    return registry!.InvokeRaw(funcName, ctx.Source, resolvedParams.AsReadOnly());
                },
                outputType,
                cfc.NullHandling, cfc.Formatting, cfc.ReplacementRules);
        }

        private Func<ExecutionContext, object?> WrapWithPostProcessing(
            ColumnDefinition                col,
            int                             tracingIdx,
            int                             colIdx,
            Func<ExecutionContext, object?> inner,
            Type                            outputType,
            Core.Models.NullHandlingConfig  nullHandling,
            Core.Models.FormattingOptions   formatting,
            IList<Core.Models.ReplacementRule>? replacementRules)
        {
            var nullHandler            = _nullHandler;
            var formatter              = _formatter;
            var replacementApplicator  = _replacementApplicator;
            var formatString           = formatting?.FormatString;

            bool isNullDefault = nullHandling == null ||
                (nullHandling.Strategy == Core.Enums.NullHandlingStrategy.EmptyString &&
                 string.IsNullOrEmpty(nullHandling.DefaultValue) &&
                 string.IsNullOrEmpty(nullHandling.ExceptionMessage));

            bool isFormattingNoOp = formatting == null ||
                (formatting.TrimBehavior == Core.Enums.TrimBehavior.None &&
                 formatting.CasingTransform == Core.Enums.CasingTransform.None &&
                 formatting.MaxLength <= 0 &&
                 formatting.PaddedWidth <= 0 &&
                 string.IsNullOrEmpty(formatting.Prefix) &&
                 string.IsNullOrEmpty(formatting.Suffix));

            bool hasReplacements = replacementRules != null && replacementRules.Count > 0;

            if (isNullDefault && isFormattingNoOp && !hasReplacements)
            {
                if (string.IsNullOrEmpty(formatString))
                {
                    return ctx =>
                    {
                        object? rawResult;
                        try
                        {
                            rawResult = inner(ctx);
                        }
                        catch (Exception ex) when (ex is not ColumnEvaluationException)
                        {
                            throw new ColumnEvaluationException(
                                col.Id, col.Name,
                                $"Delegate execution failed: {ex.Message}", ex);
                        }

                        string final;
                        if (rawResult == null)
                            final = string.Empty;
                        else if (rawResult is string s)
                            final = s;
                        else if (rawResult is IFormattable f)
                            final = f.ToString(null, CultureInfo.CurrentCulture);
                        else
                            final = rawResult.ToString() ?? string.Empty;

                        ctx.SetResult(colIdx, rawResult, final);
                        return rawResult;
                    };
                }
                else
                {
                    return ctx =>
                    {
                        object? rawResult;
                        try
                        {
                            rawResult = inner(ctx);
                        }
                        catch (Exception ex) when (ex is not ColumnEvaluationException)
                        {
                            throw new ColumnEvaluationException(
                                col.Id, col.Name,
                                $"Delegate execution failed: {ex.Message}", ex);
                        }

                        string final = rawResult == null
                            ? string.Empty
                            : (rawResult is IFormattable f
                                ? f.ToString(formatString, CultureInfo.CurrentCulture)
                                : (rawResult.ToString() ?? string.Empty));

                        ctx.SetResult(colIdx, rawResult, final);
                        return rawResult;
                    };
                }
            }

            return ctx =>
            {
                object? rawResult;
                try
                {
                    rawResult = inner(ctx);
                }
                catch (Exception ex) when (ex is not ColumnEvaluationException)
                {
                    throw new ColumnEvaluationException(
                        col.Id, col.Name,
                        $"Delegate execution failed: {ex.Message}", ex);
                }

                string? rawString = rawResult == null
                    ? null
                    : ExecutionContext.FormatValue(rawResult, formatString);

                var handled = nullHandler.Handle(rawString, col);
                if (handled == null)
                {
                    ctx.MarkSkipped(colIdx);
                    return null;
                }

                var formatted = formatter.Apply(handled, col);
                var final    = replacementApplicator.Apply(formatted, replacementRules);

                ctx.SetResult(colIdx, rawResult, final);
                return rawResult;
            };
        }

        private static string ExpandForExpression(
            string                    expression,
            IReadOnlyList<int>        depIndexes,
            List<ColumnDefinition>    allColumns,
            ExecutionContext           ctx)
        {
            if (string.IsNullOrEmpty(expression) || !expression.Contains("${"))
                return expression ?? string.Empty;

            var values = new Dictionary<string, string>(
                depIndexes.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var depIdx in depIndexes)
            {
                if (depIdx < 0 || depIdx >= allColumns.Count) continue;
                var depCol = allColumns[depIdx];
                var rawVal = ctx.GetResult(depIdx);
                var formattedVal = FormatLiteral(rawVal);

                if (!string.IsNullOrWhiteSpace(depCol.Name))   values[depCol.Name]  = formattedVal;
                if (!string.IsNullOrWhiteSpace(depCol.Alias))  values[depCol.Alias!] = formattedVal;
            }

            var refs = ColumnReferenceParser.ExtractColumnReferences(expression);
            foreach (var r in refs)
            {
                if (!values.ContainsKey(r) && ctx.TryGetVariable(r, out _))
                {
                    // Bind parameter directly in expression engine:
                    // In NCalc, [Name] is a parameter that triggers EvaluateParameter(Name),
                    // which retrieves the typed value from ctx.TryGetVariable without loss of type or unsafe text substitution.
                    values[r] = $"[{r}]";
                }
            }

            return ColumnReferenceParser.ExpandColumnReferences(expression, values);
        }

        private static string ExpandForText(
            string                    text,
            IReadOnlyList<int>        depIndexes,
            List<ColumnDefinition>    allColumns,
            ExecutionContext           ctx)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains("${"))
                return text ?? string.Empty;

            var values = new Dictionary<string, string>(
                depIndexes.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var depIdx in depIndexes)
            {
                if (depIdx < 0 || depIdx >= allColumns.Count) continue;
                var depCol = allColumns[depIdx];
                var formattedVal = ctx.GetFormatted(depIdx);

                if (!string.IsNullOrWhiteSpace(depCol.Name))   values[depCol.Name]  = formattedVal;
                if (!string.IsNullOrWhiteSpace(depCol.Alias))  values[depCol.Alias!] = formattedVal;
            }

            var refs = ColumnReferenceParser.ExtractColumnReferences(text);
            foreach (var r in refs)
            {
                if (!values.ContainsKey(r) && ctx.TryGetVariable(r, out var varVal))
                {
                    values[r] = varVal?.ToString() ?? string.Empty;
                }
            }

            return ColumnReferenceParser.ExpandColumnReferences(text, values);
        }

        /// <summary>
        /// Formats a value as a valid NCalc expression literal with proper escaping.
        /// </summary>
        public static string FormatLiteral(object? value)
        {
            if (value == null) return "null";
            if (value is bool b) return b ? "true" : "false";
            if (value is string s) return "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
            if (value is char c) return "'" + (c == '\'' ? "\\'" : c == '\\' ? "\\\\" : c.ToString()) + "'";
            if (value is byte or sbyte or short or ushort or int or uint or long or ulong)
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            if (value is float or double or decimal)
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            if (value is DateTime dt)
                return "#" + dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "#";
            if (value is Guid)
                return "'" + value.ToString() + "'";
            return "'" + value.ToString()?.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
        }

        private static int IndexOf(List<ColumnDefinition> list, ColumnDefinition col)
        {
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], col)) return i;
            return -1;
        }
    }
}
