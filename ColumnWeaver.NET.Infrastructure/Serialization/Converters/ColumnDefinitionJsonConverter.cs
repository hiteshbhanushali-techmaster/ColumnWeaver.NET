using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using ColumnWeaver.NET.Core.Columns;

namespace ColumnWeaver.NET.Infrastructure.Serialization.Converters
{
    /// <summary>
    /// Custom Newtonsoft.Json converter that handles the polymorphic
    /// <see cref="ColumnDefinition"/> hierarchy using a <c>"columnType"</c>
    /// string discriminator field.
    /// </summary>
    /// <remarks>
    /// <b>Serialized format:</b>
    /// <code>
    /// {
    ///   "columnType": "Property",
    ///   "Name": "OrderId",
    ///   "Order": 0,
    ///   "PropertyPath": "Id"
    /// }
    /// </code>
    ///
    /// <b>Discriminator values:</b>
    /// <list type="table">
    ///   <listheader><term>columnType</term><description>Concrete type</description></listheader>
    ///   <item><term>Property</term><description><see cref="PropertyColumn"/></description></item>
    ///   <item><term>FixedText</term><description><see cref="FixedTextColumn"/></description></item>
    ///   <item><term>Formula</term><description><see cref="FormulaColumn"/></description></item>
    ///   <item><term>Conditional</term><description><see cref="ConditionalColumn"/></description></item>
    ///   <item><term>CustomFunction</term><description><see cref="CustomFunctionColumn"/></description></item>
    /// </list>
    ///
    /// <b>Infinite-recursion prevention:</b>
    /// The converter uses a dedicated <c>_fallbackSerializer</c> (without itself registered)
    /// when converting to/from <see cref="JObject"/>. This prevents the converter from
    /// triggering itself during the intermediate serialization step.
    /// </remarks>
    public sealed class ColumnDefinitionJsonConverter : JsonConverter
    {
        // -----------------------------------------------------------------
        //  Type discriminator map
        // -----------------------------------------------------------------

        private static readonly Dictionary<string, Type> TypeMap =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                ["Property"]       = typeof(PropertyColumn),
                ["FixedText"]      = typeof(FixedTextColumn),
                ["Formula"]        = typeof(FormulaColumn),
                ["Conditional"]    = typeof(ConditionalColumn),
                ["CustomFunction"] = typeof(CustomFunctionColumn)
            };

        private static readonly Dictionary<Type, string> ReverseTypeMap =
            new Dictionary<Type, string>
            {
                [typeof(PropertyColumn)]      = "Property",
                [typeof(FixedTextColumn)]     = "FixedText",
                [typeof(FormulaColumn)]       = "Formula",
                [typeof(ConditionalColumn)]   = "Conditional",
                [typeof(CustomFunctionColumn)]= "CustomFunction"
            };

        // -----------------------------------------------------------------
        //  Fallback serializer (no ColumnDefinitionJsonConverter to prevent recursion)
        // -----------------------------------------------------------------

        /// <summary>
        /// Used internally when converting to/from <see cref="JObject"/> to avoid
        /// infinite recursion while this converter is active.
        /// </summary>
        private static readonly JsonSerializer FallbackSerializer = JsonSerializer.Create(
            new JsonSerializerSettings
            {
                NullValueHandling      = NullValueHandling.Ignore,
                DefaultValueHandling   = DefaultValueHandling.Include,
                Converters             = { new StringEnumConverter() }
            });

        // -----------------------------------------------------------------
        //  JsonConverter overrides
        // -----------------------------------------------------------------

        /// <inheritdoc />
        /// <remarks>
        /// Returns true for <see cref="ColumnDefinition"/> and all of its derived types.
        /// </remarks>
        public override bool CanConvert(Type objectType)
            => typeof(ColumnDefinition).IsAssignableFrom(objectType);

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var column   = (ColumnDefinition)value;
            var typeName = GetTypeName(column.GetType());

            // Serialize the column's own properties without this converter
            // (prevents infinite recursion).
            var jo = JObject.FromObject(column, FallbackSerializer);

            // Inject the type discriminator as the very first property for readability.
            jo.AddFirst(new JProperty("columnType", typeName));

            jo.WriteTo(writer);
        }

        /// <inheritdoc />
        public override object? ReadJson(
            JsonReader     reader,
            Type           objectType,
            object?        existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var jo = JObject.Load(reader);

            // ── Read the discriminator ─────────────────────────────────────
            var typeName = jo["columnType"]?.Value<string>();

            if (string.IsNullOrWhiteSpace(typeName))
                throw new JsonSerializationException(
                    "Missing or empty 'columnType' discriminator field. " +
                    "Each column definition in JSON must include a 'columnType' field. " +
                    $"Valid values: {string.Join(", ", TypeMap.Keys)}.");

            if (!TypeMap.TryGetValue(typeName!, out var targetType))
                throw new JsonSerializationException(
                    $"Unknown columnType value '{typeName}'. " +
                    $"Valid values: {string.Join(", ", TypeMap.Keys)}.");

            // Remove the discriminator before passing to the fallback deserializer
            // so it does not cause an "unknown property" warning.
            jo.Remove("columnType");

            // ── Deserialize into the resolved concrete type ────────────────
            var result = jo.ToObject(targetType, FallbackSerializer);

            if (result == null)
                throw new JsonSerializationException(
                    $"Failed to deserialize column of type '{typeName}'.");

            return result;
        }

        // -----------------------------------------------------------------
        //  Helpers
        // -----------------------------------------------------------------

        private static string GetTypeName(Type type)
        {
            if (ReverseTypeMap.TryGetValue(type, out var name))
                return name;

            throw new JsonSerializationException(
                $"Column type '{type.FullName}' is not registered in {nameof(ColumnDefinitionJsonConverter)}. " +
                "Register custom column types using the appropriate overloads.");
        }
    }
}
