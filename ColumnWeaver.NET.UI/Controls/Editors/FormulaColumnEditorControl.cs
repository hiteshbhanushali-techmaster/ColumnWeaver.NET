using System;
using System.Drawing;
using System.Windows.Forms;
using ColumnWeaver.NET.Application.Pipeline;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.UI.Services;

namespace ColumnWeaver.NET.UI.Controls.Editors
{
    /// <summary>
    /// Editor for <see cref="FormulaColumn"/> — provides a multi-line expression
    /// text area, debounced inline syntax validation, and a live dependency hint
    /// that shows which columns the current expression references.
    /// </summary>
    [System.ComponentModel.DesignerCategory("Code")]
    public sealed class FormulaColumnEditorControl : BaseColumnEditorControl
    {
        private readonly UIParserService _service;

        // Controls
        private RichTextBox _rtbExpression = null!;
        private Label       _lblValidation = null!;
        private ColumnWeaver.NET.UI.Controls.Modern.ModernButton _btnValidate = null!;
        private Label       _lblDepHint    = null!;

        // Debounce timer — validates 600 ms after the user stops typing.
        private readonly Timer _debounceTimer;

        public FormulaColumnEditorControl(UIParserService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));

            // Debounce timer (fires once, then stops)
            _debounceTimer          = new Timer { Interval = 600 };
            _debounceTimer.Tick    += OnDebounceElapsed;

            // ── Header ────────────────────────────────────────────────────
            var hdr = CreateHeader("Formula Column",
                "Evaluates a NCalc expression. Use property names as variables.", "∑", Color.FromArgb(254, 246, 230), Color.FromArgb(170, 90, 0));

            var lbl = MakeLabel("Expression:");

            // ── Expression editor ─────────────────────────────────────────
            _rtbExpression = new RichTextBox
            {
                Width      = 400,
                Height     = 80,
                Font       = new Font("Consolas", 9.5f),
                Margin     = new Padding(4, 2, 4, 2),
                AcceptsTab = false,
                WordWrap   = true,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            // ── Validate button ───────────────────────────────────────────
            _btnValidate = new ColumnWeaver.NET.UI.Controls.Modern.ModernButton
            {
                Text      = "✔ Validate Syntax",
                Width     = 140,
                Height    = 28,
                Margin    = new Padding(4, 2, 4, 2),
                BackColor = Color.FromArgb(41, 121, 230)
            };

            // ── Inline validation feedback label ──────────────────────────
            _lblValidation = new Label
            {
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = Color.Gray,
                Text      = "Enter an expression and click Validate.",
                Margin    = new Padding(4, 2, 4, 4)
            };

            // ── Cross-column dependency hint (V2) ─────────────────────────
            _lblDepHint = new Label
            {
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8f, FontStyle.Regular),
                ForeColor = Color.FromArgb(30, 100, 180),
                Text      = string.Empty,
                Margin    = new Padding(4, 0, 4, 4)
            };

            // ── Examples hint ─────────────────────────────────────────────
            var hint = new Label
            {
                Text      = "Examples:  UnitPrice * Quantity    /    FirstName + ' ' + LastName\n" +
                            "Cross-column refs: ${ColumnName}   /   Nested: prop('Order.Customer.City')",
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8f),
                Margin    = new Padding(4, 0, 4, 4)
            };

            TypeSpecificPanel.Controls.AddRange(new System.Windows.Forms.Control[]
                { hdr, lbl, _rtbExpression, _btnValidate, _lblValidation, _lblDepHint, hint });

            _rtbExpression.TextChanged += OnExpressionChanged;
            _btnValidate.Click         += OnValidateClicked;
        }

        // -----------------------------------------------------------------
        //  IColumnEditorControl — Load / Save
        // -----------------------------------------------------------------

        protected override void LoadTypeSpecific(ColumnDefinition column)
        {
            if (column is FormulaColumn fc)
            {
                _rtbExpression.Text = fc.Expression;
                _lblValidation.Text = string.Empty;
                UpdateDepHint(fc.Expression);
            }
        }

        protected override ColumnDefinition CreateTypedColumn()
            => new FormulaColumn { Expression = _rtbExpression.Text.Trim() };

        // -----------------------------------------------------------------
        //  Event handlers
        // -----------------------------------------------------------------

        private void OnExpressionChanged(object? sender, EventArgs e)
        {
            // Clear old result and restart debounce timer.
            _lblValidation.Text      = string.Empty;
            _lblValidation.ForeColor = Color.Gray;

            _debounceTimer.Stop();
            _debounceTimer.Start();

            // Update dependency hint immediately — it's cheap.
            UpdateDepHint(_rtbExpression.Text);

            RaiseColumnChanged();
        }

        /// <summary>Called 600 ms after the last keystroke.</summary>
        private void OnDebounceElapsed(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();

            var expr = _rtbExpression.Text.Trim();
            if (string.IsNullOrWhiteSpace(expr)) return;

            // Validate on the UI thread (timer fires on the UI thread in WinForms).
            if (_service.ValidateExpression(expr))
            {
                _lblValidation.Text      = "✔  Syntax OK";
                _lblValidation.ForeColor = Color.SeaGreen;
            }
            else
            {
                _lblValidation.Text      = "✖  Syntax error — check the expression.";
                _lblValidation.ForeColor = Color.OrangeRed;
            }
        }

        private void OnValidateClicked(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();

            var expr = _rtbExpression.Text.Trim();
            if (string.IsNullOrWhiteSpace(expr))
            {
                _lblValidation.Text      = "⚠  Expression is empty.";
                _lblValidation.ForeColor = Color.OrangeRed;
                return;
            }

            if (_service.ValidateExpression(expr))
            {
                _lblValidation.Text      = "✔  Syntax is valid.";
                _lblValidation.ForeColor = Color.SeaGreen;
            }
            else
            {
                _lblValidation.Text      = "✖  Syntax error — check the expression.";
                _lblValidation.ForeColor = Color.Red;
            }
        }

        // -----------------------------------------------------------------
        //  V2: Dependency hint
        // -----------------------------------------------------------------

        /// <summary>
        /// Updates the <see cref="_lblDepHint"/> with the list of column names
        /// referenced via <c>${ColumnName}</c> tokens in the current expression.
        /// </summary>
        private void UpdateDepHint(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                _lblDepHint.Text = string.Empty;
                return;
            }

            var refs = ColumnReferenceParser.ExtractColumnReferences(expression);
            _lblDepHint.Text = refs.Count == 0
                ? string.Empty
                : $"🔗 References: {string.Join(", ", refs)}";
        }

        // -----------------------------------------------------------------
        //  Helpers
        // -----------------------------------------------------------------


        private static Label MakeLabel(string text)
            => new Label { Text = text, AutoSize = true, Margin = new Padding(4, 6, 4, 2) };
    }
}
