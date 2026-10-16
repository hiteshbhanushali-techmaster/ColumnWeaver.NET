using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ColumnWeaver.NET.Core.ParserConfiguration;
using ColumnWeaver.NET.Infrastructure.Serialization.Converters;

namespace ColumnWeaver.NET.Infrastructure.Serialization
{
    /// <summary>
    /// Serializes and deserializes <see cref="ParserConfiguration"/> as formatted JSON
    /// using Newtonsoft.Json with a custom <see cref="ColumnDefinitionJsonConverter"/>
    /// for polymorphic column handling.
    /// </summary>
    /// <remarks>
    /// <b>Output format:</b>
    /// <code>
    /// {
    ///   "Name": "OrderParser",
    ///   "GlobalSeparator": "|",
    ///   "AutoSortByOrder": true,
    ///   "Columns": [
    ///     {
    ///       "columnType": "Property",
    ///       "Name": "OrderId",
    ///       "Order": 0,
    ///       "IsEnabled": true,
    ///       "PropertyPath": "Id",
    ///       "NullHandling": { "Strategy": "EmptyString" },
    ///       "Formatting": {}
    ///     },
    ///     {
    ///       "columnType": "Formula",
    ///       "Name": "Total",
    ///       "Order": 1,
    ///       "Expression": "UnitPrice * Quantity"
    ///     }
    ///   ]
    /// }
    /// </code>
    ///
    /// <b>Enum values</b> are serialized as strings (e.g., <c>"EmptyString"</c> rather
    /// than <c>0</c>) for human readability and forward compatibility.
    ///
    /// <b>Null and default values</b> are omitted from the output
    /// (<see cref="NullValueHandling.Ignore"/>).
    ///
    /// <b>Thread safety:</b> The internal <see cref="JsonSerializerSettings"/> and
    /// <see cref="JsonSerializer"/> instances are immutable after construction.
    /// Concurrent calls to <see cref="Serialize"/> and <see cref="Deserialize"/> are safe.
    /// </remarks>
    public sealed class JsonConfigurationSerializer : IConfigurationSerializer
    {
        // -----------------------------------------------------------------
        //  Serializer settings (constructed once, immutable)
        // -----------------------------------------------------------------

        private readonly JsonSerializerSettings _settings;

        // -----------------------------------------------------------------
        //  Constructor
        // -----------------------------------------------------------------

        /// <summary>
        /// Initializes a new <see cref="JsonConfigurationSerializer"/> with
        /// production-ready, human-readable serialization settings.
        /// </summary>
        public JsonConfigurationSerializer()
        {
            _settings = new JsonSerializerSettings
            {
                Formatting           = Formatting.Indented,
                NullValueHandling    = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Include,
                DateFormatHandling   = DateFormatHandling.IsoDateFormat,
                Converters =
                {
                    // Order matters: ColumnDefinitionJsonConverter before StringEnumConverter.
                    new ColumnDefinitionJsonConverter(),
                    new StringEnumConverter()          // Enums as strings, not integers.
                }
            };
        }

        // -----------------------------------------------------------------
        //  IConfigurationSerializer
        // -----------------------------------------------------------------

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">When <paramref name="config"/> is null.</exception>
        public string Serialize(ParserConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return JsonConvert.SerializeObject(config, _settings);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException">When <paramref name="content"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">
        /// When the JSON cannot be deserialized into a <see cref="ParserConfiguration"/>.
        /// </exception>
        public ParserConfiguration Deserialize(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("JSON content cannot be null or empty.", nameof(content));

            try
            {
                var result = JsonConvert.DeserializeObject<ParserConfiguration>(content, _settings);
                return result ?? throw new InvalidOperationException(
                    "Deserialization returned null. Ensure the JSON is a valid ParserConfiguration object.");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize ParserConfiguration from JSON: {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException">When <paramref name="filePath"/> is null or empty.</exception>
        /// <exception cref="IOException">On file write errors.</exception>
        public void SerializeToFile(ParserConfiguration config, string filePath)
        {
            if (config  == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            // Ensure the directory exists.
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(filePath, Serialize(config), Encoding.UTF8);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException">When <paramref name="filePath"/> is null or empty.</exception>
        /// <exception cref="FileNotFoundException">When the file does not exist.</exception>
        public ParserConfiguration DeserializeFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException(
                    $"Configuration file not found: '{filePath}'.", filePath);

            return Deserialize(File.ReadAllText(filePath, Encoding.UTF8));
        }
    }
}
