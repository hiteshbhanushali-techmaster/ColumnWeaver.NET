using System;
using System.Drawing;
using System.Windows.Forms;
using ColumnWeaver.NET.UI.Demo;
using ColumnWeaver.NET.UI.Services;

namespace ColumnWeaver.NET.UI.Controls
{
    /// <summary>
    /// Panel that lets the user paste a JSON object, click Parse, and see the
    /// full parser output using the current configuration.
    /// </summary>
    /// <remarks>
    /// Internally calls <see cref="UIParserService.TryTestParse"/> which uses
    /// <see cref="Infrastructure.PropertyResolution.JObjectPropertyResolver"/>
    /// so no real CLR type is needed — paste any JSON that matches the configured paths.
    ///
    /// <b>Usage tip:</b>
    /// <code>
    /// {
    ///   "Id": 1001,
    ///   "UnitPrice": 49.99,
    ///   "Quantity": 3,
    ///   "Customer": { "Name": "ACME Corp" }
    /// }
    /// </code>
    /// </remarks>
    [System.ComponentModel.DesignerCategory("Code")]
    public sealed class TestInputControl : UserControl
    {
        private readonly UIParserService _service;

        private Label       _lblTitle    = null!;
        private RichTextBox _rtbJson     = null!;
        private ColumnWeaver.NET.UI.Controls.Modern.ModernButton _btnParse = null!;
        private Label       _lblResult   = null!;
        private RichTextBox _rtbResult   = null!;
        private Label       _lblJsonHint = null!;

        public TestInputControl(UIParserService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));

            SuspendLayout();
            BackColor = Color.FromArgb(248, 250, 252);
            Padding   = new Padding(0);

            BuildUI();
            ResumeLayout(false);
            PopulateDefaultJson();
        }

        // -----------------------------------------------------------------
        //  UI Construction
        // -----------------------------------------------------------------

        private void BuildUI()
        {
            _lblTitle = new Label
            {
                Text      = "JSON Sandbox Test (Dynamic Engine) 📋",
                Dock      = DockStyle.Top,
                Height    = 28,
                BackColor = Color.FromArgb(41, 121, 230),
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(8, 0, 0, 0)
            };

            // JSON input area
            _rtbJson = new RichTextBox
            {
                Height      = 120,
                Dock        = DockStyle.Top,
                Font        = new Font("Consolas", 9f),
                BackColor   = Color.FromArgb(28, 35, 50),
                ForeColor   = Color.FromArgb(200, 220, 255),
                BorderStyle = BorderStyle.None,
                Padding     = new Padding(6),
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                AcceptsTab  = true
            };

            _lblJsonHint = new Label
            {
                Text      = "Paste a JSON object (evaluated via dynamic JObject sandbox engine).",
                Dock      = DockStyle.Top,
                Height    = 20,
                ForeColor = Color.Gray,
                Font      = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0)
            };

            // Toolbar: Parse button
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.FromArgb(236, 240, 244) };
            _btnParse = new ColumnWeaver.NET.UI.Controls.Modern.ModernButton
            {
                Text     = "▶  Parse",
                Width    = 90,
                Height   = 26,
                Location = new Point(8, 4),
                BackColor= Color.FromArgb(41, 121, 230),
                Font     = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            var btnClear = new ColumnWeaver.NET.UI.Controls.Modern.ModernButton
            {
                Text     = "✕ Clear",
                Width    = 70,
                Height   = 26,
                Location = new Point(104, 4),
                BackColor= Color.FromArgb(170, 180, 190)
            };

            toolbar.Controls.AddRange(new System.Windows.Forms.Control[] { _btnParse, btnClear });

            // Result area
            _lblResult = new Label
            {
                Text      = "Result:",
                Dock      = DockStyle.Top,
                Height    = 20,
                ForeColor = Color.FromArgb(44, 62, 80),
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0)
            };

            _rtbResult = new RichTextBox
            {
                Dock        = DockStyle.Fill,
                ReadOnly    = true,
                BackColor   = Color.FromArgb(30, 30, 30),
                ForeColor   = Color.FromArgb(100, 210, 145),
                Font        = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.None,
                Padding     = new Padding(8),
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                WordWrap    = true
            };

            // Layout (bottom-up for DockStyle.Top order)
            Controls.Add(_rtbResult);
            Controls.Add(_lblResult);
            Controls.Add(toolbar);
            Controls.Add(_lblJsonHint);
            Controls.Add(_rtbJson);
            Controls.Add(_lblTitle);

            // Events
            _btnParse.Click += OnParseClicked;
            btnClear.Click  += (_, _) => { _rtbJson.Clear(); _rtbResult.Clear(); };
        }

        // -----------------------------------------------------------------
        //  Event handlers
        // -----------------------------------------------------------------

        private void OnParseClicked(object? sender, EventArgs e)
        {
            var json = _rtbJson.Text.Trim();
            if (string.IsNullOrWhiteSpace(json))
            {
                ShowResult("⚠  No JSON input provided.", isError: true);
                return;
            }

            var result = _service.TryTestParse(json);
            ShowResult(result, result.StartsWith("⚠", StringComparison.Ordinal));
        }

        // -----------------------------------------------------------------
        //  Helpers
        // -----------------------------------------------------------------

        private void ShowResult(string text, bool isError)
        {
            _rtbResult.Text      = text;
            _rtbResult.ForeColor = isError
                ? Color.FromArgb(255, 100, 80)
                : Color.FromArgb(100, 210, 145);
            _lblResult.ForeColor = isError
                ? Color.FromArgb(180, 40, 20)
                : Color.FromArgb(44, 62, 80);
        }

        private void PopulateDefaultJson()
        {
            // Pre-fill with the same shape as the demo SampleOrder so the
            // user can immediately click Parse and see matching output.
            _rtbJson.Text = DemoConfigurationFactory.SampleOrderJson;
        }
    }
}
