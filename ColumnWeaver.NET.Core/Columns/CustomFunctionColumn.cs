using System.Collections.Generic;

namespace ColumnWeaver.NET.Core.Columns
{
    /// <summary>
    /// Delegates evaluation to a named external or user-registered function,
    /// passing a configurable set of parameters alongside the source object.
    /// </summary>
    /// <remarks>
    /// This column type enables extensibility by allowing consumers to register
    /// named functions (via their infrastructure implementation) that encapsulate
    /// arbitrary logic without modifying the Core domain.
    /// </remarks>
    public sealed class CustomFunctionColumn : ColumnDefinition
    {
        /// <summary>
        /// Gets or sets the name of the registered function to invoke.
        /// The function registry is maintained outside the Core layer.
        /// Cannot be null or empty.
        /// </summary>
        public string FunctionName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an ordered list of string parameters passed to the function
        /// in addition to the source object. Parameters are interpreted by the
        /// function implementation.
        /// </summary>
        public IList<string> Parameters { get; set; } = new List<string>();

        /// <inheritdoc />
        public override ColumnDefinition Clone()
        {
            var copy = new CustomFunctionColumn
            {
                FunctionName = this.FunctionName,
                Parameters = this.Parameters != null ? new List<string>(this.Parameters) : new List<string>()
            };
            CopyBasePropertiesTo(copy);
            return copy;
        }
    }
}
