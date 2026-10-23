using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using ColumnWeaver.NET.Core.Interfaces;

namespace ColumnWeaver.NET.Infrastructure.ExpressionEngine
{
    /// <summary>
    /// An <see cref="IExpressionEngine"/> implementation that compiles expressions and conditions into
    /// executable LINQ delegates, delegating to an underlying fallback engine when necessary.
    /// </summary>
    public sealed class LambdaExpressionEngine : IExpressionEngine
    {
        private static readonly MethodInfo _getResultMethod
            = typeof(IExecutionContext).GetMethod(nameof(IExecutionContext.GetResult))
              ?? throw new InvalidOperationException("IExecutionContext.GetResult not found.");

        private static readonly ConcurrentDictionary<string, ExpressionParseResult> _compiledCache
            = new ConcurrentDictionary<string, ExpressionParseResult>(StringComparer.Ordinal);

        private readonly IExpressionEngine _fallback;
        private readonly ConcurrentDictionary<string, bool> _validationCache;

        /// <summary>
        /// Gets the capabilities supported by this expression engine.
        /// </summary>
        public EngineCapabilities Capabilities =>
            new EngineCapabilities(supportsCompilation: true, supportsTyping: true);

        /// <summary>
        /// Initializes a new instance of the <see cref="LambdaExpressionEngine"/> class.
        /// </summary>
        /// <param name="fallback">The underlying expression engine used for fallback evaluation and validation.</param>
        public LambdaExpressionEngine(IExpressionEngine fallback)
        {
            _fallback        = fallback ?? throw new ArgumentNullException(nameof(fallback));
            _validationCache = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Evaluates an expression against the given execution context and returns its string representation.
        /// </summary>
        /// <param name="expression">The expression string to evaluate.</param>
        /// <param name="context">The execution context.</param>
        /// <returns>The evaluated string result.</returns>
        public string Evaluate(string expression, IExecutionContext context)
        {
            var result = EvaluateRaw(expression, context);
            return result?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Evaluates an expression against the given execution context and returns the raw object result.
        /// </summary>
        /// <param name="expression">The expression string to evaluate.</param>
        /// <param name="context">The execution context.</param>
        /// <returns>The raw evaluated object value.</returns>
        public object? EvaluateRaw(string expression, IExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(expression))
                throw new ArgumentException("Expression cannot be null or empty.", nameof(expression));

            if (IsSimplePropertyPath(expression))
            {
                try
                {
                    return EvaluatePropertyAccess(expression, context.Source);
                }
                catch
                {
                    return _fallback.EvaluateRaw(expression, context);
                }
            }

            return _fallback.EvaluateRaw(expression, context);
        }

        /// <summary>
        /// Evaluates a boolean condition against the given execution context.
        /// </summary>
        /// <param name="conditionExpression">The condition expression to evaluate.</param>
        /// <param name="context">The execution context.</param>
        /// <returns><c>true</c> if condition evaluates to true; otherwise, <c>false</c>.</returns>
        public bool EvaluateCondition(string conditionExpression, IExecutionContext context)
        {
            if (string.IsNullOrWhiteSpace(conditionExpression))
                throw new ArgumentException("Condition cannot be null or empty.", nameof(conditionExpression));

            var result = EvaluateRaw(conditionExpression, context);
            if (result is bool b) return b;

            try { return Convert.ToBoolean(result); }
            catch { return _fallback.EvaluateCondition(conditionExpression, context); }
        }

        /// <summary>
        /// Validates whether an expression is syntactically valid.
        /// </summary>
        /// <param name="expression">The expression string to validate.</param>
        /// <returns><c>true</c> if the expression is valid; otherwise, <c>false</c>.</returns>
        public bool ValidateExpression(string expression)
        {
            if (string.IsNullOrEmpty(expression)) return false;

            return _validationCache.GetOrAdd(expression, expr =>
            {
                if (IsSimplePropertyPath(expr)) return true;
                return _fallback.ValidateExpression(expr);
            });
        }

        /// <summary>
        /// Compiles an expression into an executable delegate.
        /// </summary>
        /// <param name="expression">The expression string to compile.</param>
        /// <param name="sourceType">The optional source input type.</param>
        /// <param name="dependencyIndexes">The column dependency indexes.</param>
        /// <param name="variableIndexes">The variable index mapping dictionary.</param>
        /// <param name="columnIndexes">The column index mapping dictionary.</param>
        /// <param name="variableValues">The compiled variable values list.</param>
        /// <returns>A compiled delegate, or <c>null</c> if the expression cannot be compiled.</returns>
        public Func<IExecutionContext, object?>? CompileExpression(
            string               expression,
            Type?                sourceType,
            IReadOnlyList<int>   dependencyIndexes,
            IReadOnlyDictionary<string, int>? variableIndexes = null,
            IReadOnlyDictionary<string, int>? columnIndexes = null,
            IReadOnlyList<object?>? variableValues = null)
        {
            if (string.IsNullOrWhiteSpace(expression)) return null;

            var compiled = CompiledExpressionBuilder.Compile(
                expression, sourceType, variableIndexes, columnIndexes, variableValues);
            if (compiled != null) return compiled;

            if (dependencyIndexes.Count == 0 && IsSimplePropertyPath(expression))
            {
                if (variableIndexes != null && variableIndexes.TryGetValue(expression, out int vIdx))
                {
                    return ctx => ctx.Variables[vIdx];
                }

                if (sourceType != null)
                {
                    return BuildSimplePropertyAccessDelegate(expression, sourceType);
                }
            }

            return null;
        }

        /// <summary>
        /// Compiles a condition expression into an executable boolean delegate.
        /// </summary>
        /// <param name="conditionExpression">The condition expression string to compile.</param>
        /// <param name="sourceType">The optional source input type.</param>
        /// <param name="dependencyIndexes">The column dependency indexes.</param>
        /// <param name="variableIndexes">The variable index mapping dictionary.</param>
        /// <param name="columnIndexes">The column index mapping dictionary.</param>
        /// <param name="variableValues">The compiled variable values list.</param>
        /// <returns>A compiled boolean delegate, or <c>null</c> if the condition cannot be compiled.</returns>
        public Func<IExecutionContext, bool>? CompileCondition(
            string               conditionExpression,
            Type?                sourceType,
            IReadOnlyList<int>   dependencyIndexes,
            IReadOnlyDictionary<string, int>? variableIndexes = null,
            IReadOnlyDictionary<string, int>? columnIndexes = null,
            IReadOnlyList<object?>? variableValues = null)
        {
            if (string.IsNullOrWhiteSpace(conditionExpression)) return null;

            var compiledCond = CompiledExpressionBuilder.CompileCondition(
                conditionExpression, sourceType, variableIndexes, columnIndexes, variableValues);
            if (compiledCond != null) return compiledCond;

            var compiled = CompileExpression(conditionExpression, sourceType, dependencyIndexes, variableIndexes, columnIndexes, variableValues);
            if (compiled == null) return null;

            return ctx =>
            {
                var raw = compiled(ctx);
                if (raw is bool b) return b;
                try { return Convert.ToBoolean(raw); }
                catch { return false; }
            };
        }

        private static string BuildCacheKey(string expression, Type? sourceType, IReadOnlyList<int> deps)
            => $"{sourceType?.Name ?? "null"}|{string.Join(",", deps)}|{expression}";

        private static bool IsSimplePropertyPath(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return false;
            foreach (char c in expression)
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '_') return false;
            if (expression.All(char.IsDigit)) return false;
            return true;
        }

