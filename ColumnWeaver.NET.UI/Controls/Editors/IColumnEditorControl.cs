using System;
using ColumnWeaver.NET.Core.Columns;

namespace ColumnWeaver.NET.UI.Controls.Editors
{
    /// <summary>
    /// Contract implemented by all column editor UserControls.
    /// </summary>
    /// <remarks>
    /// Each concrete implementation is responsible for exactly one
    /// <see cref="ColumnDefinition"/> subtype (e.g., <see cref="PropertyColumn"/>).
    /// Common properties (Name, Alias, Order, IsEnabled, Null Handling, Formatting,
    /// Replacement Rules) are handled by <see cref="BaseColumnEditorControl"/>.
    /// </remarks>
    public interface IColumnEditorControl
    {
        /// <summary>
        /// Raised whenever the user changes any field in the editor control.
        /// </summary>
        event EventHandler? ColumnChanged;

        /// <summary>
        /// Populates all editor controls from the provided <paramref name="column"/>.
        /// </summary>
        void LoadColumn(ColumnDefinition column);

        /// <summary>
        /// Reads all editor controls and returns a new <see cref="ColumnDefinition"/>
        /// with the current values. Returns null if the editor state is invalid.
        /// </summary>
        ColumnDefinition? SaveColumn();
    }
}
