using System;
using System.Collections.Generic;
using System.Linq;
using ColumnWeaver.NET.Application.Validation;

namespace ColumnWeaver.NET.Application.Exceptions
{
    /// <summary>
    /// Thrown by <c>ExecutionPlanCompiler</c> when
    /// <see cref="Validation.ConfigurationValidator"/> finds one or more errors
    /// in a <see cref="Core.ParserConfiguration.ParserConfiguration"/> before
    /// compilation begins.
    /// </summary>
    /// <remarks>
    /// Unlike V1's <see cref="Exceptions.ParserConfigurationException"/> which
    /// throws on the first error, this exception carries the <b>complete</b> list
    /// of <see cref="ValidationError"/> instances so the caller (or UI) can
    /// present all issues at once.
    /// </remarks>
    public sealed class AggregateValidationException : Exception
    {
        /// <summary>
        /// Gets the complete list of structured validation errors that prevented
        /// compilation.
        /// </summary>
        public IReadOnlyList<ValidationError> Errors { get; }

        /// <summary>
        /// Initializes a new <see cref="AggregateValidationException"/>.
        /// </summary>
        /// <param name="errors">
        /// The non-empty list of validation errors. Must not be null.
        /// </param>
        public AggregateValidationException(IReadOnlyList<ValidationError> errors)
            : base(BuildMessage(errors))
        {
            Errors = errors ?? Array.Empty<ValidationError>();
        }

        private static string BuildMessage(IReadOnlyList<ValidationError> errors)
        {
            if (errors == null || errors.Count == 0)
                return "Configuration validation failed with unspecified errors.";

            if (errors.Count == 1)
                return $"Configuration validation failed: {errors[0]}";

            return $"Configuration validation failed with {errors.Count} error(s):\n" +
                   string.Join("\n", errors.Select((e, i) => $"  [{i + 1}] {e}"));
        }
    }
}
