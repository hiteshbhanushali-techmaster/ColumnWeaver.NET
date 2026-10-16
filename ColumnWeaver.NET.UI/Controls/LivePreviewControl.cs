using System;
using System.Drawing;
using System.Windows.Forms;

namespace ColumnWeaver.NET.UI.Controls
{
    /// <summary>
    /// Displays the current parser output for a null source object in real time.
    /// Updated by <see cref="Forms.MainForm"/> after every <c>ConfigurationChanged</c> event.
    /// </summary>
    /// <remarks>
    /// Parsing with a null source reveals how the pipeline handles missing data
    /// (controlled by each column's <c>NullHandlingConfig</c>). This is useful for
    /// verifying separator layout, fixed-text columns, and null strategies without
    /// needing real input data.
    /// Pair with <see cref="TestInputControl"/> for JSON-driven testing.
    /// </remarks>
    [System.ComponentModel.DesignerCategory("Code")]
    public sealed class LivePreviewControl : UserControl
    {
        private Label      _lblTitle  = null!;
        private RichTextBox _rtbOutput = null!;


        public LivePreviewControl()
        {
            SuspendLayout();
            BackColor = Color.FromArgb(248, 250, 252);
            Padding   = new Padding(0);

            _lblTitle = new Label
            {
                Text      = "Live Preview (Compiled Engine ⚡)",
                Dock      = DockStyle.Top,
                Height    = 28,
                BackColor = Color.FromArgb(41, 121, 230),
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            };

            _rtbOutput = new RichTextBox
            {
                Dock        = DockStyle.Fill,
                ReadOnly    = true,
                BackColor   = Color.FromArgb(30, 30, 30),
                ForeColor   = Color.FromArgb(180, 230, 110),
                Font        = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                Padding     = new Padding(8),
                WordWrap    = true,
                ScrollBars  = RichTextBoxScrollBars.Vertical
            };

            Controls.Add(_rtbOutput);
            Controls.Add(_lblTitle);

            ResumeLayout(false);
        }

        /// <summary>
        /// Updates the displayed output. Shows error state when the string starts with "⚠ ".
        /// </summary>
        public void UpdatePreview(string output)
        {
            _rtbOutput.Text = output;

            if (output.StartsWith("⚠", StringComparison.Ordinal))
            {
                _rtbOutput.ForeColor = Color.FromArgb(255, 120, 80);
                _lblTitle.BackColor  = Color.FromArgb(120, 40, 20);
                _lblTitle.Text       = "Live Preview — Error";
            }
            else
            {
                _rtbOutput.ForeColor = Color.FromArgb(180, 230, 110);
                _lblTitle.BackColor  = Color.FromArgb(41, 121, 230);
                _lblTitle.Text       = "Live Preview (Compiled Engine ⚡)";
            }
        }
    }
}
