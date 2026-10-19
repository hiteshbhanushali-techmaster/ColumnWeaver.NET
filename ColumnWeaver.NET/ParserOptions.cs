using System;
using System.Threading;

namespace ColumnWeaver.NET
{
    /// <summary>
    /// Configures execution behavior for an <see cref="ColumnWeaver.NET"/> instance.
    /// All options have sensible defaults. Set only the options you need.
    /// </summary>
    /// <example>
    /// <code>
    /// var options = new ParserOptions { EnableDiagnostics = true };
    /// var parser = ColumnWeaver.NET.Create(config, options);
    /// </code>
    /// </example>
    public sealed class ParserOptions
    {
        /// <summary>
        /// Gets a default <see cref="ParserOptions"/> instance with all options set to their defaults.
        /// </summary>
        public static ParserOptions Default => new ParserOptions();

        /// <summary>
        /// Gets or sets a value indicating whether execution diagnostics are enabled.
        /// When true, detailed timing and execution information is captured.
        /// Default is false (no overhead).
        /// </summary>
        /// <remarks>
        /// Enabling diagnostics has a small performance cost. Disable for production
        /// unless you need the diagnostic information.
        /// </remarks>
        public bool EnableDiagnostics { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether validation warnings should be allowed
        /// without blocking compilation and execution.
        /// When false (default), warnings are treated as informational only.
        /// </summary>
        public bool AllowWarnings { get; set; }

        /// <summary>
        /// Gets or sets the maximum duration allowed for a single row parse operation.
        /// Applies to each item when parsing multiple rows.
        /// Default is null (no timeout).
        /// </summary>
        /// <remarks>
        /// Timeout enforcement operates cooperatively by checking deadlines before and after
        /// each column node. A slow column causes a <see cref="TimeoutException"/> to be reported
        /// after that column returns; in-flight synchronous expressions or custom functions
        /// cannot be preemptively aborted while running.
        /// </remarks>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets an optional cancellation token to cooperatively cancel execution
        /// between column evaluations and between rows in batch processing.
        /// In-flight synchronous expressions, custom functions, and property resolutions
        /// cannot be preemptively interrupted mid-execution.
        /// Default is <see cref="CancellationToken.None"/>.
        /// </summary>
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        /// <summary>
        /// Gets or sets a value indicating whether the client configuration is frozen/immutable.
        /// When true, the client compiles the execution plan once and reuses it on every parse
        /// without checking configuration fingerprints per record, achieving maximum steady-state throughput.
        /// Default is false (dynamic change detection enabled).
        /// </summary>
        public bool IsFrozen { get; set; }

        /// <summary>
        /// Creates a new <see cref="ParserOptions"/> with diagnostics enabled.
        /// </summary>
        public static ParserOptions WithDiagnostics()
            => new ParserOptions { EnableDiagnostics = true };

        /// <summary>
        /// Creates a new <see cref="ParserOptions"/> with a timeout.
        /// </summary>
        /// <param name="timeout">The maximum duration for a single parse operation.</param>
        public static ParserOptions WithTimeout(TimeSpan timeout)
            => new ParserOptions { Timeout = timeout };

        /// <summary>
        /// Creates a new <see cref="ParserOptions"/> with a cancellation token.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to observe.</param>
        public static ParserOptions WithCancellationToken(CancellationToken cancellationToken)
            => new ParserOptions { CancellationToken = cancellationToken };

        /// <summary>
        /// Creates a deep copy of this <see cref="ParserOptions"/> instance.
        /// </summary>
        public ParserOptions Clone()
        {
            return new ParserOptions
            {
                EnableDiagnostics = this.EnableDiagnostics,
                AllowWarnings = this.AllowWarnings,
                Timeout = this.Timeout,
                IsFrozen = this.IsFrozen,
                CancellationToken = this.CancellationToken
            };
        }
    }
}
