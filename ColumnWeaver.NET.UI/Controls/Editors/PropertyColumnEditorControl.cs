using System.Drawing;
using System.Windows.Forms;
using ColumnWeaver.NET.Core.Columns;

namespace ColumnWeaver.NET.UI.Controls.Editors
{
    /// <summary>
    /// Editor for <see cref="PropertyColumn"/> — lets the user enter a dot-path
    /// property string (e.g., <c>Customer.Address.City</c> or <c>Orders[0].Amount</c>).
    /// </summary>
    [System.ComponentModel.DesignerCategory("Code")]
    public sealed class PropertyColumnEditorControl : BaseColumnEditorControl
    {
        private TextBox _txtPropertyPath = null!;

        public PropertyColumnEditorControl()
        {
            // ── Type-specific controls added to TypeSpecificPanel ──────────
            var hdr = CreateHeader("Property Column", "Resolves a dot-path on the source object.", "📄", Color.FromArgb(232, 240, 254), Color.FromArgb(30, 60, 120));

            var lbl  = MakeLabel("Property Path:");
            _txtPropertyPath = new TextBox
            {
                Dock    = DockStyle.None,
                Width   = 340,
                Margin  = new Padding(4, 2, 4, 2)
            };

            var hint = new Label
            {
                Text      = "Examples: Name  /  Customer.Address.City  /  Orders[0].Amount",
                ForeColor = Color.Gray,
                AutoSize  = true,
                Margin    = new Padding(4, 0, 4, 4),
                Font      = new System.Drawing.Font("Segoe UI", 8f)
            };

            TypeSpecificPanel.Controls.AddRange(new System.Windows.Forms.Control[]
                { hdr, lbl, _txtPropertyPath, hint });

            _txtPropertyPath.TextChanged += (_, _) => RaiseColumnChanged();
        }

        protected override void LoadTypeSpecific(ColumnDefinition column)
        {
            if (column is PropertyColumn pc)
                _txtPropertyPath.Text = pc.PropertyPath;
        }

        protected override ColumnDefinition CreateTypedColumn()
            => new PropertyColumn { PropertyPath = _txtPropertyPath.Text.Trim() };

        // -----------------------------------------------------------------
        //  Local UI helpers
        // -----------------------------------------------------------------



        private static Label MakeLabel(string text)
            => new Label { Text = text, AutoSize = true, Margin = new Padding(4, 6, 4, 2) };
    }
}
