using System.Collections.Generic;

namespace ColumnWeaver.NET.Application.Validation
{
    /// <summary>
    /// V2.5 — The structured result returned by <see cref="ConfigurationValidator.Validate"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="IsValid"/> is <c>true</c> when <see cref="Errors"/> is empty
    /// (warnings do not affect validity by default). The result is immutable once created.
    /// </remarks>
    public sealed class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// V2.5 — Returns <c>true</c> when this result has no errors.
        /// Optionally considers warnings as non-blocking when
        /// <paramref name="allowWarnings"/> is <c>true</c>.
        /// </summary>
        public bool IsValidForExecution(bool allowWarnings)
        {
            if (Errors.Count > 0) return false;
            if (!allowWarnings && Warnings.Count > 0) return false;
            return true;
        }

        public IReadOnlyList<ValidationError> Errors { get; }
        public IReadOnlyList<ValidationError> Warnings { get; }
        public IReadOnlyList<ValidationError> Infos { get; }

        public int TotalIssueCount => Errors.Count + Warnings.Count + Infos.Count;

        public ValidationResult(
            IReadOnlyList<ValidationError> errors,
            IReadOnlyList<ValidationError> warnings,
            IReadOnlyList<ValidationError> infos)
        {
            Errors   = errors   ?? System.Array.Empty<ValidationError>();
            Warnings = warnings ?? System.Array.Empty<ValidationError>();
            Infos    = infos    ?? System.Array.Empty<ValidationError>();
        }

        public static ValidationResult Ok()
            => new ValidationResult(
                System.Array.Empty<ValidationError>(),
                System.Array.Empty<ValidationError>(),
                System.Array.Empty<ValidationError>());

        public static ValidationResult WithErrors(IReadOnlyList<ValidationError> errors)
            => new ValidationResult(errors, System.Array.Empty<ValidationError>(), System.Array.Empty<ValidationError>());

        public static ValidationResult From(
            List<ValidationError> errors,
            List<ValidationError> warnings,
            List<ValidationError> infos)
            => new ValidationResult(errors.AsReadOnly(), warnings.AsReadOnly(), infos.AsReadOnly());
    }
}
