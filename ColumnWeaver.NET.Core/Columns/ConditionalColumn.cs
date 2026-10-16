namespace ColumnWeaver.NET.Core.Columns
{
    /// <summary>
    /// Evaluates a boolean condition against the input object and outputs one of two
    /// pre-configured values depending on the result.
    /// </summary>
    /// <remarks>
    /// The condition expression is evaluated by the configured
    /// <see cref="Interfaces.IExpressionEngine"/>. When the condition evaluates to
    /// <c>true</c>, <see cref="TrueValue"/> is output; otherwise, <see cref="FalseValue"/>.
    /// Both branch values may themselves be literal strings or nested dynamic expressions,
    /// subject to the expression engine's capabilities.
    /// </remarks>
    public sealed class ConditionalColumn : ColumnDefinition
    {
        /// <summary>
        /// Gets or sets the boolean expression evaluated against the source object.
        /// The expression syntax is determined by the configured <see cref="Interfaces.IExpressionEngine"/>.
        /// Cannot be null or empty.
        /// </summary>
        public string ConditionExpression { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the output value when <see cref="ConditionExpression"/> evaluates to true.
        /// May be a literal string or a dynamic expression, depending on the expression engine.
        /// </summary>
        public string TrueValue { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the output value when <see cref="ConditionExpression"/> evaluates to false.
        /// May be a literal string or a dynamic expression, depending on the expression engine.
        /// </summary>
        public string FalseValue { get; set; } = string.Empty;

        /// <inheritdoc />
        public override ColumnDefinition Clone()
        {
            var copy = new ConditionalColumn
            {
                ConditionExpression = this.ConditionExpression,
                TrueValue = this.TrueValue,
                FalseValue = this.FalseValue
            };
            CopyBasePropertiesTo(copy);
            return copy;
        }
    }
}
