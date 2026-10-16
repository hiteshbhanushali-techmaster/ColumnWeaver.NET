using System;
using CoreColumns = ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Enums;
using ColumnWeaver.NET.Core.ParserConfiguration;

namespace ColumnWeaver.NET
{
    /// <summary>
    /// A fluent builder for creating <see cref="ParserConfiguration"/> instances.
    /// Provides an intuitive API for defining columns and their properties.
    /// </summary>
    /// <example>
    /// <code>
    /// var config = ParserConfigurationBuilder.Create()
    ///     .WithGlobalSeparator("|")
    ///     .AddProperty("Name", "Name")
    ///     .AddFormula("Total", "${Price} * ${Qty}", f => f.WithFormat("N2"))
    ///     .Build();
    /// </code>
    /// </example>
    public sealed class ParserConfigurationBuilder
    {
        private readonly ParserConfiguration _config = new ParserConfiguration();
        private int _order = 0;

        private ParserConfigurationBuilder() { }

        /// <summary>
        /// Creates a new <see cref="ParserConfigurationBuilder"/>.
        /// </summary>
        public static ParserConfigurationBuilder Create()
            => new ParserConfigurationBuilder();

        /// <summary>
        /// Sets the global separator inserted between column outputs.
        /// </summary>
        /// <param name="separator">The separator string.</param>
        public ParserConfigurationBuilder WithGlobalSeparator(string separator)
        {
            _config.GlobalSeparator = separator ?? string.Empty;
            return this;
        }

        /// <summary>
        /// Sets the configuration name for logging purposes.
        /// </summary>
        /// <param name="name">The configuration name.</param>
        public ParserConfigurationBuilder WithName(string name)
        {
            _config.Name = name ?? string.Empty;
            return this;
        }

        /// <summary>
        /// Sets whether columns should be sorted by their Order property.
        /// </summary>
        /// <param name="autoSort">True to auto-sort, false to use insertion order.</param>
        public ParserConfigurationBuilder WithAutoSort(bool autoSort)
        {
            _config.AutoSortByOrder = autoSort;
            return this;
        }

        /// <summary>
        /// Sets whether disabled columns should preserve their separator slot.
        /// </summary>
        /// <param name="preserve">True to preserve separators, false to collapse gaps.</param>
        public ParserConfigurationBuilder WithSeparatorPreservation(bool preserve)
        {
            _config.PreserveSeparatorForDisabledColumns = preserve;
            return this;
        }

        /// <summary>
        /// Adds a property column that resolves a value from the input object.
        /// </summary>
        /// <param name="name">The column display name.</param>
        /// <param name="propertyPath">The dot-separated path to the property.</param>
        /// <param name="configure">Optional action to configure additional column properties.</param>
        public ParserConfigurationBuilder AddProperty(
            string name,
            string propertyPath,
            Action<ColumnBuilder>? configure = null)
        {
            var col = new CoreColumns.PropertyColumn
            {
                Name         = name ?? string.Empty,
                PropertyPath = propertyPath ?? string.Empty,
                Order        = _order++
            };

            var builder = new ColumnBuilder(col);
            configure?.Invoke(builder);

            _config.Columns.Add(col);
            return this;
        }

        /// <summary>
        /// Adds a formula column that evaluates an expression.
        /// </summary>
        /// <param name="name">The column display name.</param>
        /// <param name="expression">The expression to evaluate.</param>
        /// <param name="configure">Optional action to configure additional column properties.</param>
        public ParserConfigurationBuilder AddFormula(
            string name,
            string expression,
            Action<ColumnBuilder>? configure = null)
        {
            var col = new CoreColumns.FormulaColumn
            {
                Name       = name ?? string.Empty,
                Expression = expression ?? string.Empty,
                Order      = _order++
            };

            var builder = new ColumnBuilder(col);
            configure?.Invoke(builder);

            _config.Columns.Add(col);
            return this;
        }

