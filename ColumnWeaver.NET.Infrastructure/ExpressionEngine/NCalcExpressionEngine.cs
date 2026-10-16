using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NCalc;
using ColumnWeaver.NET.Core.Interfaces;

namespace ColumnWeaver.NET.Infrastructure.ExpressionEngine
{
    /// <summary>
    /// NCalc-backed implementation of <see cref="IExpressionEngine"/>.
    /// </summary>
    /// <remarks>
    /// <b>How source object properties are exposed to expressions:</b>
    ///
    /// NCalc fires an <c>EvaluateParameter</c> event for every unresolved identifier
    /// in an expression. This implementation handles that event by attempting to resolve
    /// the identifier as a property path on the source object via the injected
    /// <see cref="IPropertyResolver"/>.
    /// <code>
    ///   Expression: "UnitPrice * Quantity"
    ///   → NCalc fires EvaluateParameter("UnitPrice") → resolves via IPropertyResolver
    ///   → NCalc fires EvaluateParameter("Quantity")  → resolves via IPropertyResolver
    /// </code>
    ///
    /// For nested or ambiguous property paths, the explicit <c>prop()</c> custom function
    /// is provided:
    /// <code>
    ///   Expression: "prop('Order.Customer.Name') + ' — ' + prop('Order.Total')"
    /// </code>
    ///
    /// <b>Cross-column references (<c>${ColumnName}</c>):</b>
    /// All such tokens are expanded by <c>ColumnPipeline</c> <i>before</i> the expression
    /// is passed to this engine. By the time <c>Evaluate</c> or <c>EvaluateCondition</c>
    /// is called, the expression string contains only NCalc-native syntax.
    ///
    /// <b>Performance caching:</b>
    /// NCalc internally parses and caches the AST per expression string.
    /// Creating a new <see cref="Expression"/> instance is therefore cheap
    /// (one allocation, no re-parsing). Thread-safe because each call owns its own instance.
    ///
    /// Additionally, this class maintains a <see cref="ConcurrentDictionary{TKey,TValue}"/>
    /// for syntax-validation results so that <see cref="ValidateExpression"/> only parses
    /// each unique string once.
    ///
    /// <b>Supported NCalc expression types:</b>
    /// Arithmetic, boolean logic, string operations, comparison operators, the full
    /// set of built-in NCalc math/string functions, and the custom <c>prop(path)</c> function.
    /// </remarks>
    public sealed class NCalcExpressionEngine : IExpressionEngine
    {
        // -----------------------------------------------------------------
        //  Dependencies
        // -----------------------------------------------------------------

        private readonly IPropertyResolver _propertyResolver;

        // -----------------------------------------------------------------
        //  Caches
        // -----------------------------------------------------------------

        /// <summary>
        /// Caches the result of expression syntax validation so repeated
        /// <see cref="ValidateExpression"/> calls for the same string are O(1).
        /// </summary>
        private readonly ConcurrentDictionary<string, bool> _validationCache;

        // -----------------------------------------------------------------
        //  Constructor
        // -----------------------------------------------------------------

        /// <summary>
        /// Initializes a new <see cref="NCalcExpressionEngine"/>.
        /// </summary>
        /// <param name="propertyResolver">
        /// Used to resolve expression identifiers against the source object at runtime.
        /// Must not be null.
        /// </param>
        public NCalcExpressionEngine(IPropertyResolver propertyResolver)
        {
            _propertyResolver = propertyResolver
                ?? throw new ArgumentNullException(nameof(propertyResolver));

            _validationCache = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }

        // -----------------------------------------------------------------
        //  IExpressionEngine
        // -----------------------------------------------------------------

        /// <inheritdoc />
        /// <exception cref="ArgumentException">When <paramref name="expression"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">
        /// When NCalc fails to evaluate the expression (parse error or runtime error).
        /// </exception>
        public string Evaluate(string expression, IExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(expression))
                throw new ArgumentException("Expression cannot be null or empty.", nameof(expression));

            var ncalc = BuildExpression(expression, context);

