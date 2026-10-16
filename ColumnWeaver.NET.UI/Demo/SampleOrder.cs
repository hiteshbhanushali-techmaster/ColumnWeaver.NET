namespace ColumnWeaver.NET.UI.Demo
{
    /// <summary>
    /// Simple nested customer value used by <see cref="SampleOrder"/>.
    /// </summary>
    public sealed class SampleCustomer
    {
        public string Name { get; set; } = "ACME Corp";
        public string City { get; set; } = "New York";
    }

    /// <summary>
    /// A representative order domain object used to demonstrate every column type
    /// in the default parser configuration loaded at application startup.
    /// </summary>
    /// <remarks>
    /// <b>Column mapping:</b>
    /// <list type="table">
    ///   <item><term>OrderId</term><description>PropertyColumn</description></item>
    ///   <item><term>Customer.Name</term><description>PropertyColumn (nested)</description></item>
    ///   <item><term>UnitPrice * Quantity</term><description>FormulaColumn</description></item>
    ///   <item><term>IsActive</term><description>ConditionalColumn (bool condition)</description></item>
    ///   <item><term>Status</term><description>PropertyColumn</description></item>
    /// </list>
    /// <b>Note:</b> <see cref="UnitPrice"/> is <see langword="double"/> (not <c>decimal</c>)
    /// so that NCalc can perform arithmetic on it without type-coercion errors.
    /// </remarks>
    public sealed class SampleOrder
    {
        /// <summary>Unique order identifier.</summary>
        public int OrderId { get; set; } = 1001;

        /// <summary>Human-readable lifecycle status.</summary>
        public string Status { get; set; } = "Shipped";

        /// <summary>Whether the order is still active in the system.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Per-unit price. Declared as <c>double</c> for NCalc arithmetic compatibility.
        /// </summary>
        public double UnitPrice { get; set; } = 49.99;

        /// <summary>Number of units ordered.</summary>
        public int Quantity { get; set; } = 3;

        /// <summary>Destination date (shown with yyyy-MM-dd formatting).</summary>
        public System.DateTime OrderDate { get; set; } = new System.DateTime(2026, 4, 15);

        /// <summary>Nested customer reference — demonstrates dot-path property resolution.</summary>
        public SampleCustomer Customer { get; set; } = new SampleCustomer();
    }
}