        /// <summary>
        /// Adds a conditional column that evaluates a condition and outputs one of two values.
        /// </summary>
        /// <param name="name">The column display name.</param>
        /// <param name="condition">The boolean condition expression.</param>
        /// <param name="trueValue">The value when condition is true.</param>
        /// <param name="falseValue">The value when condition is false.</param>
        /// <param name="configure">Optional action to configure additional column properties.</param>
        public ParserConfigurationBuilder AddConditional(
            string name,
            string condition,
            string trueValue,
            string falseValue,
            Action<ColumnBuilder>? configure = null)
        {
            var col = new CoreColumns.ConditionalColumn
            {
                Name                = name ?? string.Empty,
                ConditionExpression = condition ?? string.Empty,
                TrueValue           = trueValue ?? string.Empty,
                FalseValue          = falseValue ?? string.Empty,
                Order               = _order++
            };

            var builder = new ColumnBuilder(col);
            configure?.Invoke(builder);

            _config.Columns.Add(col);
            return this;
        }

        /// <summary>
        /// Adds a fixed text column that outputs a constant value.
        /// </summary>
        /// <param name="name">The column display name.</param>
        /// <param name="text">The constant text value.</param>
        /// <param name="configure">Optional action to configure additional column properties.</param>
        public ParserConfigurationBuilder AddFixedText(
            string name,
            string text,
            Action<ColumnBuilder>? configure = null)
        {
            var col = new CoreColumns.FixedTextColumn
            {
                Name  = name ?? string.Empty,
                Text  = text ?? string.Empty,
                Order = _order++
            };

            var builder = new ColumnBuilder(col);
            configure?.Invoke(builder);

            _config.Columns.Add(col);
            return this;
        }

        /// <summary>
        /// Builds the final <see cref="ParserConfiguration"/>.
        /// </summary>
        public ParserConfiguration Build() => _config;
    }

    /// <summary>
    /// Builder for configuring column properties like formatting, null handling, and separators.
    /// Used with <see cref="ParserConfigurationBuilder"/> method callbacks.
    /// </summary>
    public sealed class ColumnBuilder
    {
        private readonly CoreColumns.ColumnDefinition _column;

        internal ColumnBuilder(CoreColumns.ColumnDefinition column) => _column = column;

        /// <summary>
        /// Sets a separator to append after this column's output.
        /// </summary>
        public ColumnBuilder WithSeparator(string separator)
        {
            _column.SeparatorOverride = separator;
            return this;
        }

        /// <summary>
        /// Sets a format string (e.g., "N2", "yyyy-MM-dd") applied to the value.
        /// </summary>
        public ColumnBuilder WithFormat(string format)
        {
            _column.Formatting.FormatString = format;
            return this;
        }

        /// <summary>
        /// Sets a casing transformation.
        /// </summary>
        public ColumnBuilder WithCasing(CasingTransform casing)
        {
            _column.Formatting.CasingTransform = casing;
            return this;
        }

        /// <summary>
        /// Sets the maximum length of the output.
        /// </summary>
        public ColumnBuilder WithMaxLength(int maxLength)
        {
            _column.Formatting.MaxLength = maxLength;
            return this;
        }

        /// <summary>
        /// Sets fixed-width padding.
        /// </summary>
        public ColumnBuilder WithPadding(int width, PaddingAlignment alignment, char padChar = ' ')
        {
            _column.Formatting.PaddedWidth       = width;
            _column.Formatting.PaddingAlignment = alignment;
            _column.Formatting.PaddingChar      = padChar;
            return this;
        }

        /// <summary>
        /// Sets a prefix prepended to the value.
        /// </summary>
        public ColumnBuilder WithPrefix(string prefix)
        {
            _column.Formatting.Prefix = prefix;
            return this;
        }

        /// <summary>
        /// Sets a suffix appended to the value.
        /// </summary>
        public ColumnBuilder WithSuffix(string suffix)
        {
            _column.Formatting.Suffix = suffix;
            return this;
        }

        /// <summary>
        /// Sets null handling strategy and optional default value.
        /// </summary>
        public ColumnBuilder WhenNull(NullHandlingStrategy strategy, string? defaultValue = null)
        {
            _column.NullHandling.Strategy    = strategy;
            _column.NullHandling.DefaultValue = defaultValue;
            return this;
        }

        /// <summary>
        /// Disables this column so it is skipped during parsing.
        /// </summary>
        public ColumnBuilder Disabled()
        {
            _column.IsEnabled = false;
            return this;
        }
    }
}
