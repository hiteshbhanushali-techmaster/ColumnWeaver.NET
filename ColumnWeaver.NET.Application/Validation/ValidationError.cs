namespace ColumnWeaver.NET.Application.Validation
{
    /// <summary>
    /// V2.5 — Represents a structured diagnostic entry produced by
    /// <see cref="ConfigurationValidator"/>.
    /// </summary>
    public sealed class ValidationError
    {
        public string Severity { get; }
        public string ColumnName { get; }
        public string Code { get; }
        public string Message { get; }

        public ValidationSeverity SeverityLevel => Severity switch
        {
            "Error"   => ValidationSeverity.Error,
            "Warning" => ValidationSeverity.Warning,
            "Info"    => ValidationSeverity.Info,
            _         => ValidationSeverity.Error
        };

        public ValidationError(string severity, string columnName, string code, string message)
        {
            Severity   = severity   ?? "Error";
            ColumnName = columnName ?? string.Empty;
            Code       = code       ?? string.Empty;
            Message    = message    ?? string.Empty;
        }

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

        public override string ToString()
            => string.IsNullOrEmpty(ColumnName)
                ? $"[{Severity}] [{Code}] {Message}"
                : $"[{Severity}] Column '{ColumnName}' [{Code}] {Message}";
    }

    public enum ValidationSeverity
    {
        Info    = 0,
        Warning = 1,
        Error   = 2
    }
}
