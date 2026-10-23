using System;

namespace ColumnWeaver.NET.Core.Interfaces
{
    /// <summary>
    /// V2.5 — Minimal execution context interface in the Core layer.
    /// The concrete ExecutionContext in the Application layer implements this.
    /// </summary>
    public interface IExecutionContext
    {
        /// <summary>
        /// Gets the source input object being processed, or <c>null</c> if no source object was provided.
        /// </summary>
        object? Source { get; }

        /// <summary>
        /// Gets the evaluated raw result of the column at the specified zero-based index.
        /// </summary>
        /// <param name="index">The zero-based column index.</param>
        /// <returns>The evaluated column result, or <c>null</c> if not evaluated or null.</returns>
        object? GetResult(int index);

        /// <summary>
        /// Gets the array of configuration variable values available during execution.
        /// </summary>
        object?[] Variables { get; }

        /// <summary>
        /// Attempts to retrieve the value of a configuration variable by name.
        /// </summary>
        /// <param name="name">The name of the variable to look up.</param>
        /// <param name="value">When this method returns, contains the variable value if found; otherwise, <c>null</c>.</param>
        /// <returns><c>true</c> if the variable exists; otherwise, <c>false</c>.</returns>
        bool TryGetVariable(string name, out object? value);
    }
}
