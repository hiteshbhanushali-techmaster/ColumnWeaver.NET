namespace ColumnWeaver.NET.Application.Validation
{
    /// <summary>
    /// V2.5 — Represents a structured diagnostic entry produced by
    /// <see cref="ConfigurationValidator"/>.
    /// </summary>
    public sealed class ValidationError
    {
        /// <summary>
        /// Gets the string representation of the severity ("Error", "Warning", or "Info").
        /// </summary>
        public string Severity { get; }

        /// <summary>
        /// Gets the display name of the affected column, or an empty string for configuration-level diagnostics.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// Gets the diagnostic rule or error code.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the descriptive diagnostic message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the typed <see cref="ValidationSeverity"/> equivalent of <see cref="Severity"/>.
        /// </summary>
        public ValidationSeverity SeverityLevel => Severity switch
        {
            "Error"   => ValidationSeverity.Error,
            "Warning" => ValidationSeverity.Warning,
            "Info"    => ValidationSeverity.Info,
            _         => ValidationSeverity.Error
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationError"/> class for a specific column.
        /// </summary>
        /// <param name="severity">The diagnostic severity string.</param>
        /// <param name="columnName">The affected column display name.</param>
        /// <param name="code">The diagnostic code.</param>
        /// <param name="message">The descriptive message.</param>
        public ValidationError(string severity, string columnName, string code, string message)
        {
            Severity   = severity   ?? "Error";
            ColumnName = columnName ?? string.Empty;
            Code       = code       ?? string.Empty;
            Message    = message    ?? string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationError"/> class for a configuration-level issue.
        /// </summary>
        /// <param name="severity">The diagnostic severity string.</param>
        /// <param name="code">The diagnostic code.</param>
        /// <param name="message">The descriptive message.</param>
        public ValidationError(string severity, string code, string message)
            : this(severity, string.Empty, code, message) { }

        internal static ValidationError Error(string code, string message)
            => new ValidationError("Error", string.Empty, code, message);

        internal static ValidationError Error(string columnName, string code, string message)
            => new ValidationError("Error", columnName, code, message);

        internal static ValidationError Warning(string columnName, string code, string message)
            => new ValidationError("Warning", columnName, code, message);

        internal static ValidationError Info(string columnName, string code, string message)
            => new ValidationError("Info", columnName, code, message);

        /// <summary>
        /// Returns a formatted string representation of this diagnostic.
        /// </summary>
        /// <returns>A string containing severity, column name (if present), code, and message.</returns>
        public override string ToString()
            => string.IsNullOrEmpty(ColumnName)
                ? $"[{Severity}] [{Code}] {Message}"
                : $"[{Severity}] Column '{ColumnName}' [{Code}] {Message}";
    }

    /// <summary>
    /// Indicates the severity level of a <see cref="ValidationError"/>.
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>
        /// Informational notice that does not block execution.
        /// </summary>
        Info    = 0,

        /// <summary>
        /// Non-fatal warning indicating a potential configuration or runtime discrepancy.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Fatal validation error that prevents plan compilation or execution.
        /// </summary>
        Error   = 2
    }
}
