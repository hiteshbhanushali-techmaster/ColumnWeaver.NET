using System;
using System.Collections.Generic;
using ColumnWeaver.NET.Core.Columns;

namespace ColumnWeaver.NET.Core.ParserConfiguration
{
    /// <summary>
    /// Root configuration object for a single parser pipeline.
    /// Holds the ordered set of column definitions and global output settings.
    /// </summary>
    /// <remarks>
    /// A <see cref="ParserConfiguration"/> instance is passed to an
    /// <see cref="Interfaces.IColumnEvaluator"/> implementation to drive the
    /// serialisation of a source object into a final string.
    /// </remarks>
    public sealed class ParserConfiguration
    {
        /// <summary>
        /// Gets or sets the unique name identifying this configuration,
        /// used for logging and diagnostic purposes.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ordered list of column definitions that form the
        /// evaluation pipeline. Columns with <see cref="ColumnDefinition.IsEnabled"/>
        /// set to false are skipped at runtime.
        /// </summary>
        public IList<ColumnDefinition> Columns { get; set; } = new List<ColumnDefinition>();

        /// <summary>
        /// Gets or sets the default separator string inserted between column outputs.
        /// Individual columns may override this via
        /// <see cref="ColumnDefinition.SeparatorOverride"/>.
        /// Default is an empty string (no separator).
        /// </summary>
        public string GlobalSeparator { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether columns are sorted by
        /// <see cref="ColumnDefinition.Order"/> before evaluation.
        /// When false, columns are evaluated in list-insertion order.
        /// Default is true.
        /// </summary>
        public bool AutoSortByOrder { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether disabled columns should still
        /// consume their separator slot, preserving positional output structure.
        /// Default is false (gaps are collapsed).
        /// </summary>
        public bool PreserveSeparatorForDisabledColumns { get; set; } = false;

        private Dictionary<string, object?> _variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets a collection of read-only variables available during execution.
        /// Keys are case-insensitive.
        /// </summary>
        public Dictionary<string, object?> Variables
        {
            get => _variables;
            set => _variables = value != null
                ? new Dictionary<string, object?>(value, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates an independent deep snapshot of this configuration.
        /// </summary>
        public ParserConfiguration Snapshot() => Clone();

        /// <summary>
        /// Creates an independent deep copy of this configuration.
        /// </summary>
        public ParserConfiguration Clone()
        {
            var copy = new ParserConfiguration
            {
                Name = this.Name,
                GlobalSeparator = this.GlobalSeparator,
                AutoSortByOrder = this.AutoSortByOrder,
                PreserveSeparatorForDisabledColumns = this.PreserveSeparatorForDisabledColumns,
                Variables = this.Variables != null
                    ? new Dictionary<string, object?>(this.Variables, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            };

            if (this.Columns != null)
            {
                foreach (var col in this.Columns)
                {
                    copy.Columns.Add(col?.Clone()!);
                }
            }

            return copy;
        }
    }
}
