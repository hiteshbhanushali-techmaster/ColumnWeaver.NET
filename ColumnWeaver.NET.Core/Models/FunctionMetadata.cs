using System;
using System.Collections.Generic;

namespace ColumnWeaver.NET.Core.Models
{
    /// <summary>
    /// Describes the signature of a custom function registered in a
    /// custom function registry implementation.
    /// </summary>
    /// <remarks>
    /// Used by <c>ConfigurationValidator</c> to verify that
    /// <see cref="Core.Columns.CustomFunctionColumn.Parameters"/> count and
    /// types match what the function expects, and by
    /// <c>ExecutionPlanCompiler</c> to infer the output type of a
    /// <see cref="Core.Columns.CustomFunctionColumn"/>.
    /// </remarks>
    public sealed class FunctionMetadata
    {
        /// <summary>
        /// Gets or sets the registered name of the function.
        /// Must match the value used in <see cref="Core.Columns.CustomFunctionColumn.FunctionName"/>.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CLR type returned by this function when invoked.
        /// Defaults to <c>object</c> for loosely-typed functions.
        /// </summary>
        public Type ReturnType { get; set; } = typeof(object);

        /// <summary>
        /// Gets or sets the ordered list of expected parameter types.
        /// <para>
        /// Each entry corresponds to one element of
        /// <see cref="Core.Columns.CustomFunctionColumn.Parameters"/>.
        /// An empty list indicates the function accepts no parameters beyond
        /// the implicit source object.
        /// </para>
        /// </summary>
        public IReadOnlyList<Type> ParameterTypes { get; set; } = Array.Empty<Type>();

        /// <summary>
        /// Gets or sets an optional human-readable description of the function,
        /// surfaced in the configuration UI.
        /// </summary>
        public string? Description { get; set; }
    }
}
