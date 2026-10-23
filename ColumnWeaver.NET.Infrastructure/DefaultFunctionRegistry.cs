using System;
using System.Collections.Generic;
using System.Linq;
using ColumnWeaver.NET.Application.Abstractions;
using ColumnWeaver.NET.Core.Models;

namespace ColumnWeaver.NET.Infrastructure
{
    /// <summary>
    /// A default implementation of <see cref="ICustomFunctionRegistry"/> that provides
    /// common utility functions for use in <see cref="Core.Columns.CustomFunctionColumn"/>.
    /// </summary>
    /// <remarks>
    /// This registry is automatically used by default.
    /// Extend this class to add custom functions for your domain.
    /// </remarks>
    public class DefaultFunctionRegistry : ICustomFunctionRegistry
    {
        private readonly Dictionary<string, Func<object?, IReadOnlyList<string>, object?>> _functions
            = new Dictionary<string, Func<object?, IReadOnlyList<string>, object?>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, FunctionMetadata> _metadata
            = new Dictionary<string, FunctionMetadata>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultFunctionRegistry"/> class and registers default built-in functions.
        /// </summary>
        public DefaultFunctionRegistry()
        {
            RegisterDefaultFunctions();
        }

        private void RegisterDefaultFunctions()
        {
            Register("Upper", (source, args) =>
            {
                if (args.Count == 0 || string.IsNullOrEmpty(args[0])) return string.Empty;
                return args[0].ToUpperInvariant();
            }, typeof(string), new[] { typeof(string) });

            Register("Lower", (source, args) =>
            {
                if (args.Count == 0 || string.IsNullOrEmpty(args[0])) return string.Empty;
                return args[0].ToLowerInvariant();
            }, typeof(string), new[] { typeof(string) });

            Register("Trim", (source, args) =>
            {
                if (args.Count == 0 || string.IsNullOrEmpty(args[0])) return string.Empty;
                return args[0].Trim();
            }, typeof(string), new[] { typeof(string) });

            Register("Concat", (source, args) =>
            {
                return string.Join("", args);
            }, typeof(string));

            Register("Format", (source, args) =>
            {
                if (args.Count < 2) return string.Empty;
                var format = args[0];
                var values = args.Skip(1).ToArray();
                try { return string.Format(format, values); }
                catch { return format; }
            }, typeof(string));

            Register("Substring", (source, args) =>
            {
                if (args.Count < 1 || string.IsNullOrEmpty(args[0])) return string.Empty;
                var s = args[0];
                if (args.Count == 2)
                {
                    if (int.TryParse(args[1], out int len))
                        return s.Length > len ? s.Substring(0, len) : s;
                }
                if (args.Count >= 3)
                {
                    if (int.TryParse(args[1], out int start) && int.TryParse(args[2], out int len))
                        return s.Length > start + len ? s.Substring(start, len) : s;
                }
                return s;
            }, typeof(string), new[] { typeof(string), typeof(string), typeof(string) });

            Register("Replace", (source, args) =>
            {
                if (args.Count < 3) return args.Count > 0 ? args[0] : string.Empty;
                return args[0].Replace(args[1], args[2]);
            }, typeof(string), new[] { typeof(string), typeof(string), typeof(string) });

            Register("IIf", (source, args) =>
            {
                if (args.Count < 3) return string.Empty;
                var cond = args[0];
                if (bool.TryParse(cond, out bool b) && b) return args[1];
                return args[2];
            }, typeof(string), new[] { typeof(string), typeof(string), typeof(string) });
        }

        /// <summary>
        /// Registers a custom function with the registry.
        /// </summary>
        /// <param name="name">The function name.</param>
        /// <param name="handler">The function implementation.</param>
        /// <param name="returnType">The CLR return type.</param>
        /// <param name="parameterTypes">The expected parameter types.</param>
        public void Register(
            string name,
            Func<object?, IReadOnlyList<string>, object?> handler,
            Type returnType,
            Type[]? parameterTypes = null)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (handler == null) return;

            _functions[name] = handler;
            _metadata[name] = new FunctionMetadata
            {
                Name          = name,
                ReturnType    = returnType ?? typeof(object),
                ParameterTypes = parameterTypes ?? Array.Empty<Type>()
            };
        }

        /// <inheritdoc />
        public bool IsRegistered(string functionName)
            => string.IsNullOrEmpty(functionName) ? false : _functions.ContainsKey(functionName);

        /// <inheritdoc />
        public string Invoke(string functionName, object? source, IReadOnlyList<string> parameters)
        {
            var result = InvokeRaw(functionName, source, parameters);
            return result?.ToString() ?? string.Empty;
        }

        /// <inheritdoc />
        public object? InvokeRaw(string functionName, object? source, IReadOnlyList<string> parameters)
        {
            if (string.IsNullOrEmpty(functionName)) return null;
            if (!_functions.TryGetValue(functionName, out var handler)) return null;

            try { return handler(source, parameters); }
            catch { return null; }
        }

        /// <inheritdoc />
        public FunctionMetadata? GetMetadata(string functionName)
        {
            if (string.IsNullOrEmpty(functionName)) return null;
            return _metadata.TryGetValue(functionName, out var meta) ? meta : null;
        }

        /// <inheritdoc />
        public IReadOnlyList<string> GetRegisteredFunctionNames()
        {
            return new List<string>(_functions.Keys);
        }
    }
}
