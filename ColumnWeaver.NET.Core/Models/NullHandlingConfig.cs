using ColumnWeaver.NET.Core.Enums;

namespace ColumnWeaver.NET.Core.Models
{
    /// <summary>
    /// Encapsulates the null-resolution policy for a column, including which strategy
    /// to use and, optionally, a fallback value when the strategy is
    /// <see cref="NullHandlingStrategy.UseDefault"/>.
    /// </summary>
    public sealed class NullHandlingConfig
    {
        /// <summary>
        /// Gets or sets the strategy to apply when the evaluated value is null.
        /// Default is <see cref="NullHandlingStrategy.EmptyString"/>.
        /// </summary>
        public NullHandlingStrategy Strategy { get; set; } = NullHandlingStrategy.EmptyString;

        /// <summary>
        /// Gets or sets the default fallback value used when
        /// <see cref="Strategy"/> is <see cref="NullHandlingStrategy.UseDefault"/>.
        /// Has no effect for other strategies.
        /// </summary>
        public string? DefaultValue { get; set; }

        /// <summary>
        /// Gets or sets an optional message included in the exception thrown when
        /// <see cref="Strategy"/> is <see cref="NullHandlingStrategy.ThrowException"/>.
        /// Leave null to use a framework-generated message.
        /// </summary>
        public string? ExceptionMessage { get; set; }

        /// <summary>
        /// Creates a deep copy of this <see cref="NullHandlingConfig"/> instance.
        /// </summary>
        public NullHandlingConfig Clone()
        {
            return new NullHandlingConfig
            {
                Strategy = this.Strategy,
                DefaultValue = this.DefaultValue,
                ExceptionMessage = this.ExceptionMessage
            };
        }
    }
}
