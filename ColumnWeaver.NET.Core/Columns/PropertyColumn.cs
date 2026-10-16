namespace ColumnWeaver.NET.Core.Columns
{
    /// <summary>
    /// Resolves a value by navigating a dot-separated property path on the input object
    /// (e.g., "Order.Customer.Name").
    /// </summary>
    /// <remarks>
    /// Path resolution is handled at runtime by an <see cref="Interfaces.IPropertyResolver"/>.
    /// Supports nested properties, indexers, and dictionary access depending on the resolver
    /// implementation used.
    /// </remarks>
    public sealed class PropertyColumn : ColumnDefinition
    {
        /// <summary>
        /// Gets or sets the dot-separated path to the property on the source object.
        /// Supports nested navigation (e.g., "Address.City", "Orders[0].Id").
        /// Cannot be null or empty.
        /// </summary>
        public string PropertyPath { get; set; } = string.Empty;

        /// <inheritdoc />
        public override ColumnDefinition Clone()
        {
            var copy = new PropertyColumn { PropertyPath = this.PropertyPath };
            CopyBasePropertiesTo(copy);
            return copy;
        }
    }
}
