namespace ColumnWeaver.NET.Core.Models
{
    /// <summary>
    /// Defines a single search-and-replace substitution rule applied to a column's
    /// output value after evaluation and formatting.
    /// Replacement rules are applied in the order they appear in the column's collection.
    /// </summary>
    public sealed class ReplacementRule
    {
        /// <summary>
        /// Gets or sets the source value to search for within the column output.
        /// Cannot be null or empty.
        /// </summary>
        public string SourceValue { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target value that replaces every occurrence of <see cref="SourceValue"/>.
        /// An empty string will effectively remove all matching occurrences.
        /// </summary>
        public string TargetValue { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the replacement is case-sensitive.
        /// Default is true (case-sensitive).
        /// </summary>
        public bool IsCaseSensitive { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="SourceValue"/> is interpreted
        /// as a regular expression pattern. Default is false (literal match).
        /// </summary>
        public bool UseRegex { get; set; } = false;

        /// <summary>
        /// Creates a deep copy of this <see cref="ReplacementRule"/> instance.
        /// </summary>
        public ReplacementRule Clone()
        {
            return new ReplacementRule
            {
                SourceValue = this.SourceValue,
                TargetValue = this.TargetValue,
                IsCaseSensitive = this.IsCaseSensitive,
                UseRegex = this.UseRegex
            };
        }
    }
}
