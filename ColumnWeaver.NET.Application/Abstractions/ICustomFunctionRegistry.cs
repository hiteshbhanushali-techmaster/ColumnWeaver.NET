using System.Collections.Generic;
using ColumnWeaver.NET.Core.Models;

namespace ColumnWeaver.NET.Application.Abstractions
{
    /// <summary>
    /// Application-layer abstraction for a registry of named custom functions
    /// invoked by <see cref="Core.Columns.CustomFunctionColumn"/> during evaluation.
    /// </summary>
    /// <remarks>
    /// Implementations of this interface are provided by the Infrastructure or
    /// Presentation layers and registered with the <see cref="Factory.ColumnEvaluatorFactory"/>.
    /// The Core layer has no knowledge of this registry.
    /// </remarks>
    public interface ICustomFunctionRegistry
    {
        /// <summary>
        /// Returns true if a function with the specified name is registered.
        /// </summary>
        /// <param name="functionName">
        /// The case-sensitive name of the function to check.
        /// </param>
        bool IsRegistered(string functionName);

        /// <summary>
        /// Invokes the registered function by name, passing the source object and
        /// any additional configured parameters. Returns the result as a string.
        /// </summary>
        /// <remarks>
        /// This V1-compatible overload is preserved for backward compatibility.
        /// New code should prefer <see cref="InvokeRaw"/> to avoid premature
        /// string conversion when using the compiled execution pipeline.
        /// </remarks>
        /// <param name="functionName">The registered function name. Must not be null or empty.</param>
        /// <param name="source">The source object passed to the parser. May be null.</param>
        /// <param name="parameters">
        /// The ordered list of string parameters from <see cref="Core.Columns.CustomFunctionColumn.Parameters"/>.
        /// Column cross-references in parameters are expanded before this call.
        /// </param>
        /// <returns>The string output produced by the function.</returns>
        string Invoke(string functionName, object? source, IReadOnlyList<string> parameters);

        // ── V2: Typed invocation and metadata ─────────────────────────────────

        /// <summary>
        /// Invokes the registered function by name and returns the raw, unboxed
        /// CLR result <b>before</b> string conversion.
        /// </summary>
        /// <remarks>
        /// Used by the V2 compiled execution pipeline so that the output value
        /// retains its CLR type through the execution stage, allowing
        /// <see cref="Core.Models.FormattingOptions.FormatString"/> to be applied
        /// accurately at the assembly stage.
        /// </remarks>
        /// <param name="functionName">The registered function name. Must not be null or empty.</param>
        /// <param name="source">The source object passed to the parser. May be null.</param>
        /// <param name="parameters">
        /// The ordered list of fully-expanded string parameters.
        /// </param>
        /// <returns>
        /// The unboxed CLR result of the function; or <c>null</c> when the function
        /// yields no output.
        /// </returns>
        object? InvokeRaw(string functionName, object? source, IReadOnlyList<string> parameters);

        /// <summary>
        /// Returns the <see cref="FunctionMetadata"/> for the specified function,
        /// or <c>null</c> if the function is not registered or does not expose metadata.
        /// </summary>
        /// <remarks>
        /// Used by <c>ConfigurationValidator</c> to validate parameter counts and
        /// types, and by <c>ExecutionPlanCompiler</c> to infer the
        /// <see cref="Core.Columns.ColumnDefinition.OutputType"/> of a
        /// <see cref="Core.Columns.CustomFunctionColumn"/>.
        /// </remarks>
        /// <param name="functionName">The registered function name.</param>
        /// <returns>
        /// A <see cref="FunctionMetadata"/> instance, or <c>null</c> if unavailable.
        /// </returns>
        FunctionMetadata? GetMetadata(string functionName);

        /// <summary>
        /// Returns the names of all registered functions.
        /// Used by the UI to populate function selection dropdowns.
        /// </summary>
        IReadOnlyList<string> GetRegisteredFunctionNames();
    }
}

