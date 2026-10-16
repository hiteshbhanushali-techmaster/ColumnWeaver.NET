namespace ColumnWeaver.NET.Core.Columns
{
    /// <summary>
    /// Evaluates a dynamic expression against the input object to produce its output value.
    /// The expression syntax is defined and executed by the configured
    /// <see cref="Interfaces.IExpressionEngine"/>.
    /// </summary>
    /// <remarks>
    /// Example expressions might include arithmetic, string concatenation, or
    /// member access: "obj.Quantity * obj.UnitPrice", "obj.FirstName + ' ' + obj.LastName".
    /// </remarks>
    public sealed class FormulaColumn : ColumnDefinition
    {
        /// <summary>
        /// Gets or sets the expression string to be evaluated at runtime.
        /// The expression syntax is determined by the <see cref="Interfaces.IExpressionEngine"/>
        /// in use. Cannot be null or empty.
        /// </summary>
        public string Expression { get; set; } = string.Empty;

        /// <inheritdoc />
        public override ColumnDefinition Clone()
        {
            var copy = new FormulaColumn { Expression = this.Expression };
            CopyBasePropertiesTo(copy);
            return copy;
        }
    }
}
