using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ColumnWeaver.NET.Core.Interfaces;

namespace ColumnWeaver.NET.Infrastructure.PropertyResolution
{
    /// <summary>
    /// A lightweight <see cref="IPropertyResolver"/> that navigates
    /// <see cref="JObject"/> graphs using Newtonsoft.Json's
    /// <see cref="JToken.SelectToken(string)"/> with dot-path notation.
    /// </summary>
    /// <remarks>
    /// <b>Purpose:</b> Used exclusively by the UI's test-input panel so that a user
    /// can paste a JSON snippet and immediately preview the parser output without
    /// needing a real CLR object. Not intended for production parsing pipelines.
    ///
    /// <b>Supported path syntax:</b>
    /// <list type="table">
    ///   <item><term><c>Name</c></term><description>Top-level property</description></item>
    ///   <item><term><c>Customer.Address.City</c></term><description>Nested path (SelectToken dot notation)</description></item>
    ///   <item><term><c>Orders[0].Amount</c></term><description>Array indexer (JSONPath subset)</description></item>
    /// </list>
    ///
    /// <b>Type coercion:</b> Returned values are coerced to their native CLR types
    /// (long, double, bool, string) so that arithmetic NCalc expressions work correctly.
    /// </remarks>
    public sealed class JObjectPropertyResolver : IPropertyResolver
    {
        // -----------------------------------------------------------------
        //  IPropertyResolver
        // -----------------------------------------------------------------

        /// <inheritdoc />
        public object? Resolve(object source, string propertyPath)
        {
            if (source is not JObject jObj)
                return null;

            if (string.IsNullOrWhiteSpace(propertyPath))
                return null;

            var token = jObj.SelectToken(propertyPath);
            if (token == null)
            {
                // Throwing here aligns with ReflectionPropertyResolver by telling
                // NCalcExpressionEngine that the identifier does not exist as a property,
                // leaving the NCalc parameter unresolved (triggering an evaluation error).
                throw new ArgumentException($"Property path '{propertyPath}' not found in JObject.", nameof(propertyPath));
            }
            
            return CoerceToken(token);
        }

        /// <inheritdoc />
        public bool CanResolve(Type sourceType, string propertyPath)
            => typeof(JObject).IsAssignableFrom(sourceType);

        /// <inheritdoc />
        /// <remarks>
        /// <see cref="JObject"/> is dynamically typed — there is no static type
        /// information for individual properties. Always returns <c>typeof(object)</c>
        /// so that <c>ExecutionPlanCompiler</c> falls back to the safe default.
        /// </remarks>
        public Type? ResolveType(Type sourceType, string propertyPath)
            => typeof(JObject).IsAssignableFrom(sourceType) ? typeof(object) : null;

        // -----------------------------------------------------------------
        //  Private helpers
        // -----------------------------------------------------------------

        /// <summary>
        /// Coerces a <see cref="JToken"/> to a native CLR type so that NCalc
        /// arithmetic and boolean expressions evaluate correctly against the value.
        /// </summary>
        private static object? CoerceToken(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            return token.Type switch
            {
                JTokenType.Integer => token.Value<long>(),
                JTokenType.Float   => token.Value<double>(),
                JTokenType.Boolean => token.Value<bool>(),
                JTokenType.String  => token.Value<string>(),
                JTokenType.Date    => token.Value<DateTime>(),
                _                  => token.ToString()
            };
        }
    }
}
