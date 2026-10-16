namespace ColumnWeaver.NET.Core.Columns
{
    /// <summary>
    /// Outputs a static, pre-configured text value regardless of the input object.
    /// Useful for injecting literal separators, labels, or constant values into the output.
    /// </summary>
    public sealed class FixedTextColumn : ColumnDefinition
    {
        /// <summary>
        /// Gets or sets the literal text value this column always outputs.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <inheritdoc />
        public override ColumnDefinition Clone()
        {
            var copy = new FixedTextColumn { Text = this.Text };
            CopyBasePropertiesTo(copy);
            return copy;
        }
    }
}
