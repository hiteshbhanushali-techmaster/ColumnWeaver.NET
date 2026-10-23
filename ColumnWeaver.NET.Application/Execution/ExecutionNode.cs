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

        /// <summary>
        /// Gets the unique identifier of the original column definition.
        /// </summary>
        public Guid ColumnId { get; }

        /// <summary>
        /// Gets the display name of the column.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// Gets the zero-based index of this node in the plan.
        /// </summary>
        public int ColumnIndex { get; }

        // -----------------------------------------------------------------
        //  Configuration
        // -----------------------------------------------------------------

        /// <summary>
        /// Gets a value indicating whether this column is enabled for output.
        /// </summary>
        public bool IsEnabled { get; }

        /// <summary>
        /// Gets the resolved output CLR type produced by this column.
        /// </summary>
        public Type OutputType { get; }

        /// <summary>
        /// Gets the column-specific separator override string, or <c>null</c> to use the global separator.
        /// </summary>
        public string? SeparatorOverride { get; }

        /// <summary>
        /// Gets the format string applied to the raw value of this column, or <c>null</c>.
        /// </summary>
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
        /// <summary>
        /// Unknown or unclassified column kind.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Fixed text or literal column value.
        /// </summary>
        FixedText = 1,

        /// <summary>
        /// Model property accessor column.
        /// </summary>
        Property = 2,

        /// <summary>
        /// Dynamic expression or formula column.
        /// </summary>
        Formula = 3,

        /// <summary>
        /// Conditional branch column.
        /// </summary>
        Conditional = 4,

        /// <summary>
        /// Custom registered function invocation column.
        /// </summary>
        CustomFunction = 5
    }
}
