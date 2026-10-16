namespace ColumnWeaver.NET.Core.Enums
{
    /// <summary>
    /// Specifies how whitespace trimming is applied to formatted output.
    /// </summary>
    public enum TrimBehavior
    {
        /// <summary>
        /// No trimming is applied.
        /// </summary>
        None = 0,

        /// <summary>
        /// Trim leading whitespace only.
        /// </summary>
        TrimStart = 1,

        /// <summary>
        /// Trim trailing whitespace only.
        /// </summary>
        TrimEnd = 2,

        /// <summary>
        /// Trim both leading and trailing whitespace.
        /// </summary>
        TrimBoth = 3
    }
}
