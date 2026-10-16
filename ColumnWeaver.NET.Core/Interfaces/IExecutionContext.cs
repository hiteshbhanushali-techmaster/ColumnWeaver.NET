using System;

namespace ColumnWeaver.NET.Core.Interfaces
{
    /// <summary>
    /// V2.5 — Minimal execution context interface in the Core layer.
    /// The concrete ExecutionContext in the Application layer implements this.
    /// </summary>
    public interface IExecutionContext
    {
        object? Source { get; }
        object? GetResult(int index);
        object?[] Variables { get; }
        bool TryGetVariable(string name, out object? value);
    }
}
