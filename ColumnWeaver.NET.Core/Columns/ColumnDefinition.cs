using System;
using System.Collections.Generic;

namespace ColumnWeaver.NET.Core.Columns
{
    /// <summary>
    /// Abstract base class representing the definition of a single column
    /// in the parser pipeline. Each derived type contributes a distinct
    /// evaluation strategy to produce a string fragment of the final output.
    /// </summary>
    /// <remarks>
    /// Column definitions are pure configuration objects. They carry no evaluation
    /// logic; evaluation is delegated to <see cref="Interfaces.IColumnEvaluator"/>.
    /// </remarks>
    public abstract class ColumnDefinition
    {
        /// <summary>
        /// Gets or sets the unique identifier for this column.
        /// Must be unique within a <see cref="ParserConfiguration.ParserConfiguration"/>.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the logical name of this column, used for display,
        /// logging, and diagnostic purposes.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional alias for this column. When set, the alias
        /// takes precedence over <see cref="Name"/> in output headers or labels.
        /// </summary>
        public string? Alias { get; set; }

        /// <summary>
        /// Gets or sets the zero-based ordinal position of this column in
        /// the evaluation pipeline. Lower values are processed first.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this column participates
        /// in evaluation. Disabled columns are completely skipped.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets, an optional separator appended after this column's output.
        /// When set, overrides the global separator defined in
        /// <see cref="ParserConfiguration.ParserConfiguration.GlobalSeparator"/>.
        /// </summary>
        public string? SeparatorOverride { get; set; }

        /// <summary>
        /// Gets or sets the null-handling configuration for this column.
        /// Defaults to outputting an empty string on null.
        /// </summary>
        public Models.NullHandlingConfig NullHandling { get; set; } = new Models.NullHandlingConfig();

        /// <summary>
        /// Gets or sets the formatting options applied to this column's resolved value.
        /// </summary>
        public Models.FormattingOptions Formatting { get; set; } = new Models.FormattingOptions();

        /// <summary>
        /// Gets or sets the ordered list of replacement rules applied after formatting.
        /// Rules are executed sequentially in list order.
        /// </summary>
        public IList<Models.ReplacementRule> ReplacementRules { get; set; } = new List<Models.ReplacementRule>();

        /// <summary>
        /// Returns the effective display name of this column,
        /// which is <see cref="Alias"/> when available, otherwise <see cref="Name"/>.
        /// </summary>
        public string DisplayName => !string.IsNullOrWhiteSpace(Alias) ? Alias! : Name;

        // ── V2: Strong Typing ──────────────────────────────────────────────

        /// <summary>
        /// Gets or sets the CLR type that this column's evaluation delegate
        /// will produce <b>before</b> string conversion.
        /// </summary>
        /// <remarks>
        /// Populated by <c>ExecutionPlanCompiler</c> during compilation:
        /// <list type="bullet">
        ///   <item><see cref="ColumnWeaver.NET.Core.Columns.PropertyColumn"/> →
        ///     inferred from the property type via
        ///     <c>IPropertyResolver.ResolveType</c>.</item>
        ///   <item><see cref="ColumnWeaver.NET.Core.Columns.FormulaColumn"/> →
        ///     inferred by the expression engine when it supports typing;
        ///     otherwise <c>typeof(object)</c>.</item>
        ///   <item><see cref="ColumnWeaver.NET.Core.Columns.ConditionalColumn"/> →
        ///     set to the common branch type when both branches agree;
        ///     otherwise <c>typeof(object)</c>.</item>
        ///   <item><see cref="ColumnWeaver.NET.Core.Columns.CustomFunctionColumn"/> →
        ///     taken from <c>FunctionMetadata.ReturnType</c>.</item>
        ///   <item><see cref="ColumnWeaver.NET.Core.Columns.FixedTextColumn"/> →
        ///     always <c>typeof(string)</c>.</item>
        /// </list>
        /// Defaults to <c>typeof(object)</c> so all V1 column definitions are
        /// backward-compatible without any modification.
        /// </remarks>
        public Type OutputType { get; set; } = typeof(object);

        /// <summary>
        /// Creates a deep copy of this column definition.
        /// </summary>
        public abstract ColumnDefinition Clone();

        /// <summary>
        /// Copies base properties of this column definition to the specified target.
        /// </summary>
        protected void CopyBasePropertiesTo(ColumnDefinition target)
        {
            if (target == null) return;
            target.Id = this.Id;
            target.Name = this.Name;
            target.Alias = this.Alias;
            target.Order = this.Order;
            target.IsEnabled = this.IsEnabled;
            target.SeparatorOverride = this.SeparatorOverride;
            target.NullHandling = this.NullHandling?.Clone() ?? new Models.NullHandlingConfig();
            target.Formatting = this.Formatting?.Clone() ?? new Models.FormattingOptions();
            target.OutputType = this.OutputType;
            if (this.ReplacementRules != null)
            {
                foreach (var r in this.ReplacementRules)
                {
                    target.ReplacementRules.Add(r.Clone());
                }
            }
        }
    }
}
