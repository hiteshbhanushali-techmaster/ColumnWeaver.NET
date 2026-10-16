using ColumnWeaver.NET.Core.ParserConfiguration;

namespace ColumnWeaver.NET.Infrastructure.Serialization
{
    /// <summary>
    /// Defines the contract for serializing and deserializing a
    /// <see cref="ParserConfiguration"/> to/from a string or file.
    /// </summary>
    /// <remarks>
    /// Two built-in implementations are provided:
    /// <list type="bullet">
    ///   <item><see cref="JsonConfigurationSerializer"/> — Newtonsoft.Json with type discriminator</item>
    ///   <item><see cref="XmlConfigurationSerializer"/>  — DataContractSerializer with known types</item>
    /// </list>
    /// </remarks>
    public interface IConfigurationSerializer
    {
        /// <summary>
        /// Serializes <paramref name="config"/> to a string (JSON or XML).
        /// </summary>
        string Serialize(ParserConfiguration config);

        /// <summary>
        /// Deserializes a <see cref="ParserConfiguration"/> from a string.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">
        /// When the content cannot be deserialized into a valid configuration.
        /// </exception>
        ParserConfiguration Deserialize(string content);

        /// <summary>
        /// Serializes <paramref name="config"/> and writes it to <paramref name="filePath"/>.
        /// Creates the file if it does not exist; overwrites it if it does.
        /// </summary>
        void SerializeToFile(ParserConfiguration config, string filePath);

        /// <summary>
        /// Reads the file at <paramref name="filePath"/> and deserializes it into a
        /// <see cref="ParserConfiguration"/>.
        /// </summary>
        ParserConfiguration DeserializeFromFile(string filePath);
    }
}
