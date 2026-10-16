using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColumnWeaver.NET.Application.Engine;
using ColumnWeaver.NET.Application.Exceptions;
using ColumnWeaver.NET.Application.Factory;
using ColumnWeaver.NET.Application.PostProcessing;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.ParserConfiguration;

namespace ColumnWeaver.NET.Application.Pipeline
{
    /// <summary>
    /// Orchestrates the two-phase evaluation pipeline for a single parse operation.
    /// </summary>
    /// <remarks>
    /// <b>Phase 1 — Evaluation:</b>
    /// All enabled columns are evaluated in dependency-resolved (topological) order.
    /// Results are stored in the <see cref="ColumnEvaluationContext"/>. Formula/conditional
    /// column cross-references (<c>${ColumnName}</c>) are expanded immediately before
    /// each column is evaluated, so every evaluator receives a fully-resolved input.
    ///
    /// <b>Phase 2 — Assembly:</b>
    /// Stored results are collected in the user-defined <c>Order</c> sequence and
    /// joined with per-column or global separators to produce the final string.
    ///
    /// <b>Post-processing per column (applied during Phase 1):</b>
    /// <list type="number">
    ///   <item>Null handling (NullHandler)</item>
    ///   <item>Formatting — trim, casing, padding, prefix/suffix (Formatter)</item>
    ///   <item>Replacement rules (ReplacementApplicator)</item>
    /// </list>
    /// </remarks>
    public sealed class ColumnPipeline
    {
        private readonly IColumnEvaluatorFactory _factory;
        private readonly NullHandler             _nullHandler;
        private readonly Formatter               _formatter;
        private readonly ReplacementApplicator   _replacementApplicator;

        // -----------------------------------------------------------------
        //  Constructor
        // -----------------------------------------------------------------

        /// <summary>
        /// Initializes the pipeline with the evaluator factory.
        /// Post-processing services are instantiated internally.
        /// </summary>
        public ColumnPipeline(IColumnEvaluatorFactory factory)
        {
            _factory               = factory ?? throw new ArgumentNullException(nameof(factory));
            _nullHandler           = new NullHandler();
            _formatter             = new Formatter();
            _replacementApplicator = new ReplacementApplicator();
        }

        // -----------------------------------------------------------------
        //  Public API
        // -----------------------------------------------------------------

        /// <summary>
        /// Runs both pipeline phases and returns the assembled output string.
        /// </summary>
        /// <param name="evaluationOrder">
        /// Columns in topologically-sorted order (dependencies before dependents).
        /// Contains only enabled columns.
        /// </param>
        /// <param name="assemblyOrder">
        /// Columns in user-defined output order. Includes disabled columns when
        /// <see cref="ParserConfiguration.PreserveSeparatorForDisabledColumns"/> is true.
        /// </param>
        /// <param name="context">
        /// The per-parse evaluation context for storing and sharing column results.
        /// </param>
        /// <param name="config">The parser configuration for global separator settings.</param>
        /// <returns>The fully assembled parser output string.</returns>
        public string Execute(
            IReadOnlyList<ColumnDefinition> evaluationOrder,
            IReadOnlyList<ColumnDefinition> assemblyOrder,
            ColumnEvaluationContext        context,
            ParserConfiguration            config)
        {
            if (evaluationOrder == null) throw new ArgumentNullException(nameof(evaluationOrder));
            if (assemblyOrder   == null) throw new ArgumentNullException(nameof(assemblyOrder));
            if (context         == null) throw new ArgumentNullException(nameof(context));
            if (config          == null) throw new ArgumentNullException(nameof(config));

            // ── Phase 1: Evaluate all enabled columns in dependency order ──────
            foreach (var column in evaluationOrder)
            {
                EvaluateAndStore(column, context);
            }

            // ── Phase 2: Assemble output in user-defined order ─────────────────
            return AssembleOutput(assemblyOrder, context, config);
        }

        // -----------------------------------------------------------------
        //  Phase 1 — Evaluation
        // -----------------------------------------------------------------

