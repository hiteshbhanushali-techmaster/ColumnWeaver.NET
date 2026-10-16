using System.Drawing;
using System.Windows.Forms;
using ColumnWeaver.NET.Core.Columns;

namespace ColumnWeaver.NET.UI.Controls.Editors
{
    /// <summary>
    /// Editor for <see cref="ConditionalColumn"/> — lets the user configure the
    /// boolean condition expression and the true/false branch values.
    /// </summary>
    /// <remarks>
    /// Branch values prefixed with <c>=</c> are treated as NCalc expressions by
    /// the evaluator (e.g., <c>= Price * 0.9</c>). Plain values are output literally.
    /// </remarks>
    [System.ComponentModel.DesignerCategory("Code")]
    public sealed class ConditionalColumnEditorControl : BaseColumnEditorControl
    {
        private TextBox _txtCondition  = null!;
        private TextBox _txtTrueValue  = null!;
        private TextBox _txtFalseValue = null!;

        public ConditionalColumnEditorControl()
        {
            var hdr  = CreateHeader("Conditional Column",
                "Evaluates a condition expression and outputs TrueValue or FalseValue.", "🔀", Color.FromArgb(245, 235, 250), Color.FromArgb(90, 40, 180));

            var tbl = new TableLayoutPanel
            {
                ColumnCount  = 2,
                AutoSize     = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin       = new Padding(4)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100f));

            _txtCondition  = new TextBox { Dock = DockStyle.Fill, Font = new System.Drawing.Font("Consolas", 9f) };
            _txtTrueValue  = new TextBox { Dock = DockStyle.Fill };
            _txtFalseValue = new TextBox { Dock = DockStyle.Fill };

            AddRow(tbl, "Condition:", _txtCondition);
            AddRow(tbl, "True Value:", _txtTrueValue);
            AddRow(tbl, "False Value:", _txtFalseValue);

            var hint = new Label
            {
                Text      = "Prefix True/False values with '=' to evaluate as expressions: e.g., = Price * 0.9",
                ForeColor = Color.Gray,
                AutoSize  = true,
                Font      = new System.Drawing.Font("Segoe UI", 8f),
                Margin    = new Padding(4, 0, 4, 4)
            };

            TypeSpecificPanel.Controls.AddRange(
                new System.Windows.Forms.Control[] { hdr, tbl, hint });

            _txtCondition.TextChanged  += (_, _) => RaiseColumnChanged();
            _txtTrueValue.TextChanged  += (_, _) => RaiseColumnChanged();
            _txtFalseValue.TextChanged += (_, _) => RaiseColumnChanged();
        }

        protected override void LoadTypeSpecific(ColumnDefinition column)
        {
            if (column is ConditionalColumn cc)
            {
                _txtCondition.Text  = cc.ConditionExpression;
                _txtTrueValue.Text  = cc.TrueValue  ?? string.Empty;
                _txtFalseValue.Text = cc.FalseValue ?? string.Empty;
            }
        }

        protected override ColumnDefinition CreateTypedColumn()
            => new ConditionalColumn
            {
                ConditionExpression = _txtCondition.Text.Trim(),
                TrueValue           = _txtTrueValue.Text,
                FalseValue          = _txtFalseValue.Text
            };

        // -----------------------------------------------------------------
        //  Helpers
        // -----------------------------------------------------------------


        private static void AddRow(TableLayoutPanel tbl, string label, System.Windows.Forms.Control ctrl)
        {
            tbl.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(2, 5, 2, 2) });
            ctrl.Margin = new Padding(2);
            tbl.Controls.Add(ctrl);
        }
    }
}
