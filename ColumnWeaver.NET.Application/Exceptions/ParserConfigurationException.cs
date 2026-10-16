using System;

namespace ColumnWeaver.NET.Application.Exceptions
{
    /// <summary>
    /// Thrown when a <see cref="Core.ParserConfiguration.ParserConfiguration"/> is
    /// structurally invalid and cannot be used to drive the parser pipeline.
    /// Examples: duplicate column IDs, duplicate names, empty column list.
    /// </summary>
    public sealed class ParserConfigurationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ParserConfigurationException"/>
        /// with a descriptive message.
        /// </summary>
        public ParserConfigurationException(string message)
            : base(message) { }

        /// <summary>
        /// Initializes a new instance of <see cref="ParserConfigurationException"/>
        /// with a descriptive message and an inner exception.
        /// </summary>
        public ParserConfigurationException(string message, Exception? innerException)
            : base(message, innerException) { }
    }
}
