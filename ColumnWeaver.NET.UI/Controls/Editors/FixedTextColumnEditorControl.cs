using System.Drawing;
using System.Windows.Forms;
using ColumnWeaver.NET.Core.Columns;

namespace ColumnWeaver.NET.UI.Controls.Editors
{
    /// <summary>
    /// Editor for <see cref="FixedTextColumn"/> — lets the user enter a literal
    /// text value that is always output verbatim regardless of the source object.
    /// </summary>
    [System.ComponentModel.DesignerCategory("Code")]
    public sealed class FixedTextColumnEditorControl : BaseColumnEditorControl
    {
        private TextBox _txtText = null!;

        public FixedTextColumnEditorControl()
        {
            var hdr  = CreateHeader("Fixed Text Column",
                "Always outputs a constant string literal, ignoring the source object.", "📝", Color.FromArgb(232, 248, 236), Color.FromArgb(20, 90, 40));
            var lbl  = MakeLabel("Text:");
            _txtText = new TextBox
            {
                Width  = 340,
                Margin = new Padding(4, 2, 4, 4)
            };

            TypeSpecificPanel.Controls.AddRange(
                new System.Windows.Forms.Control[] { hdr, lbl, _txtText });

            _txtText.TextChanged += (_, _) => RaiseColumnChanged();
        }

        protected override void LoadTypeSpecific(ColumnDefinition column)
        {
            if (column is FixedTextColumn fc)
                _txtText.Text = fc.Text;
        }

        protected override ColumnDefinition CreateTypedColumn()
            => new FixedTextColumn { Text = _txtText.Text };

        // -----------------------------------------------------------------
        //  Local helpers
        // -----------------------------------------------------------------


        private static Label MakeLabel(string text)
            => new Label { Text = text, AutoSize = true, Margin = new Padding(4, 6, 4, 2) };
    }
}
