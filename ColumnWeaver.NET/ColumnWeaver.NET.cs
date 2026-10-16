using System;
using System.Collections.Generic;
using System.Linq;
using ColumnWeaver.NET.Core.ParserConfiguration;

namespace ColumnWeaver.NET
{
    /// <summary>
    /// The main entry point for the ColumnWeaver.NET library.
    /// Provides a simple, thread-safe API for parsing objects to strings using
    /// a configured set of column definitions.
    /// </summary>
    /// <remarks>
    /// <b>Quick Start:</b>
    /// <code>
    /// var config = new ParserConfiguration
    /// {
    ///     Columns =
    ///     {
    ///         new PropertyColumn { Name = "Name", PropertyPath = "Name" },
    ///         new FormulaColumn { Name = "Total", Expression = "${Price} * ${Qty}" }
    ///     }
    /// };
    ///
    /// var parser = ColumnWeaverClient.Create(config);
    /// var result = parser.Parse(new { Name = "Widget", Price = 10, Qty = 2 });
    /// // result: "Widget|20"
    /// </code>
    /// </remarks>
    public sealed class ColumnWeaverClient
    {
        private readonly Application.Engine.CompiledParserEngine _engine;
        private readonly ParserOptions _options;
        private readonly ParserConfiguration _configuration;

        private bool _isFrozen;
        private Type? _precompiledSourceType;
        private Application.Execution.ExecutionPlan? _precompiledPlan;

        [ThreadStatic]
        private static System.Text.StringBuilder? _threadStringBuilder;

        /// <summary>
        /// Gets a value indicating whether this client is in precompiled/frozen mode.
        /// In frozen mode, the execution plan is cached directly on the client, eliminating
        /// configuration fingerprint computation on steady-state parses.
        /// </summary>
        public bool IsFrozen => _isFrozen;

        internal ColumnWeaverClient(ParserConfiguration configuration, ParserOptions options)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _options        = options?.Clone() ?? ParserOptions.Default;

            var resolver    = new Infrastructure.PropertyResolution.ReflectionPropertyResolver();
            var ncalc      = new Infrastructure.ExpressionEngine.NCalcExpressionEngine(resolver);
            var lambdaEngine = new Infrastructure.ExpressionEngine.LambdaExpressionEngine(ncalc);
            var registry   = new Infrastructure.DefaultFunctionRegistry();

            _engine = new Application.Engine.CompiledParserEngine(resolver, lambdaEngine, registry);
            _engine.Options.DebugMode    = _options.EnableDiagnostics;
            _engine.Options.AllowWarnings = _options.AllowWarnings;
            if (_options.Timeout.HasValue)
                _engine.Options.Timeout = _options.Timeout.Value;

            if (_options.IsFrozen)
            {
                _isFrozen = true;
            }
        }

        /// <summary>
        /// Creates a new <see cref="ColumnWeaverClient"/> instance from the specified configuration.
        /// If <paramref name="options"/> specifies <see cref="ParserOptions.IsFrozen"/> = true,
        /// the client operates in high-throughput precompiled mode.
        /// </summary>
        /// <param name="configuration">The parser configuration defining columns and behavior.</param>
        /// <param name="options">Optional parser options for diagnostics and execution control.</param>
        /// <returns>A configured <see cref="ColumnWeaverClient"/> instance.</returns>
        public static ColumnWeaverClient Create(ParserConfiguration configuration, ParserOptions? options = null)
        {
            return new ColumnWeaverClient(configuration, options ?? ParserOptions.Default);
        }

        /// <summary>
        /// Creates a high-performance precompiled <see cref="ColumnWeaverClient"/> with frozen configuration.
        /// Eliminates repeated configuration fingerprint hashing on every parse call.
        /// </summary>
        /// <param name="configuration">The parser configuration defining columns and behavior.</param>
        /// <param name="defaultSourceType">Optional source object CLR type to compile immediately.</param>
        /// <param name="options">Optional parser options for diagnostics and execution control.</param>
        /// <returns>A precompiled, frozen <see cref="ColumnWeaverClient"/> instance.</returns>
        public static ColumnWeaverClient CreatePrecompiled(
            ParserConfiguration configuration,
            Type? defaultSourceType = null,
            ParserOptions? options = null)
        {
            var opts = options != null ? options.Clone() : new ParserOptions();
            opts.IsFrozen = true;
            var client = new ColumnWeaverClient(configuration, opts);
            if (defaultSourceType != null)
            {
                client.Precompile(defaultSourceType);
            }
            return client;
        }

