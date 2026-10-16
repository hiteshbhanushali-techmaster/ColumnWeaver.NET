using System;
using System.Drawing;
using System.Windows.Forms;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Enums;
using ColumnWeaver.NET.Core.Models;

namespace ColumnWeaver.NET.UI.Controls.Editors
{
    /// <summary>
    /// Abstract base for all column editor UserControls.
    /// Provides the tab-based shell (Definition | Null Handling | Formatting | Replacements)
    /// and implements all common property binding. Derived classes populate only the
    /// type-specific area inside the "Definition" tab.
    /// </summary>
    /// <remarks>
    /// <b>Extension pattern for derived classes:</b>
    /// <code>
    ///   public sealed class PropertyColumnEditorControl : BaseColumnEditorControl
    ///   {
    ///       public PropertyColumnEditorControl()
    ///       {
    ///           // After base constructor, TypeSpecificPanel is ready.
    ///           TypeSpecificPanel.Controls.Add(myLabel);
    ///           TypeSpecificPanel.Controls.Add(myTextBox);
    ///       }
    ///
    ///       protected override void LoadTypeSpecific(ColumnDefinition col) { ... }
    ///       protected override ColumnDefinition CreateTypedColumn()         { ... }
    ///   }
    /// </code>
    /// No virtual methods are called from the base constructor — the
    /// <see cref="TypeSpecificPanel"/> is a completed control that derived classes
    /// safely populate in their OWN constructors after <c>base()</c> returns.
    /// </remarks>
    [System.ComponentModel.DesignerCategory("Code")]
    public abstract class BaseColumnEditorControl : UserControl, IColumnEditorControl
    {
        // -----------------------------------------------------------------
        //  Column identity (preserved across Save/Load cycles)
        // -----------------------------------------------------------------

        private Guid _columnId = Guid.NewGuid();

        // -----------------------------------------------------------------
        //  Event suppression (prevents change events during data load)
        // -----------------------------------------------------------------

        private bool _loading;

        // -----------------------------------------------------------------
        //  Common controls — Definition tab
        // -----------------------------------------------------------------

        private TextBox         _txtName       = null!;
        private TextBox         _txtAlias      = null!;
        private NumericUpDown   _nudOrder      = null!;
        private CheckBox        _chkEnabled    = null!;
        private TextBox         _txtSeparator  = null!;

        // -----------------------------------------------------------------
        //  Common controls — Null Handling tab
        // -----------------------------------------------------------------

        private ComboBox    _cmbNullStrategy    = null!;
        private TextBox     _txtNullDefault     = null!;
        private TextBox     _txtNullException   = null!;
        private Label       _lblNullDefault     = null!;
        private Label       _lblNullException   = null!;

        // -----------------------------------------------------------------
        //  Common controls — Formatting tab
        // -----------------------------------------------------------------

        private TextBox       _txtFormatString  = null!;
        private ComboBox      _cmbCasing        = null!;
        private ComboBox      _cmbTrim          = null!;
        private NumericUpDown _nudMaxLength      = null!;
        private TextBox       _txtPrefix        = null!;
        private TextBox       _txtSuffix        = null!;
        private NumericUpDown _nudPaddedWidth   = null!;
        private TextBox       _txtPaddingChar   = null!;
        private ComboBox      _cmbPaddingAlign  = null!;

        // -----------------------------------------------------------------
        //  Common controls — Replacements tab
        // -----------------------------------------------------------------

        private DataGridView _dgvRules = null!;

        // -----------------------------------------------------------------
        //  Exposed to derived classes
        // -----------------------------------------------------------------

        /// <summary>
        /// The <see cref="FlowLayoutPanel"/> inside the "Definition" tab where derived
        /// classes add their type-specific controls. Safe to use after this constructor returns.
        /// </summary>
        protected FlowLayoutPanel TypeSpecificPanel { get; private set; } = null!;

        // -----------------------------------------------------------------
        //  IColumnEditorControl
        // -----------------------------------------------------------------

        /// <inheritdoc />
        public event EventHandler? ColumnChanged;

        // -----------------------------------------------------------------
        //  Constructor
        // -----------------------------------------------------------------

        protected BaseColumnEditorControl()
        {
            SuspendLayout();

            AutoScroll = true;
            BackColor = Color.FromArgb(248, 250, 252);
            Padding = new Padding(10, 10, 10, 50);

            var panel = BuildPropertiesPanel();
            panel.Dock = DockStyle.Top;
            Controls.Add(panel);

            ResumeLayout(false);
        }

