namespace ColumnWeaver.NET.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for resolving the value of a dot-separated property path
    /// on a source object at runtime (used by <see cref="Columns.PropertyColumn"/>).
    /// </summary>
    /// <remarks>
    /// Implementations may use reflection, compiled expression trees, or any other
    /// mechanism to navigate the property graph. The Core layer imposes no constraint
    /// on the resolution strategy.
    /// Deep paths ("Order.Customer.Name"), collection indexing ("Items[0].Price"),
    /// and dictionary keys ("Metadata['region']") may be supported by individual
    /// implementations.
    /// </remarks>
    public interface IPropertyResolver
    {
        /// <summary>
        /// Resolves the value at the given <paramref name="propertyPath"/> on the
        /// <paramref name="source"/> object.
        /// </summary>
        /// <param name="source">
        /// The root source object to navigate. Must not be null.
        /// </param>
        /// <param name="propertyPath">
        /// A dot-separated path string identifying the target property
        /// (e.g., "Customer.Address.PostCode"). Must not be null or empty.
        /// </param>
        /// <returns>
        /// The resolved value, which may be null if the path leads to a null member
        /// or if an intermediate segment is null.
        /// </returns>
        object? Resolve(object source, string propertyPath);

        /// <summary>
        /// Determines whether the given <paramref name="propertyPath"/> is navigable
        /// on the specified <paramref name="sourceType"/> without invoking a resolution.
        /// </summary>
        /// <param name="sourceType">The <see cref="System.Type"/> of the source object.</param>
        /// <param name="propertyPath">The dot-separated path to validate.</param>
        /// <returns>
        /// <c>true</c> if all segments of the path exist and are readable;
        /// otherwise, <c>false</c>.
        /// </returns>
        bool CanResolve(System.Type sourceType, string propertyPath);

        // ── V2: Type inference ──────────────────────────────────────────────

        /// <summary>
        /// Returns the CLR <see cref="System.Type"/> of the property at the end of
        /// <paramref name="propertyPath"/> on <paramref name="sourceType"/>,
        /// without resolving an actual instance.
        /// </summary>
        /// <param name="sourceType">The root <see cref="System.Type"/> to inspect.</param>
        /// <param name="propertyPath">The dot-separated path to navigate.</param>
        /// <returns>
        /// The <see cref="System.Type"/> of the terminal property, or
        /// <c>null</c> when any segment of the path cannot be found on the type.
        /// </returns>
        /// <remarks>
        /// Used by <c>ExecutionPlanCompiler</c> to populate
        /// <see cref="Core.Columns.ColumnDefinition.OutputType"/> for
        /// <see cref="Core.Columns.PropertyColumn"/> definitions at compile time.
        /// A <c>null</c> return causes the compiler to fall back to
        /// <c>typeof(object)</c> rather than failing.
        /// </remarks>
        System.Type? ResolveType(System.Type sourceType, string propertyPath);
    }
}