        /// <summary>
        /// Eagerly compiles and freezes the execution plan for the specified source type,
        /// eliminating fingerprint hashing and plan lookups on subsequent parses.
        /// </summary>
        /// <param name="sourceType">The CLR type of the input objects to be parsed.</param>
        public void Precompile(Type sourceType)
        {
            if (sourceType == null) throw new ArgumentNullException(nameof(sourceType));
            _precompiledPlan = _engine.GetPlan(_configuration, sourceType);
            _precompiledSourceType = sourceType;
            _isFrozen = true;
        }

        /// <summary>
        /// Freezes this client to prevent runtime configuration fingerprint hashing.
        /// If <paramref name="defaultSourceType"/> is provided, the execution plan is precompiled immediately.
        /// </summary>
        /// <param name="defaultSourceType">Optional CLR type of input objects.</param>
        /// <returns>This client instance for fluent chaining.</returns>
        public ColumnWeaverClient Freeze(Type? defaultSourceType = null)
        {
            _isFrozen = true;
            if (defaultSourceType != null)
            {
                Precompile(defaultSourceType);
            }
            return this;
        }

        /// <summary>
        /// Explicitly recompiles the frozen execution plan.
        /// Call this if the underlying configuration was mutated and the precompiled client should adopt the changes.
        /// </summary>
        /// <param name="sourceType">Optional specific source type to recompile. Defaults to previous source type.</param>
        public void Recompile(Type? sourceType = null)
        {
            var targetType = sourceType ?? _precompiledSourceType;
            _precompiledPlan = null;
            if (targetType != null)
            {
                Precompile(targetType);
            }
        }

        /// <summary>
        /// Parses a single input object into a formatted string using the configured columns.
        /// </summary>
        /// <param name="input">The source object to parse. May be null if all columns handle null gracefully.</param>
        /// <returns>A formatted string assembled from all enabled columns.</returns>
        public string Parse(object input)
        {
            if (input == null)
            {
                var nullResult = _engine.Parse(_configuration, null, null);
                return nullResult ?? string.Empty;
            }

            if (_isFrozen)
            {
                var inputType = input.GetType();
                var plan = _precompiledPlan;
                if (plan == null || _precompiledSourceType != inputType)
                {
                    plan = GetOrCompileFrozenPlan(inputType);
                }

                if (plan.FastRowExecutor != null && !_options.EnableDiagnostics && !_options.Timeout.HasValue)
                {
                    var sb = _threadStringBuilder ?? (_threadStringBuilder = new System.Text.StringBuilder(256));
                    sb.Clear();
                    var fastResult = plan.FastRowExecutor(input, sb);
                    if (fastResult != null) return fastResult;
                }

                var res = _engine.ParsePlan(plan, input);
                return res ?? string.Empty;
            }

            var result = _engine.Parse(_configuration, input, input.GetType());
            return result ?? string.Empty;
        }

        private Application.Execution.ExecutionPlan GetOrCompileFrozenPlan(Type inputType)
        {
            var plan = _engine.GetPlan(_configuration, inputType);
            _precompiledPlan = plan;
            _precompiledSourceType = inputType;
            return plan;
        }

        /// <summary>
        /// Parses multiple input objects into formatted strings using the configured columns.
        /// Optimized for high-throughput batch processing with single-pass, lazy enumeration.
        /// </summary>
        /// <param name="inputs">The sequence of source objects to parse.</param>
        /// <returns>An enumerable of formatted strings, one per input object.</returns>
        public IEnumerable<string> ParseMany(IEnumerable<object> inputs)
        {
            if (inputs == null)
                throw new ArgumentNullException(nameof(inputs));

            if (_isFrozen && _precompiledPlan != null)
            {
                return _engine.ParseManyWithPlan(_precompiledPlan, inputs);
            }

            return _engine.ParseMany(_configuration, inputs, _precompiledSourceType);
        }

        /// <summary>
        /// Validates the configuration without parsing any objects.
        /// </summary>
        /// <returns>A <see cref="Application.Validation.ValidationResult"/> containing errors and warnings.</returns>
        public Application.Validation.ValidationResult Validate()
        {
            return _engine.Validate(_configuration, null);
        }
    }
}
