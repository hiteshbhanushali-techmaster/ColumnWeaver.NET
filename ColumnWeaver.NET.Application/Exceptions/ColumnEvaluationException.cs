using System;

namespace ColumnWeaver.NET.Application.Exceptions
{
    /// <summary>
    /// Thrown when the evaluation of a specific column fails at runtime.
    /// Wraps the original exception with column identity context.
    /// </summary>
    public sealed class ColumnEvaluationException : Exception
    {
        /// <summary>
        /// Gets the unique identifier of the column that failed evaluation.
        /// </summary>
        public Guid ColumnId { get; }

        /// <summary>
        /// Gets the name of the column that failed evaluation.
        /// </summary>
        public string ColumnName { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ColumnEvaluationException"/>.
        /// </summary>
        /// <param name="columnId">The Id of the failing column.</param>
        /// <param name="columnName">The Name of the failing column.</param>
        /// <param name="message">The diagnostic message describing the failure.</param>
        /// <param name="innerException">Optional inner exception that caused this failure.</param>
        public ColumnEvaluationException(
            Guid columnId,
            string columnName,
            string message,
            Exception? innerException = null)
            : base($"[Column '{columnName}' ({columnId:N})]: {message}", innerException)
        {
            ColumnId = columnId;
            ColumnName = columnName ?? string.Empty;
        }
    }
}