        // -----------------------------------------------------------------
        //  IColumnEditorControl — Load / Save
        // -----------------------------------------------------------------

        /// <inheritdoc />
        public void LoadColumn(ColumnDefinition column)
        {
            if (column == null) throw new ArgumentNullException(nameof(column));

            _loading = true;
            try
            {
                _columnId = column.Id;
                LoadCommonProperties(column);
                LoadTypeSpecific(column);
            }
            finally
            {
                _loading = false;
            }
        }

        /// <inheritdoc />
        public ColumnDefinition? SaveColumn()
        {
            var col     = CreateTypedColumn();
            col.Id      = _columnId;
            ApplyCommonProperties(col);
            return col;
        }

        // -----------------------------------------------------------------
        //  Abstract members — implemented by derived classes
        // -----------------------------------------------------------------

        /// <summary>Populates type-specific controls from <paramref name="column"/>.</summary>
        protected abstract void LoadTypeSpecific(ColumnDefinition column);

        /// <summary>
        /// Creates a new instance of the concrete column type with the type-specific
        /// properties already set (e.g., <c>PropertyPath = txtPropertyPath.Text</c>).
        /// The base class will then set all common properties.
        /// </summary>
        protected abstract ColumnDefinition CreateTypedColumn();

        // -----------------------------------------------------------------
        //  Protected helpers for derived classes
        // -----------------------------------------------------------------

        /// <summary>
        /// Raises <see cref="ColumnChanged"/> unless a load operation is in progress.
        /// Derived classes call this in their control event handlers.
        /// </summary>
        protected void RaiseColumnChanged()
        {
            if (!_loading)
                ColumnChanged?.Invoke(this, EventArgs.Empty);
        }

        // -----------------------------------------------------------------
        //  Common property load / save
        // -----------------------------------------------------------------

        private void LoadCommonProperties(ColumnDefinition col)
        {
            _txtName.Text      = col.Name;
            _txtAlias.Text     = col.Alias ?? string.Empty;
            _nudOrder.Value    = col.Order;
            _chkEnabled.Checked= col.IsEnabled;
            _txtSeparator.Text = col.SeparatorOverride ?? string.Empty;

            // Null handling
            _cmbNullStrategy.SelectedItem   = col.NullHandling.Strategy;
            _txtNullDefault.Text            = col.NullHandling.DefaultValue ?? string.Empty;
            _txtNullException.Text          = col.NullHandling.ExceptionMessage ?? string.Empty;
            UpdateNullHandlingVisibility(col.NullHandling.Strategy);

            // Formatting
            _txtFormatString.Text           = col.Formatting.FormatString ?? string.Empty;
            _cmbCasing.SelectedItem         = col.Formatting.CasingTransform;
            _cmbTrim.SelectedItem           = col.Formatting.TrimBehavior;
            _nudMaxLength.Value             = col.Formatting.MaxLength;
            _txtPrefix.Text                 = col.Formatting.Prefix ?? string.Empty;
            _txtSuffix.Text                 = col.Formatting.Suffix ?? string.Empty;
            _nudPaddedWidth.Value           = col.Formatting.PaddedWidth;
            _txtPaddingChar.Text            = col.Formatting.PaddingChar == '\0'
                ? " " : col.Formatting.PaddingChar.ToString();
            _cmbPaddingAlign.SelectedItem   = col.Formatting.PaddingAlignment;

            // Replacement rules
            _dgvRules.Rows.Clear();
            foreach (var rule in col.ReplacementRules)
                _dgvRules.Rows.Add(rule.SourceValue, rule.TargetValue, rule.UseRegex, rule.IsCaseSensitive);
        }

