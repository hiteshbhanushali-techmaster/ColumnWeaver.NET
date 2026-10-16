using System;
using System.Collections.Generic;

namespace ColumnWeaver.NET.Application.Execution
{
    /// <summary>
    /// The immutable, compiled representation of a single column inside an
    /// <see cref="ExecutionPlan"/>.
    /// </summary>
    /// <remarks>
    /// <b>Created by</b> <see cref="ExecutionPlanCompiler"/> — never by user code.<br/>
    ///
    /// <b>Hot-path contract:</b>
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="Execute"/> is a pre-compiled <c>Func&lt;ExecutionContext, object?&gt;</c>.
    ///     It MUST use only <c>context.Source</c> and <c>context.GetResult(int)</c>.
    ///     It MUST NOT call any factory, use reflection, or perform dictionary lookups.
    ///   </item>
    ///   <item>
    ///     <see cref="DependencyIndexes"/> stores integer positions into the plan's
    ///     node array — strings are never parsed at execution time.
    ///   </item>
    /// </list>
    ///
    /// <b>V2.5 additions:</b>
    /// <list type="bullet">
    ///   <item><see cref="ReverseDependencies"/> enables future parallel execution
    ///         by revealing which columns depend on this one.</item>
    ///   <item><see cref="ColumnKind"/> tags the node type for diagnostics.</item>
    /// </list>
    /// </remarks>
    public sealed class ExecutionNode
    {
        // -----------------------------------------------------------------
        //  Identity
        // -----------------------------------------------------------------

        public Guid ColumnId { get; }
        public string ColumnName { get; }
        public int ColumnIndex { get; }

        // -----------------------------------------------------------------
        //  Configuration
        // -----------------------------------------------------------------

        public bool IsEnabled { get; }
        public Type OutputType { get; }
        public string? SeparatorOverride { get; }
        public string? FormatString { get; }

        /// <summary>
        /// V2.5 — Gets the semantic kind of this column for diagnostics
        /// and future optimisation passes.
        /// </summary>
        public ColumnKind Kind { get; }

        // -----------------------------------------------------------------
        //  Dependency graph (int-indexed, NO strings at execution time)
        // -----------------------------------------------------------------

        /// <summary>
        /// Gets the zero-based indexes of all columns that must be evaluated
        /// before this node can execute.
        /// </summary>
        public IReadOnlyList<int> DependencyIndexes { get; }

        /// <summary>
        /// V2.5 — Gets the indexes of all columns that depend on this node.
        /// Used for future parallel execution and dependency analysis.
        /// </summary>
        public IReadOnlyList<int> ReverseDependencies { get; }

        // -----------------------------------------------------------------
        //  Compiled delegate
        // -----------------------------------------------------------------

        /// <summary>
        /// The pre-compiled evaluation delegate.
        /// </summary>
        public Func<ExecutionContext, object?> Execute { get; }

        // -----------------------------------------------------------------
        //  Constructor (internal — only ExecutionPlanCompiler creates nodes)
        // -----------------------------------------------------------------

        internal ExecutionNode(
            Guid                           columnId,
            string                         columnName,
            int                            columnIndex,
            bool                           isEnabled,
            Type                           outputType,
            string?                        separatorOverride,
            string?                        formatString,
            IReadOnlyList<int>             dependencyIndexes,
            IReadOnlyList<int>             reverseDependencies,
            Func<ExecutionContext, object?> execute,
            ColumnKind                      kind)
        {
            ColumnId             = columnId;
            ColumnName           = columnName        ?? string.Empty;
            ColumnIndex          = columnIndex;
            IsEnabled            = isEnabled;
            OutputType           = outputType        ?? typeof(object);
            SeparatorOverride    = separatorOverride;
            FormatString         = formatString;
            DependencyIndexes    = dependencyIndexes ?? Array.Empty<int>();
            ReverseDependencies  = reverseDependencies ?? Array.Empty<int>();
            Execute              = execute           ?? throw new ArgumentNullException(nameof(execute));
            Kind                 = kind;
        }
    }

    /// <summary>
    /// V2.5 — Semantic classification of an <see cref="ExecutionNode"/>
    /// for diagnostics and future optimization.
    /// </summary>
    public enum ColumnKind
    {
        Unknown = 0,
        FixedText = 1,
        Property = 2,
        Formula = 3,
        Conditional = 4,
        CustomFunction = 5
    }
}
