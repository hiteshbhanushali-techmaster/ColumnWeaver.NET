using ColumnWeaver.NET.Core.Enums;

namespace ColumnWeaver.NET.Core.Models
{
    /// <summary>
    /// Encapsulates all output formatting options for a column, including
    /// format strings, casing, padding, and trimming behavior.
    /// This is a value-like configuration object (POCO).
    /// </summary>
    public sealed class FormattingOptions
    {
        /// <summary>
        /// Gets or sets a .NET composite format string applied to the resolved value
        /// before any other transformations (e.g., "N2", "yyyy-MM-dd", "P1").
        /// Leave null or empty to skip format string application.
        /// </summary>
        public string? FormatString { get; set; }

        /// <summary>
        /// Gets or sets the casing transformation applied to the string output.
        /// Default is <see cref="CasingTransform.None"/>.
        /// </summary>
        public CasingTransform CasingTransform { get; set; } = CasingTransform.None;

        /// <summary>
        /// Gets or sets the whitespace trimming behavior applied to the string output.
        /// Default is <see cref="TrimBehavior.None"/>.
        /// </summary>
        public TrimBehavior TrimBehavior { get; set; } = TrimBehavior.None;

        /// <summary>
        /// Gets or sets the padding character used when <see cref="PaddingAlignment"/> is not None.
        /// Default is a space character.
        /// </summary>
        public char PaddingChar { get; set; } = ' ';

        /// <summary>
        /// Gets or sets the total fixed width of the output after padding.
        /// A value of 0 means no padding is applied.
        /// </summary>
        public int PaddedWidth { get; set; } = 0;

        /// <summary>
        /// Gets or sets the padding alignment direction.
        /// Default is <see cref="PaddingAlignment.None"/>.
        /// </summary>
        public PaddingAlignment PaddingAlignment { get; set; } = PaddingAlignment.None;

        /// <summary>
        /// Gets or sets an optional prefix string prepended to the column's final output.
        /// </summary>
        public string? Prefix { get; set; }

        /// <summary>
        /// Gets or sets an optional suffix string appended to the column's final output.
        /// </summary>
        public string? Suffix { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of characters allowed in the output.
        /// A value of 0 means no truncation is applied.
        /// </summary>
        public int MaxLength { get; set; } = 0;

        /// <summary>
        /// Creates a deep copy of this <see cref="FormattingOptions"/> instance.
        /// </summary>
        public FormattingOptions Clone()
        {
            return new FormattingOptions
            {
                FormatString = this.FormatString,
                CasingTransform = this.CasingTransform,
                TrimBehavior = this.TrimBehavior,
                PaddingChar = this.PaddingChar,
                PaddedWidth = this.PaddedWidth,
                PaddingAlignment = this.PaddingAlignment,
                Prefix = this.Prefix,
                Suffix = this.Suffix,
                MaxLength = this.MaxLength
            };
        }
    }
}