        private void ApplyCommonProperties(ColumnDefinition col)
        {
            col.Name              = _txtName.Text.Trim();
            col.Alias             = string.IsNullOrWhiteSpace(_txtAlias.Text) ? null : _txtAlias.Text.Trim();
            col.Order             = (int)_nudOrder.Value;
            col.IsEnabled         = _chkEnabled.Checked;
            col.SeparatorOverride = string.IsNullOrWhiteSpace(_txtSeparator.Text) ? null : _txtSeparator.Text;

            // Null handling
            col.NullHandling.Strategy         = (NullHandlingStrategy)_cmbNullStrategy.SelectedItem!;
            col.NullHandling.DefaultValue     = NullIfEmpty(_txtNullDefault.Text);
            col.NullHandling.ExceptionMessage = NullIfEmpty(_txtNullException.Text);

            // Formatting
            col.Formatting.FormatString    = NullIfEmpty(_txtFormatString.Text);
            col.Formatting.CasingTransform = (CasingTransform)_cmbCasing.SelectedItem!;
            col.Formatting.TrimBehavior    = (TrimBehavior)_cmbTrim.SelectedItem!;
            col.Formatting.MaxLength       = (int)_nudMaxLength.Value;
            col.Formatting.Prefix          = NullIfEmpty(_txtPrefix.Text);
            col.Formatting.Suffix          = NullIfEmpty(_txtSuffix.Text);
            col.Formatting.PaddedWidth     = (int)_nudPaddedWidth.Value;
            col.Formatting.PaddingChar     = string.IsNullOrEmpty(_txtPaddingChar.Text)
                ? ' ' : _txtPaddingChar.Text[0];
            col.Formatting.PaddingAlignment= (PaddingAlignment)_cmbPaddingAlign.SelectedItem!;

            // Replacement rules
            col.ReplacementRules.Clear();
            foreach (DataGridViewRow row in _dgvRules.Rows)
            {
                if (row.IsNewRow) continue;
                col.ReplacementRules.Add(new ReplacementRule
                {
                    SourceValue     = (string?)row.Cells[0].Value ?? string.Empty,
                    TargetValue     = (string?)row.Cells[1].Value ?? string.Empty,
                    UseRegex        = (bool?)row.Cells[2].Value ?? false,
                    IsCaseSensitive = (bool?)row.Cells[3].Value ?? true
                });
            }
        }

        // -----------------------------------------------------------------
        //  TabControl construction
        // -----------------------------------------------------------------

        private Control BuildPropertiesPanel()
        {
            var pnl = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill
            };
            
            pnl.Controls.Add(BuildDefinitionTab());
            pnl.Controls.Add(MakeSectionHeader("Null Handling"));
            pnl.Controls.Add(BuildNullHandlingTab());
            pnl.Controls.Add(MakeSectionHeader("Formatting"));
            pnl.Controls.Add(BuildFormattingTab());
            pnl.Controls.Add(MakeSectionHeader("Replacement Rules"));
            pnl.Controls.Add(BuildReplacementsTab());

            return pnl;
        }

        private static Label MakeSectionHeader(string text) => new Label { Text = text, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Color.FromArgb(41, 121, 230), AutoSize = true, Margin = new Padding(0, 15, 0, 5) };

        // ── Tab 1: Definition ────────────────────────────────────────────