        /// <summary>
        /// Evaluates a single column, applies all post-processing, and stores
        /// the result in the context.
        /// </summary>
        private void EvaluateAndStore(ColumnDefinition column, ColumnEvaluationContext context)
        {
            // Belt-and-suspenders runtime cycle guard (DependencyResolver handles the
            // static case; this catches cycles arising from dynamic configurations).
            if (context.IsInProgress(column.Id))
                throw new ColumnEvaluationException(
                    column.Id, column.Name,
                    "Circular dependency detected at runtime. This column is already mid-evaluation.");

            // Already evaluated (e.g., was a dependency of a previously processed column).
            if (context.HasResult(column.Id) || context.IsSkipped(column.Id))
                return;

            context.BeginEvaluation(column);

            try
            {
                // ── Step A: Expand cross-column ${ColumnName} references ──────
                var expandedColumn = ExpandColumnReferences(column, context);

                // ── Step B: Evaluate raw value via the appropriate evaluator ──
                var evaluator = _factory.GetEvaluator(expandedColumn);
                string? rawValue;

                try
                {
                    rawValue = evaluator.Evaluate(expandedColumn, context);
                }
                catch (ColumnEvaluationException)
                {
                    throw; // Already wrapped with column identity — do not double-wrap.
                }
                catch (Exception ex)
                {
                    throw new ColumnEvaluationException(
                        column.Id, column.Name,
                        $"Unhandled exception during evaluation: {ex.Message}", ex);
                }

                // ── Step C: Apply null handling ───────────────────────────────
                // NullHandler returns null to signal "Skip this column".
                var handledValue = _nullHandler.Handle(rawValue, column);

                if (handledValue == null)
                {
                    // Strategy == Skip: exclude from output and separator logic.
                    context.MarkSkipped(column);
                    return;
                }

                // ── Step D: Apply formatting ──────────────────────────────────
                var formattedValue = _formatter.Apply(handledValue, column);

                // ── Step E: Apply replacement rules ───────────────────────────
                var finalValue = _replacementApplicator.Apply(formattedValue, column.ReplacementRules);

                context.CompleteEvaluation(column, finalValue);
            }
            catch
            {
                // Ensure in-progress state is always cleared to keep the context consistent.
                context.CancelEvaluation(column);
                throw;
            }
        }

        // -----------------------------------------------------------------
        //  Phase 2 — Assembly
        // -----------------------------------------------------------------

        /// <summary>
        /// Collects stored column results in <paramref name="assemblyOrder"/> and
        /// joins them with the configured separators.
        /// </summary>
        private static string AssembleOutput(
            IReadOnlyList<ColumnDefinition> assemblyOrder,
            ColumnEvaluationContext        context,
            ParserConfiguration            config)
        {
            // Filter to columns that will contribute to the output (not skipped).
            // Disabled columns are included only when PreserveSeparatorForDisabledColumns is true.
            var outputColumns = assemblyOrder
                .Where(col => !context.IsSkipped(col.Id))
                .ToList();

            if (outputColumns.Count == 0)
                return string.Empty;

            var sb        = new StringBuilder();
            int lastIndex = outputColumns.Count - 1;

            for (int i = 0; i < outputColumns.Count; i++)
            {
                var column = outputColumns[i];
                var isLast = i == lastIndex;

                // Disabled columns contribute an empty string (or their separator slot
                // when PreserveSeparatorForDisabledColumns=true, already handled by the
                // caller including them in assemblyOrder).
                context.TryGetResultById(column.Id, out var result);
                sb.Append(result ?? string.Empty);

                // Append separator after every column except the last.
                if (!isLast)
                {
                    var separator = column.SeparatorOverride ?? config.GlobalSeparator;
                    if (!string.IsNullOrEmpty(separator))
                        sb.Append(separator);
                }
            }

            return sb.ToString();
        }

        // -----------------------------------------------------------------
        //  Cross-column reference expansion helpers
        // -----------------------------------------------------------------

