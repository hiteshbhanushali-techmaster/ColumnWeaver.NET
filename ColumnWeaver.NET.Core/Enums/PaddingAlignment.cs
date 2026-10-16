namespace ColumnWeaver.NET.Core.Enums
{
    /// <summary>
    /// Specifies the alignment direction when padding is applied to a column value.
    /// </summary>
    public enum PaddingAlignment
    {
        /// <summary>
        /// No padding is applied.
        /// </summary>
        None = 0,

        /// <summary>
        /// Pad the value on the left (right-align the content).
        /// </summary>
        Left = 1,

        /// <summary>
        /// Pad the value on the right (left-align the content).
        /// </summary>
        Right = 2,

        /// <summary>
        /// Pad both sides equally (center-align the content).
        /// </summary>
        Center = 3
    }
}