        private TableLayoutPanel BuildDefinitionTab()
        {
            var page = new TableLayoutPanel { BackColor = Color.FromArgb(248, 250, 252), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0), ColumnCount = 1 };
            page.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            // TypeSpecificPanel — derived classes add controls here AFTER base() returns
            TypeSpecificPanel = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents  = false,
                AutoSize      = true,
                AutoSizeMode  = AutoSizeMode.GrowAndShrink,
                Padding       = new Padding(8, 8, 8, 4)
            };

            // Common basic properties
            var grp = new GroupBox
            {
                Text    = "Column Properties",
                Dock    = DockStyle.Fill,
                Padding = new Padding(8),
                AutoSize= true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            var tbl = new TableLayoutPanel
            {
                ColumnCount       = 2,
                AutoSize          = true,
                AutoSizeMode      = AutoSizeMode.GrowAndShrink,
                Dock              = DockStyle.Top,
                Padding           = new Padding(4)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,  100f));

            _txtName      = new TextBox { Dock = DockStyle.Fill };
            _txtAlias     = new TextBox { Dock = DockStyle.Fill };
            _nudOrder     = new NumericUpDown { Minimum = 0, Maximum = 999, Dock = DockStyle.Fill };
            _chkEnabled   = new ColumnWeaver.NET.UI.Controls.Modern.ModernToggle { Checked = true };
            var _chkLbl   = new Label { Text = "Enabled", AutoSize = true, Margin = new Padding(0,2,0,0) };
            var panEnabled = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            panEnabled.Controls.Add(_chkEnabled);
            panEnabled.Controls.Add(_chkLbl);
            _txtSeparator = new TextBox  { Dock = DockStyle.Fill };

            AddRow(tbl, "Name:", _txtName);
            AddRow(tbl, "Alias:", _txtAlias);
            AddRow(tbl, "Order:", _nudOrder);
            AddRow(tbl, "Step Override:", panEnabled);
            AddRow(tbl, "", _txtSeparator);

            grp.Controls.Add(tbl);

            // Wire change events
            _txtName.TextChanged      += (_, _) => RaiseColumnChanged();
            _txtAlias.TextChanged     += (_, _) => RaiseColumnChanged();
            _nudOrder.ValueChanged    += (_, _) => RaiseColumnChanged();
            _chkEnabled.CheckedChanged+= (_, _) => RaiseColumnChanged();
            _txtSeparator.TextChanged += (_, _) => RaiseColumnChanged();

            page.Controls.Add(TypeSpecificPanel, 0, 0);
            page.Controls.Add(grp, 0, 1);
            return page;
        }

        // ── Tab 2: Null Handling ─────────────────────────────────────────

        private TableLayoutPanel BuildNullHandlingTab()
        {
            var tbl  = MakeTableLayout();
            tbl.BackColor = Color.FromArgb(248, 250, 252);
            tbl.AutoSize = true;
            tbl.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tbl.Margin = new Padding(0);

            _cmbNullStrategy  = MakeEnumCombo<NullHandlingStrategy>();
            _txtNullDefault   = new TextBox { Dock = DockStyle.Fill };
            _txtNullException = new TextBox { Dock = DockStyle.Fill };

            _lblNullDefault   = new Label { Text = "Default Value:", AutoSize = true };
            _lblNullException = new Label { Text = "Exception Msg:",  AutoSize = true };

            AddRow(tbl, "Strategy:", _cmbNullStrategy);
            AddRow(tbl, _lblNullDefault,   _txtNullDefault);
            AddRow(tbl, _lblNullException, _txtNullException);

            _cmbNullStrategy.SelectedIndexChanged += (_, _) =>
            {
                UpdateNullHandlingVisibility((NullHandlingStrategy)_cmbNullStrategy.SelectedItem!);
                RaiseColumnChanged();
            };
            _txtNullDefault.TextChanged   += (_, _) => RaiseColumnChanged();
            _txtNullException.TextChanged += (_, _) => RaiseColumnChanged();

            return tbl;
        }

        private void UpdateNullHandlingVisibility(NullHandlingStrategy strategy)
        {
            _lblNullDefault.Visible   = strategy == NullHandlingStrategy.UseDefault;
            _txtNullDefault.Visible   = strategy == NullHandlingStrategy.UseDefault;
            _lblNullException.Visible = strategy == NullHandlingStrategy.ThrowException;
            _txtNullException.Visible = strategy == NullHandlingStrategy.ThrowException;
        }

        // ── Tab 3: Formatting ────────────────────────────────────────────

        private TableLayoutPanel BuildFormattingTab()
        {
            var tbl  = MakeTableLayout();
            tbl.BackColor = Color.FromArgb(248, 250, 252);
            tbl.AutoSize = true;
            tbl.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tbl.Margin = new Padding(0);

            _txtFormatString = new TextBox { Dock = DockStyle.Fill };
            _cmbCasing       = MakeEnumCombo<CasingTransform>();
            _cmbTrim         = MakeEnumCombo<TrimBehavior>();
            _nudMaxLength    = new NumericUpDown { Minimum = 0, Maximum = 32767, Dock = DockStyle.Fill };
            _txtPrefix       = new TextBox { Dock = DockStyle.Fill };
            _txtSuffix       = new TextBox { Dock = DockStyle.Fill };
            _nudPaddedWidth  = new NumericUpDown { Minimum = 0, Maximum = 1000, Dock = DockStyle.Fill };
            _txtPaddingChar  = new TextBox { Dock = DockStyle.Fill, MaxLength = 1, Text = " " };
            _cmbPaddingAlign = MakeEnumCombo<PaddingAlignment>();

            AddRow(tbl, "Format String:", _txtFormatString);
            AddRow(tbl, "Casing:", _cmbCasing);
            AddRow(tbl, "Trim:", _cmbTrim);
            AddRow(tbl, "Max Length:", _nudMaxLength);
            AddRow(tbl, "Prefix:", _txtPrefix);
            AddRow(tbl, "Suffix:", _txtSuffix);
            AddRow(tbl, "Pad Width:", _nudPaddedWidth);
            AddRow(tbl, "Pad Char:", _txtPaddingChar);
            AddRow(tbl, "Pad Align:", _cmbPaddingAlign);

            void Changed(object? s, EventArgs e) => RaiseColumnChanged();
            _txtFormatString.TextChanged    += Changed;
            _cmbCasing.SelectedIndexChanged += Changed;
            _cmbTrim.SelectedIndexChanged   += Changed;
            _nudMaxLength.ValueChanged      += Changed;
            _txtPrefix.TextChanged          += Changed;
            _txtSuffix.TextChanged          += Changed;
            _nudPaddedWidth.ValueChanged    += Changed;
            _txtPaddingChar.TextChanged     += Changed;
            _cmbPaddingAlign.SelectedIndexChanged += Changed;

            return tbl;
        }

        // ── Tab 4: Replacement Rules ─────────────────────────────────────

        private Panel BuildReplacementsTab()
        {
            var page    = new Panel { BackColor = Color.FromArgb(248, 250, 252), Height = 200, Margin = new Padding(0) };
            var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
            var btnAdd  = new ToolStripButton("+ Add Rule");
            var btnDel  = new ToolStripButton("✕ Remove");
            toolbar.Items.AddRange(new ToolStripItem[] { btnAdd, btnDel });

            _dgvRules = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible     = false,
                BackgroundColor       = Color.White,
                BorderStyle           = BorderStyle.None
            };
            _dgvRules.Columns.AddRange(
                new DataGridViewTextBoxColumn  { HeaderText = "Source",    Name = "src",  FillWeight = 40 },
                new DataGridViewTextBoxColumn  { HeaderText = "Replace",   Name = "tgt",  FillWeight = 40 },
                new DataGridViewCheckBoxColumn { HeaderText = "Regex",     Name = "rgx",  Width = 50, AutoSizeMode = DataGridViewAutoSizeColumnMode.None },
                new DataGridViewCheckBoxColumn { HeaderText = "MatchCase", Name = "mcs",  Width = 75, AutoSizeMode = DataGridViewAutoSizeColumnMode.None }
            );
            _dgvRules.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            btnAdd.Click += (_, _) =>
            {
                _dgvRules.Rows.Add("", "", false, true);
                RaiseColumnChanged();
            };
            btnDel.Click += (_, _) =>
            {
                foreach (DataGridViewRow row in _dgvRules.SelectedRows)
                    if (!row.IsNewRow) { _dgvRules.Rows.Remove(row); }
                RaiseColumnChanged();
            };
            _dgvRules.CellValueChanged += (_, _) => RaiseColumnChanged();
            _dgvRules.CurrentCellDirtyStateChanged += (_, _) =>
            {
                if (_dgvRules.IsCurrentCellDirty)
                    _dgvRules.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            page.Controls.Add(_dgvRules);
            page.Controls.Add(toolbar);
            return page;
        }

        // -----------------------------------------------------------------
        //  Layout helpers
        // -----------------------------------------------------------------

        protected Panel CreateHeader(string title, string subtitle, string icon, Color backColor, Color foreColor)
        {
            var pnl = new Panel { Margin = new Padding(0, 0, 0, 15), Padding = new Padding(15, 10, 15, 15), BackColor = backColor, Dock = DockStyle.Top, Height = 90 };
            
            var lblTitle = new Label { Text = $"{icon}  {title}", Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = foreColor, AutoSize = true, Location = new Point(15, 14) };
            var lblSub = new Label { Text = subtitle, Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(Math.Min(255, foreColor.R + 40), Math.Min(255, foreColor.G + 40), Math.Min(255, foreColor.B + 40)), AutoSize = true, Location = new Point(39, 44), MaximumSize = new Size(500, 0) };
            
            var btnHelp = new Button { Text = "❓ Help & Syntax", Font = new Font("Segoe UI", 9f, FontStyle.Bold), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 255, 255, 255), ForeColor = foreColor, Anchor = AnchorStyles.Top | AnchorStyles.Right, Size = new Size(130, 28), Cursor = Cursors.Hand };
            btnHelp.FlatAppearance.BorderSize = 0;
            btnHelp.Location = new Point(pnl.Width - 150, 14); 
            
            btnHelp.Click += (_, _) => ShowHelpGuide(title);

            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(lblSub);
            pnl.Controls.Add(btnHelp);

            pnl.Resize += (s, e) =>
            {
                lblSub.MaximumSize = new Size(pnl.Width - 40, 0);
                btnHelp.Left = pnl.Width - 150;
            };
            return pnl;
        }

        private void ShowHelpGuide(string columnType)
        {
            string content = columnType switch
            {
                "Property Column" => 
                    "Extracts a value directly from the source object.\n\n" +
                    "• Property Path: Use dot notation for nested properties.\n" +
                    "  Example: 'Customer.Address.City'\n\n" +
                    "• Auto-Formatting: Dates, numbers, and enums can be formatted via the Formatting tab.",
                    
                "Formula Column" => 
                    "Evaluates a mathematical or logical NCalc expression.\n\n" +
                    "• Cross-Column References: Use ${ColumnName} to inject the output of another column.\n" +
                    "  Example: ${Price} * ${Quantity}\n\n" +
                    "• Built-in Functions: if(), in(), round(), padLeft(), etc.\n" +
                    "• Ensure all referenced columns appear BEFORE this column in order.",
                    
                "Conditional Column" => 
                    "Outputs one of two strings based on a boolean NCalc expression.\n\n" +
                    "• Condition Expression: Must evaluate to True or False. (e.g. ${Age} >= 18)\n" +
                    "• True/False Values: Can be precise literal strings (e.g. 'Adult') or dynamic properties.\n" +
                    "• Dynamic Resolution: To map a True/False value to a dynamic object property, prefix it with '='.\n" +
                    "  Example: '= SecondaryName'\n\n" +
                    "• Cross-References: You can use ${Column} syntax within branches as well.",
                    
                "Custom Function Column" => 
                    "Executes C# code registered in the ICustomFunctionRegistry.\n\n" +
                    "• Parameters: Map strings or references dynamically to the backend registry function.\n" +
                    "• Dynamic Parameters: Prefix with '=' to read from the object rather than passing literal text.\n" +
                    "  Example: '= TotalAmount'\n" +
                    "• Cross-References: Passed via ${ColumnName}.",
                    
                _ => "Standard layout configuration options for ColumnWeaver.NET."
            };

            using var frm = new Form
            {
                Text = $"{columnType} — Guide",
                Size = new Size(500, 350),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.White,
                ShowIcon = false,
                ShowInTaskbar = false
            };
            
            var txt = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Text = content,
                Font = new Font("Segoe UI", 10f),
                Padding = new Padding(15)
            };
            frm.Controls.Add(txt);
            
            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.FromArgb(248,250,252) };
            var btnOk = new Button { Text = "Close", Anchor = AnchorStyles.Right, Location = new Point(390, 10), Width = 80 };
            btnOk.Click += (_, _) => frm.Close();
            pnlBottom.Controls.Add(btnOk);
            frm.Controls.Add(pnlBottom);
            
            frm.ShowDialog(this);
        }

        private static TableLayoutPanel MakeTableLayout()
        {
            var tbl = new TableLayoutPanel
            {
                ColumnCount  = 2,
                AutoSize     = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding      = new Padding(8)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110f));
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            return tbl;
        }

        private static void AddRow(TableLayoutPanel tbl, string labelText, Control control)
        {
            var lbl = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(2, 5, 2, 2) };
            control.Margin = new Padding(2);
            tbl.Controls.Add(lbl);
            tbl.Controls.Add(control);
        }

        private static void AddRow(TableLayoutPanel tbl, Label label, Control control)
        {
            label.AutoSize = true;
            label.Anchor   = AnchorStyles.Left | AnchorStyles.Top;
            label.Margin   = new Padding(2, 5, 2, 2);
            control.Margin = new Padding(2);
            tbl.Controls.Add(label);
            tbl.Controls.Add(control);
        }

        private static ComboBox MakeEnumCombo<T>() where T : struct, Enum
        {
            var cmb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock          = DockStyle.Fill
            };
            foreach (var item in Enum.GetValues(typeof(T)))
            {
                cmb.Items.Add(item);
            }
            return cmb;
        }

        private static string? NullIfEmpty(string s)
            => string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