        /// <summary>
        /// Replaces <c>${ColumnName}</c> tokens in the column's expression fields
        /// with resolved values from the context. Returns a new (shallow-copied)
        /// column instance with expanded expressions, or the original if unchanged.
        /// </summary>
        private static ColumnDefinition ExpandColumnReferences(
            ColumnDefinition       column,
            ColumnEvaluationContext context)
        {
            var resolved = context.GetAllResults();
            var variables = context.Configuration.Variables;

            // No resolved values or variables — nothing to expand.
            if (resolved.Count == 0 && (variables == null || variables.Count == 0))
                return column;

            switch (column)
            {
                case FormulaColumn formulaCol:
                {
                    var expandedExpr = ColumnReferenceParser.ExpandForExpression(
                        formulaCol.Expression, resolved, variables);

                    // Return original if expression was unchanged (no allocations).
                    if (ReferenceEquals(expandedExpr, formulaCol.Expression) ||
                        expandedExpr == formulaCol.Expression)
                        return column;

                    return CloneWith(formulaCol, fc => fc.Expression = expandedExpr);
                }

                case ConditionalColumn condCol:
                {
                    var expandedCond  = ColumnReferenceParser.ExpandForExpression(condCol.ConditionExpression, resolved, variables);
                    var expandedTrue  = condCol.TrueValue != null && condCol.TrueValue.StartsWith("=")
                        ? ColumnReferenceParser.ExpandForExpression(condCol.TrueValue, resolved, variables)
                        : ColumnReferenceParser.ExpandForText(condCol.TrueValue, resolved, variables);
                    var expandedFalse = condCol.FalseValue != null && condCol.FalseValue.StartsWith("=")
                        ? ColumnReferenceParser.ExpandForExpression(condCol.FalseValue, resolved, variables)
                        : ColumnReferenceParser.ExpandForText(condCol.FalseValue, resolved, variables);

                    if (expandedCond  == condCol.ConditionExpression &&
                        expandedTrue  == condCol.TrueValue           &&
                        expandedFalse == condCol.FalseValue)
                        return column;

                    return CloneWith(condCol, cc =>
                    {
                        cc.ConditionExpression = expandedCond;
                        cc.TrueValue           = expandedTrue;
                        cc.FalseValue          = expandedFalse;
                    });
                }

                case CustomFunctionColumn funcCol:
                {
                    bool changed    = false;
                    var  expanded   = new List<string>(funcCol.Parameters.Count);

                    foreach (var param in funcCol.Parameters)
                    {
                        var expandedParam = ColumnReferenceParser.ExpandForText(param, resolved, variables);
                        expanded.Add(expandedParam);
                        if (expandedParam != param) changed = true;
                    }

                    if (!changed) return column;

                    return CloneWith(funcCol, cfc => cfc.Parameters = expanded);
                }

                default:
                    // PropertyColumn / FixedTextColumn have no cross-column references.
                    return column;
            }
        }

        // -----------------------------------------------------------------
        //  Shallow clone helpers (avoids mutating the original configuration)
        // -----------------------------------------------------------------

        /// <summary>
        /// Copies all nine <see cref="ColumnDefinition"/> base properties from
        /// <paramref name="src"/> to <paramref name="dst"/>.
        /// Called by each <c>CloneWith</c> overload to avoid repeating the same
        /// property assignments for every column type.
        /// </summary>
        private static void CopyBaseProperties(ColumnDefinition src, ColumnDefinition dst)
        {
            dst.Id                = src.Id;
            dst.Name              = src.Name;
            dst.Alias             = src.Alias;
            dst.Order             = src.Order;
            dst.IsEnabled         = src.IsEnabled;
            dst.SeparatorOverride = src.SeparatorOverride;
            dst.NullHandling      = src.NullHandling;
            dst.Formatting        = src.Formatting;
            dst.ReplacementRules  = src.ReplacementRules;
        }

        private static FormulaColumn CloneWith(FormulaColumn src, Action<FormulaColumn> mutate)
        {
            var clone = new FormulaColumn { Expression = src.Expression };
            CopyBaseProperties(src, clone);
            mutate(clone);
            return clone;
        }

        private static ConditionalColumn CloneWith(ConditionalColumn src, Action<ConditionalColumn> mutate)
        {
            var clone = new ConditionalColumn
            {
                ConditionExpression = src.ConditionExpression,
                TrueValue           = src.TrueValue,
                FalseValue          = src.FalseValue
            };
            CopyBaseProperties(src, clone);
            mutate(clone);
            return clone;
        }

        private static CustomFunctionColumn CloneWith(CustomFunctionColumn src, Action<CustomFunctionColumn> mutate)
        {
            var clone = new CustomFunctionColumn
            {
                FunctionName = src.FunctionName,
                Parameters   = new List<string>(src.Parameters)
            };
            CopyBaseProperties(src, clone);
            mutate(clone);
            return clone;
        }
    }
}
