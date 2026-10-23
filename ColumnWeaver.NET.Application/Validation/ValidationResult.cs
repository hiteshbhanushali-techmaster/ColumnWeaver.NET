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
        /// <summary>
        /// Gets a value indicating whether validation succeeded without any errors.
        /// </summary>
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

        /// <summary>
        /// Gets the read-only collection of validation errors.
        /// </summary>
        public IReadOnlyList<ValidationError> Errors { get; }

        /// <summary>
        /// Gets the read-only collection of validation warnings.
        /// </summary>
        public IReadOnlyList<ValidationError> Warnings { get; }

        /// <summary>
        /// Gets the read-only collection of informational validation diagnostics.
        /// </summary>
        public IReadOnlyList<ValidationError> Infos { get; }

        /// <summary>
        /// Gets the total count of all diagnostic issues across all severity levels.
        /// </summary>
        public int TotalIssueCount => Errors.Count + Warnings.Count + Infos.Count;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationResult"/> class.
        /// </summary>
        /// <param name="errors">The list of errors.</param>
        /// <param name="warnings">The list of warnings.</param>
        /// <param name="infos">The list of informational messages.</param>
        public ValidationResult(
            IReadOnlyList<ValidationError> errors,
            IReadOnlyList<ValidationError> warnings,
            IReadOnlyList<ValidationError> infos)
        {
            Errors   = errors   ?? System.Array.Empty<ValidationError>();
            Warnings = warnings ?? System.Array.Empty<ValidationError>();
            Infos    = infos    ?? System.Array.Empty<ValidationError>();
        }

        /// <summary>
        /// Creates an empty, successful <see cref="ValidationResult"/> with no issues.
        /// </summary>
        /// <returns>A successful validation result.</returns>
        public static ValidationResult Ok()
            => new ValidationResult(
                System.Array.Empty<ValidationError>(),
                System.Array.Empty<ValidationError>(),
                System.Array.Empty<ValidationError>());

        /// <summary>
        /// Creates a <see cref="ValidationResult"/> containing only the specified errors.
        /// </summary>
        /// <param name="errors">The list of errors.</param>
        /// <returns>A validation result with errors.</returns>
        public static ValidationResult WithErrors(IReadOnlyList<ValidationError> errors)
            => new ValidationResult(errors, System.Array.Empty<ValidationError>(), System.Array.Empty<ValidationError>());

        /// <summary>
        /// Creates a <see cref="ValidationResult"/> wrapping copies of the specified issue lists.
        /// </summary>
        /// <param name="errors">The list of errors.</param>
        /// <param name="warnings">The list of warnings.</param>
        /// <param name="infos">The list of informational issues.</param>
        /// <returns>A constructed validation result.</returns>
        public static ValidationResult From(
            List<ValidationError> errors,
            List<ValidationError> warnings,
            List<ValidationError> infos)
            => new ValidationResult(errors.AsReadOnly(), warnings.AsReadOnly(), infos.AsReadOnly());
    }
}