        private static object? EvaluatePropertyAccess(string path, object? source)
        {
            if (source == null) return null;
            var segments = path.Split('.');
            object? current = source;
            foreach (var seg in segments)
            {
                if (current == null) return null;
                var prop = current.GetType().GetProperty(seg,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null) return null;
                current = prop.GetValue(current);
            }
            return current;
        }

        private Func<IExecutionContext, object?>? BuildSimplePropertyAccessDelegate(
            string path,
            Type?  sourceType)
        {
            if (sourceType == null) return null;

            try
            {
                var segments = path.Split('.');
                var param   = Expression.Parameter(typeof(IExecutionContext), "ctx");
                var srcExpr = Expression.Property(param, nameof(IExecutionContext.Source));
                Expression current = Expression.Convert(srcExpr, sourceType);

                foreach (var seg in segments)
                {
                    if (string.IsNullOrWhiteSpace(seg)) return null;
                    var prop = sourceType.GetProperty(seg,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop == null) return null;
                    current    = Expression.Property(current, prop);
                    sourceType = prop.PropertyType;
                }

                var body   = Expression.Convert(current, typeof(object));
                var lambda = Expression.Lambda<Func<IExecutionContext, object?>>(body, param);
                return lambda.Compile();
            }
            catch
            {
                return null;
            }
        }

