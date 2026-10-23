using System;
using System.Collections.Generic;

namespace ColumnWeaver.NET.Application.Execution
{
    /// <summary>
    /// The immutable, compiled representation of a complete parser pipeline.
    /// </summary>
    /// <remarks>
    /// <b>Lifetime:</b> Created once by <see cref="ExecutionPlanCompiler.Compile"/>
    /// and cached in <c>CompiledParserEngine</c>. It is reused across all
    /// <c>Parse</c> and <c>ParseMany</c> calls for the same configuration.
    ///
    /// <b>Thread safety:</b> <see cref="ExecutionPlan"/> itself is immutable and
    /// fully thread-safe. Each concurrent parse call must obtain its own
    /// <see cref="ExecutionContext"/> via <see cref="CreateContext"/>.
    /// </remarks>
    public sealed class ExecutionPlan
    {
        // -----------------------------------------------------------------
        //  Evaluation order (topologically sorted)
        // -----------------------------------------------------------------

        /// <summary>
        /// Gets the nodes in dependency-resolved (topological) order.
        /// </summary>
        /// <remarks>
        /// Every node's <see cref="ExecutionNode.DependencyIndexes"/> will have
        /// been evaluated before the node itself is reached in this sequence.
        /// Contains only <b>enabled</b> columns.
        /// </remarks>
        public IReadOnlyList<ExecutionNode> EvaluationOrder { get; }

        // -----------------------------------------------------------------
        //  Assembly order (user-defined output order)
        // -----------------------------------------------------------------

        /// <summary>
        /// Gets the nodes in user-defined output order (by
        /// <see cref="Core.Columns.ColumnDefinition.Order"/>).
        /// </summary>
        /// <remarks>
        /// When <see cref="PreserveSeparatorForDisabledColumns"/> is <c>true</c>,
        /// this list includes disabled columns (which contribute an empty string
        /// plus their separator slot). When <c>false</c>, only enabled columns
        /// appear here.
        /// </remarks>
        public IReadOnlyList<ExecutionNode> AssemblyOrder { get; }

        // -----------------------------------------------------------------
        //  Global settings (captured from ParserConfiguration at compile time)
        // -----------------------------------------------------------------

        /// <summary>
        /// Gets the global separator inserted between column outputs.
        /// Individual columns may override this via
        /// <see cref="ExecutionNode.SeparatorOverride"/>.
        /// </summary>
        public string GlobalSeparator { get; }

        /// <summary>
        /// Gets a value indicating whether disabled columns should still
        /// occupy a separator slot in the output.
        /// </summary>
        public bool PreserveSeparatorForDisabledColumns { get; }

        /// <summary>
        /// Gets the compiled specialized whole-row delegate, if the configuration and
        /// source type allow static compilation. Null when dynamic execution is required.
        /// </summary>
        public Func<object?, System.Text.StringBuilder, string>? FastRowExecutor { get; }

        /// <summary>
        /// Gets the compiled variable values for this execution plan.
        /// </summary>
        public object?[] CompiledVariables { get; }

        /// <summary>
        /// Gets the dictionary mapping variable names to their array index.
        /// </summary>
        public IReadOnlyDictionary<string, int> VariableIndexes { get; }

        /// <summary>
        /// Gets the total number of column slots — the size of the arrays
        /// inside each <see cref="ExecutionContext"/> created by this plan.
        /// </summary>
        public int TotalColumnCount { get; }

        // -----------------------------------------------------------------
        //  Constructor (internal)
        // -----------------------------------------------------------------

        /// <summary>
        /// Initializes a compiled <see cref="ExecutionPlan"/>.
        /// Called exclusively by <see cref="ExecutionPlanCompiler"/>.
        /// </summary>
        internal ExecutionPlan(
            IReadOnlyList<ExecutionNode> evaluationOrder,
            IReadOnlyList<ExecutionNode> assemblyOrder,
            string                       globalSeparator,
            bool                         preserveSeparatorForDisabledColumns,
            int                          totalColumnCount,
            object?[]?                   compiledVariables = null,
            IReadOnlyDictionary<string, int>? variableIndexes = null,
            Func<object?, System.Text.StringBuilder, string>? fastRowExecutor = null)
        {
            EvaluationOrder                    = evaluationOrder  ?? throw new ArgumentNullException(nameof(evaluationOrder));
            AssemblyOrder                      = assemblyOrder    ?? throw new ArgumentNullException(nameof(assemblyOrder));
            GlobalSeparator                    = globalSeparator  ?? string.Empty;
            PreserveSeparatorForDisabledColumns= preserveSeparatorForDisabledColumns;
            TotalColumnCount                   = totalColumnCount > 0
                ? totalColumnCount
                : throw new ArgumentOutOfRangeException(nameof(totalColumnCount));
            CompiledVariables                  = compiledVariables ?? Array.Empty<object?>();
            VariableIndexes                    = variableIndexes ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            FastRowExecutor                    = fastRowExecutor;
        }

        // -----------------------------------------------------------------
        //  Factory method
        // -----------------------------------------------------------------

        /// <summary>
        /// Creates a fresh <see cref="ExecutionContext"/> sized for this plan.
        /// </summary>
        /// <remarks>
        /// Call once per parse operation (or once per thread for concurrent use).
        /// In <c>ParseMany</c> scenarios, reuse the same context across iterations
        /// by calling <see cref="ExecutionContext.Reset(object?)"/> instead of creating
        /// a new context per item.
        /// </remarks>
        public ExecutionContext CreateContext() => new ExecutionContext(TotalColumnCount, CompiledVariables, VariableIndexes);
    }
}
