namespace ColumnWeaver.NET.Core.Enums
{
    /// <summary>
    /// Defines how a column behaves when the resolved value is null.
    /// </summary>
    public enum NullHandlingStrategy
    {
        /// <summary>
        /// Output an empty string when the value is null.
        /// </summary>
        EmptyString = 0,

        /// <summary>
        /// Output a user-defined fallback/default value when the value is null.
        /// </summary>
        UseDefault = 1,

        /// <summary>
        /// Skip the column entirely when the value is null (omit from output).
        /// </summary>
        Skip = 2,

        /// <summary>
        /// Throw an exception when the value is null.
        /// </summary>
        ThrowException = 3,

        /// <summary>
        /// Output the literal string "NULL" when the value is null.
        /// </summary>
        LiteralNull = 4
    }
}