        private Func<IExecutionContext, object?>? BuildFullExpressionDelegate(
            string             expression,
            Type?              sourceType,
            IReadOnlyList<int> dependencyIndexes)
        {
            try
            {
                var ctxParam = Expression.Parameter(typeof(IExecutionContext), "ctx");
                var depCount = dependencyIndexes.Count;

                var depAccesses = new Expression[depCount];
                for (int i = 0; i < depCount; i++)
                {
                    var call = Expression.Call(ctxParam, _getResultMethod,
                        Expression.Constant(dependencyIndexes[i]));
                    depAccesses[i] = Expression.Convert(call, typeof(object));
                }

                Expression body;
                if (sourceType != null && IsSimplePropertyPath(expression))
                {
                    var propAccess = BuildPropertyAccessWithDeps(expression, sourceType, ctxParam, depAccesses);
                    if (propAccess is not ConstantExpression constExpr || constExpr.Value != null)
                        body = propAccess;
                    else
                        body = BuildUntypedCall(expression, sourceType, ctxParam, dependencyIndexes);
                }
                else
                {
                    body = BuildUntypedCall(expression, sourceType, ctxParam, dependencyIndexes);
                }

                body   = Expression.Convert(body, typeof(object));
                var lambda = Expression.Lambda<Func<IExecutionContext, object?>>(body, ctxParam);
                return lambda.Compile();
            }
            catch
            {
                return null;
            }
        }

        private Expression BuildPropertyAccessWithDeps(
            string       path,
            Type         sourceType,
            ParameterExpression ctxParam,
            Expression[] depAccesses)
        {
            var segments = path.Split('.');
            Expression current = Expression.Convert(
                Expression.Property(ctxParam, nameof(IExecutionContext.Source)), sourceType);

            foreach (var seg in segments)
            {
                if (string.IsNullOrWhiteSpace(seg)) return Expression.Constant(null);
                var prop = sourceType.GetProperty(seg,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null) return Expression.Constant(null);
                current    = Expression.Property(current, prop);
                sourceType = prop.PropertyType;
            }
            return current;
        }

        private Expression BuildUntypedCall(
            string             expression,
            Type?              sourceType,
            ParameterExpression ctxParam,
            IReadOnlyList<int> dependencyIndexes)
        {
            var depCount = dependencyIndexes.Count;
            var depExprs = new Expression[depCount];
            for (int i = 0; i < depCount; i++)
            {
                var call = Expression.Call(ctxParam, _getResultMethod,
                    Expression.Constant(dependencyIndexes[i]));
                depExprs[i] = Expression.Convert(call, typeof(object));
            }

            return Expression.Call(
                Expression.Constant(_fallback),
                _fallbackEvaluateMethod,
                Expression.Constant(expression),
                ctxParam);
        }

        private static readonly MethodInfo _fallbackEvaluateMethod
            = typeof(IExpressionEngine).GetMethod(nameof(IExpressionEngine.EvaluateRaw))
              ?? throw new InvalidOperationException("EvaluateRaw not found.");

        private static readonly Regex _refPattern = new Regex(
            @"\$\{([^}]+)\}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static List<string> ExtractColumnReferences(string? expression)
        {
            if (string.IsNullOrEmpty(expression) || !expression!.Contains("${"))
                return new List<string>();

            var matches = _refPattern.Matches(expression!);
            if (matches.Count == 0)
                return new List<string>();

            var result = new List<string>();
            foreach (Match m in matches)
            {
                var name = m.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(name) && !result.Contains(name))
                    result.Add(name);
            }
            return result;
        }

        private enum ExpressionKind { Unknown, SimpleAccess }

        private sealed class ExpressionParseResult
        {
            public bool Success { get; set; }
            public ExpressionKind ParseKind { get; set; } = ExpressionKind.Unknown;
            public List<string> Dependencies { get; set; } = new List<string>();
        }

        private static ExpressionParseResult TryParseExpression(string expression, Type? sourceType)
        {
            if (IsSimplePropertyPath(expression))
            {
                var deps = ExtractColumnReferences(expression);
                return new ExpressionParseResult
                {
                    Success      = true,
                    ParseKind   = ExpressionKind.SimpleAccess,
                    Dependencies = deps
                };
            }
            return new ExpressionParseResult { Success = false };
        }
    }
}
