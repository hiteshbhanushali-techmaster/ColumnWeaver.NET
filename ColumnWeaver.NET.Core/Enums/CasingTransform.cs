namespace ColumnWeaver.NET.Core.Enums
{
    /// <summary>
    /// Defines optional casing transformations applied to a column's string output.
    /// </summary>
    public enum CasingTransform
    {
        /// <summary>
        /// No casing transformation is applied.
        /// </summary>
        None = 0,

        /// <summary>
        /// Convert the entire value to uppercase.
        /// </summary>
        UpperCase = 1,

        /// <summary>
        /// Convert the entire value to lowercase.
        /// </summary>
        LowerCase = 2,

        /// <summary>
        /// Capitalize the first character of the value.
        /// </summary>
        TitleCase = 3,

        /// <summary>
        /// Capitalize the first character and lowercase the rest.
        /// </summary>
        SentenceCase = 4
    }
}
