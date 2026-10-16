using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.ParserConfiguration;

namespace ColumnWeaver.NET.Infrastructure.Serialization
{
    /// <summary>
    /// Serializes and deserializes <see cref="ParserConfiguration"/> as indented XML
    /// using <see cref="DataContractSerializer"/> with all known
    /// <see cref="ColumnDefinition"/> subtypes registered as known types.
    /// </summary>
    /// <remarks>
    /// <b>Polymorphism strategy:</b>
    /// <see cref="DataContractSerializer"/> supports polymorphic collections natively via
    /// its <c>KnownTypes</c> setting. Each serialized <see cref="ColumnDefinition"/> element
    /// carries an <c>i:type</c> attribute that identifies the concrete CLR type, which is
    /// used to recreate the correct instance on deserialization — with zero modifications
    /// to the Core domain classes.
    ///
    /// <b>Sample output:</b>
    /// <code>
    /// &lt;ParserConfiguration xmlns:i="http://www.w3.org/2001/XMLSchema-instance"
    ///                      xmlns="http://schemas.datacontract.org/2004/07/..."&gt;
    ///   &lt;AutoSortByOrder&gt;true&lt;/AutoSortByOrder&gt;
    ///   &lt;Columns&gt;
    ///     &lt;ColumnDefinition i:type="PropertyColumn"&gt;
    ///       &lt;Name&gt;OrderId&lt;/Name&gt;
    ///       &lt;PropertyPath&gt;Id&lt;/PropertyPath&gt;
    ///     &lt;/ColumnDefinition&gt;
    ///   &lt;/Columns&gt;
    /// &lt;/ParserConfiguration&gt;
    /// </code>
    ///
    /// <b>Thread safety:</b> The <see cref="DataContractSerializer"/> instance is created
    /// once and stored as a static field. <c>DataContractSerializer</c> is documented as
    /// thread-safe for concurrent read and write operations.
    ///
    /// <b>Encryption / validation:</b> No XML signing or schema validation is applied.
    /// Add a wrapper layer for production scenarios requiring those controls.
    /// </remarks>
    public sealed class XmlConfigurationSerializer : IConfigurationSerializer
    {
        // -----------------------------------------------------------------
        //  Known column types for polymorphic deserialization
        // -----------------------------------------------------------------

        /// <summary>
        /// All concrete <see cref="ColumnDefinition"/> subtypes that may appear
        /// inside a serialized <c>Columns</c> collection.
        /// Add new subtypes here to support custom column types.
        /// </summary>
        private static readonly Type[] KnownColumnTypes =
        {
            typeof(PropertyColumn),
            typeof(FixedTextColumn),
            typeof(FormulaColumn),
            typeof(ConditionalColumn),
            typeof(CustomFunctionColumn)
        };

        // -----------------------------------------------------------------
        //  DataContractSerializer (thread-safe, created once)
        // -----------------------------------------------------------------

        private static readonly DataContractSerializer Serializer =
            new DataContractSerializer(
                typeof(ParserConfiguration),
                new DataContractSerializerSettings
                {
                    KnownTypes               = KnownColumnTypes,
                    PreserveObjectReferences = false,
                    MaxItemsInObjectGraph    = int.MaxValue
                });

        // -----------------------------------------------------------------
        //  XmlWriter settings
        // -----------------------------------------------------------------

        /// <summary>
        /// Settings for indented, UTF-8 encoded XML output.
        /// </summary>
        private static readonly XmlWriterSettings WriterSettings = new XmlWriterSettings
        {
            Indent             = true,
            IndentChars        = "  ",
            Encoding           = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            NewLineOnAttributes= false,
            CloseOutput        = false
        };

        // -----------------------------------------------------------------
        //  IConfigurationSerializer
        // -----------------------------------------------------------------

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">When <paramref name="config"/> is null.</exception>
        public string Serialize(ParserConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            using var stream = new MemoryStream();
            using var writer = XmlWriter.Create(stream, WriterSettings);

            Serializer.WriteObject(writer, config);
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentException">When <paramref name="content"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">
        /// When the XML cannot be deserialized into a <see cref="ParserConfiguration"/>.
        /// </exception>
        public ParserConfiguration Deserialize(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("XML content cannot be null or empty.", nameof(content));

            try
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                using var stream = new MemoryStream(bytes);
                using var reader = XmlReader.Create(stream);

                var result = Serializer.ReadObject(reader) as ParserConfiguration;

                return result ?? throw new InvalidOperationException(
                    "Deserialization returned null. Ensure the XML is a valid ParserConfiguration document.");
            }
            catch (SerializationException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize ParserConfiguration from XML: {ex.Message}", ex);
            }
            catch (XmlException ex)
            {
                throw new InvalidOperationException(
                    $"Malformed XML: {ex.Message}", ex);
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

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = XmlWriter.Create(stream, WriterSettings);

            Serializer.WriteObject(writer, config);
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

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = XmlReader.Create(stream);

                var result = Serializer.ReadObject(reader) as ParserConfiguration;

                return result ?? throw new InvalidOperationException(
                    "Deserialization returned null for file: " + filePath);
            }
            catch (SerializationException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize '{filePath}': {ex.Message}", ex);
            }
        }
    }
}
