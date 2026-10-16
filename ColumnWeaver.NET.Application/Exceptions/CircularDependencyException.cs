using System;
using System.Collections.Generic;

namespace ColumnWeaver.NET.Application.Exceptions
{
    /// <summary>
    /// Thrown when a circular dependency is detected among column definitions
    /// during topological sort of the evaluation pipeline.
    /// </summary>
    public sealed class CircularDependencyException : Exception
    {
        /// <summary>
        /// Gets the ordered list of column display names that form the detected cycle.
        /// The first and last entries are the same column, showing the full cycle path.
        /// Example: ["Total", "Price", "Tax", "Total"]
        /// </summary>
        public IReadOnlyList<string> CyclePath { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="CircularDependencyException"/>
        /// with the full cycle path.
        /// </summary>
        /// <param name="cyclePath">
        /// The sequence of column names forming the circular dependency.
        /// </param>
        public CircularDependencyException(IReadOnlyList<string> cyclePath)
            : base(BuildMessage(cyclePath))
        {
            CyclePath = cyclePath ?? Array.Empty<string>();
        }

        private static string BuildMessage(IReadOnlyList<string> path)
        {
            if (path == null || path.Count == 0)
                return "A circular dependency was detected in the column pipeline.";

            return $"Circular dependency detected: {string.Join(" \u2192 ", path)}. " +
                   "Ensure no FormulaColumn, ConditionalColumn, or CustomFunctionColumn " +
                   "references form a cycle via ${{ColumnName}} tokens.";
        }
    }
}