            try
            {
                var result = ncalc.Evaluate();
                return result?.ToString() ?? string.Empty;
            }
            catch (EvaluationException ex)
            {
                // EvaluationException covers all NCalc 1.3.8 runtime failures,
                // including undefined parameters/identifiers.
                throw new InvalidOperationException(
                    $"NCalc failed to evaluate expression '{expression}': {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        /// <remarks>Returns the unboxed CLR result before string conversion.</remarks>
        /// <exception cref="ArgumentException">When <paramref name="expression"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">When NCalc fails to evaluate the expression.</exception>
        public object? EvaluateRaw(string expression, IExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(expression))
                throw new ArgumentException("Expression cannot be null or empty.", nameof(expression));

            var ncalc = BuildExpression(expression, context);

            try
            {
                return ncalc.Evaluate();
            }
            catch (EvaluationException ex)
            {
                throw new InvalidOperationException(
                    $"NCalc failed to evaluate expression '{expression}': {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException">
        /// When <paramref name="conditionExpression"/> is null or empty.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// When the expression does not evaluate to a boolean-convertible value.
        /// </exception>
        public bool EvaluateCondition(string conditionExpression, IExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(conditionExpression))
                throw new ArgumentException(
                    "Condition expression cannot be null or empty.", nameof(conditionExpression));

            var ncalc = BuildExpression(conditionExpression, context);

            try
            {
                var result = ncalc.Evaluate();

                if (result is bool boolResult)
                    return boolResult;

                // Allow numeric / string truthy coercions.
                return Convert.ToBoolean(result);
            }
            catch (EvaluationException ex)
            {
                throw new InvalidOperationException(
                    $"NCalc failed to evaluate condition '{conditionExpression}': {ex.Message}", ex);
            }
            catch (InvalidCastException ex)
            {
                throw new InvalidOperationException(
                    $"Condition expression '{conditionExpression}' did not produce a boolean value.", ex);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException(
                    $"Condition expression '{conditionExpression}' could not be converted to boolean.", ex);
            }
        }

        private static readonly Regex TokenPattern =
            new Regex(@"\$\{([^}]+)\}", RegexOptions.Compiled);

        /// <inheritdoc />
        /// <remarks>
        /// Result is cached — the same expression string always returns the same
        /// validation result without re-parsing.
        /// </remarks>
        public bool ValidateExpression(string expression)
        {
            if (string.IsNullOrEmpty(expression)) return false;

            return _validationCache.GetOrAdd(expression, expr =>
            {
                try
                {
                    var normalized = TokenPattern.Replace(expr, "[$1]");
                    var ncalc = new Expression(normalized, EvaluateOptions.IgnoreCase);
                    return !ncalc.HasErrors();
                }
                catch
                {
                    return false;
                }
            });
        }

        // ── V2.5: Expression Compilation ─────────────────────────────────────

        /// <inheritdoc />
        /// <remarks>
        /// NCalc does not support pre-compilation to delegates. Returns <c>null</c>
        /// so the compiler falls back to <see cref="EvaluateRaw"/> at runtime.
        /// </remarks>
        public Func<IExecutionContext, object?>? CompileExpression(
            string               expression,
            Type?                sourceType,
            IReadOnlyList<int>   dependencyIndexes,
            IReadOnlyDictionary<string, int>? variableIndexes = null,
            IReadOnlyDictionary<string, int>? columnIndexes = null,
            IReadOnlyList<object?>? variableValues = null)
            => null;

        /// <inheritdoc />
        /// <remarks>
        /// NCalc does not support pre-compilation to delegates. Returns <c>null</c>.
        /// </remarks>
        public Func<IExecutionContext, bool>? CompileCondition(
            string               conditionExpression,
            Type?                sourceType,
            IReadOnlyList<int>   dependencyIndexes,
            IReadOnlyDictionary<string, int>? variableIndexes = null,
            IReadOnlyDictionary<string, int>? columnIndexes = null,
            IReadOnlyList<object?>? variableValues = null)
            => null;

        // ── V2: Engine Capabilities ──────────────────────────────────────────

        /// <inheritdoc />
        /// <remarks>
        /// NCalc evaluates expressions at runtime via an event-driven parameter
        /// resolution model. It does not support pre-compiling expressions into
        /// delegates (<see cref="EngineCapabilities.SupportsCompilation"/> = false)
        /// and cannot infer CLR return types statically
        /// (<see cref="EngineCapabilities.SupportsTyping"/> = false).
        /// </remarks>
        public EngineCapabilities Capabilities => EngineCapabilities.None;

        // -----------------------------------------------------------------
        //  Expression construction
        // -----------------------------------------------------------------

        /// <summary>
        /// Creates a configured <see cref="Expression"/> instance bound to the
        /// <paramref name="context"/> for parameter/function resolution.
        /// </summary>
        /// <remarks>
        /// NCalc's internal AST cache means parsing is skipped on subsequent calls
        /// for the same expression string. Each call gets its own <see cref="Expression"/>
        /// instance to guarantee thread safety.
        /// </remarks>
        private Expression BuildExpression(string expression, IExecutionContext context)
        {
            // EvaluateOptions.IgnoreCase: function and parameter names are case-insensitive.
            var ncalc = new Expression(expression, EvaluateOptions.IgnoreCase);

            // ── EvaluateParameter: resolves identifiers against the source object ──────
            ncalc.EvaluateParameter += (name, args) =>
            {
                if (context == null) return;

                if (context.TryGetVariable(name, out var varVal))
                {
                    args.Result = varVal;
                    return;
                }

                var source = context.Source;
                if (source == null) return;

                // Handle boolean literals (C# formats as "True"/"False", NCalc expects "true"/"false")
                var nameLower = name.ToLowerInvariant();
                if (nameLower == "true")
                {
                    args.Result = true;
                    return;
                }
                if (nameLower == "false")
                {
                    args.Result = false;
                    return;
                }

                try
                {
                    // Check resolvability first (uses IPropertyResolver's own cache)
                    // to avoid triggering NCalc's error handling for identifiers that
                    // are literal values, function outputs, or mathematical constants.
                    if (_propertyResolver.CanResolve(source.GetType(), name))
                        args.Result = _propertyResolver.Resolve(source, name);
                    // If not resolvable, leave args.Result unset.
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    // A resolver exception for this identifier — leave args.Result unset
                    // and let NCalc report an unknown-parameter error.
                    // OutOfMemoryException is NOT caught here to avoid masking critical failures.
                    _ = ex;
                }
            };

            // ── EvaluateFunction: prop("path") for explicit property path navigation ──
            ncalc.EvaluateFunction += (name, args) =>
            {
                if (!name.Equals("prop", StringComparison.OrdinalIgnoreCase)) return;
                if (args.Parameters.Length < 1) return;
                
                var source = context?.Source;
                if (source == null) { args.Result = null; return; }

                try
                {
                    var path = args.Parameters[0].Evaluate() as string;

                    if (!string.IsNullOrEmpty(path))
                        args.Result = _propertyResolver.Resolve(source, path!);
                }
                catch
                {
                    // Gracefully return null for unresolvable prop() calls.
                    args.Result = null;
                }
            };

            return ncalc;
        }
    }
}
