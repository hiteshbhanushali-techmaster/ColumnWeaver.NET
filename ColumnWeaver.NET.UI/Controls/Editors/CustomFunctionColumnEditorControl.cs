using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Models;
using ColumnWeaver.NET.UI.Services;

namespace ColumnWeaver.NET.UI.Controls.Editors
{
    /// <summary>
    /// Editor for <see cref="CustomFunctionColumn"/> — lets the user pick a
    /// registered function from a dropdown (V2), view the expected parameter
    /// count from <see cref="FunctionMetadata"/>, and manage the ordered
    /// parameter list.
    /// </summary>
    /// <remarks>
    /// Each parameter entry may contain a column cross-reference (<c>${ColumnName}</c>)
    /// which the pipeline expands before invoking the registry function.
    /// </remarks>
    [System.ComponentModel.DesignerCategory("Code")]
    public sealed class CustomFunctionColumnEditorControl : BaseColumnEditorControl
    {
        private readonly UIParserService _service;

        private ComboBox    _cmbFunctionName  = null!;
        private Label       _lblParamHint     = null!;
        private DataGridView _dgvParams       = null!;

        public CustomFunctionColumnEditorControl(UIParserService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));

            // ── Header ────────────────────────────────────────────────────
            var hdr = CreateHeader("Custom Function Column",
                "Invokes a named function from ICustomFunctionRegistry.", "⚙️", Color.FromArgb(250, 235, 235), Color.FromArgb(150, 40, 40));

            // ── Function name dropdown (V2) ───────────────────────────────
            var nameLbl = new Label
            {
                Text   = "Function Name:",
                AutoSize = true,
                Margin = new Padding(4, 6, 4, 2)
            };

            _cmbFunctionName = new ComboBox
            {
                Width         = 250,
                DropDownStyle = ComboBoxStyle.DropDown,
                Font          = new Font("Consolas", 9.5f),
                Margin        = new Padding(4, 2, 4, 4)
            };

            // Populate from registry (empty list is fine — user can still type freely)
            PopulateFunctionDropdown();

            // ── Parameter count hint (V2) ─────────────────────────────────
            _lblParamHint = new Label
            {
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8f, FontStyle.Italic),
                ForeColor = Color.FromArgb(60, 100, 140),
                Text      = string.Empty,
                Margin    = new Padding(4, 0, 4, 6)
            };

            // ── Parameter grid ────────────────────────────────────────────
            var paramsLbl   = new Label { Text = "Parameters:", AutoSize = true, Margin = new Padding(4, 4, 4, 2) };
            var paramPanel  = new Panel { Height = 28, Margin = new Padding(0) };
            var btnAddParam = new ColumnWeaver.NET.UI.Controls.Modern.ModernButton { Text = "+ Add", Width = 70, Height = 24, Location = new System.Drawing.Point(0, 2), BackColor = Color.FromArgb(41, 121, 230) };
            var btnDelParam = new ColumnWeaver.NET.UI.Controls.Modern.ModernButton { Text = "Remove", Width = 70, Height = 24, Location = new System.Drawing.Point(74, 2), BackColor = Color.FromArgb(170, 180, 190) };
            paramPanel.Controls.AddRange(new System.Windows.Forms.Control[] { btnAddParam, btnDelParam });

            _dgvParams = new DataGridView
            {
                Width                 = 400,
                Height                = 120,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible     = false,
                BackgroundColor       = Color.White,
                Margin                = new Padding(4, 2, 4, 4)
            };
            _dgvParams.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText   = "Parameter Value (literal or ${ColumnRef})",
                Name         = "param",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            TypeSpecificPanel.Controls.AddRange(new System.Windows.Forms.Control[]
                { hdr, nameLbl, _cmbFunctionName, _lblParamHint, paramsLbl, paramPanel, _dgvParams });

            // ── Events ────────────────────────────────────────────────────
            _cmbFunctionName.SelectedIndexChanged += OnFunctionSelected;
            _cmbFunctionName.TextChanged          += OnFunctionSelected;
            _dgvParams.CellValueChanged           += (_, _) => RaiseColumnChanged();

            btnAddParam.Click += (_, _) =>
            {
                _dgvParams.Rows.Add(string.Empty);
                RaiseColumnChanged();
            };
            btnDelParam.Click += (_, _) =>
            {
                foreach (DataGridViewRow row in _dgvParams.SelectedRows)
                    if (!row.IsNewRow) _dgvParams.Rows.Remove(row);
                RaiseColumnChanged();
            };
        }

        // -----------------------------------------------------------------
        //  IColumnEditorControl — Load / Save
        // -----------------------------------------------------------------

        protected override void LoadTypeSpecific(ColumnDefinition column)
        {
            if (column is not CustomFunctionColumn cfc) return;

            _cmbFunctionName.Text = cfc.FunctionName;
            UpdateParamHint(cfc.FunctionName);

            _dgvParams.Rows.Clear();
            foreach (var param in cfc.Parameters)
                _dgvParams.Rows.Add(param);
        }

        protected override ColumnDefinition CreateTypedColumn()
        {
            var parameters = new List<string>();
            foreach (DataGridViewRow row in _dgvParams.Rows)
            {
                if (row.IsNewRow) continue;
                parameters.Add((string?)row.Cells[0].Value ?? string.Empty);
            }

            return new CustomFunctionColumn
            {
                FunctionName = _cmbFunctionName.Text.Trim(),
                Parameters   = parameters
            };
        }

        // -----------------------------------------------------------------
        //  Event handlers
        // -----------------------------------------------------------------

        private void OnFunctionSelected(object? sender, EventArgs e)
        {
            var name = _cmbFunctionName.Text.Trim();
            UpdateParamHint(name);
            RaiseColumnChanged();
        }

        // -----------------------------------------------------------------
        //  V2: Populate dropdown + metadata hint
        // -----------------------------------------------------------------

        private void PopulateFunctionDropdown()
        {
            _cmbFunctionName.Items.Clear();
            var names = _service.GetRegisteredFunctions();
            foreach (var n in names)
                _cmbFunctionName.Items.Add(n);
        }

        /// <summary>
        /// Updates the parameter-count hint label from <see cref="FunctionMetadata"/>.
        /// Shows expected parameter count and types when metadata is available.
        /// </summary>
        private void UpdateParamHint(string functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName))
            {
                _lblParamHint.Text = string.Empty;
                return;
            }

            var meta = _service.GetFunctionMetadata(functionName);
            if (meta == null)
            {
                _lblParamHint.Text      = "ℹ Function not found in registry.";
                _lblParamHint.ForeColor = Color.OrangeRed;
                return;
            }

            if (meta.ParameterTypes.Count == 0)
            {
                _lblParamHint.Text      = "✔ No parameters required.";
                _lblParamHint.ForeColor = Color.SeaGreen;
            }
            else
            {
                var typeNames = new List<string>(meta.ParameterTypes.Count);
                foreach (var t in meta.ParameterTypes)
                    typeNames.Add(t.Name);

                _lblParamHint.Text =
                    $"ℹ Expected {meta.ParameterTypes.Count} parameter(s): " +
                    string.Join(", ", typeNames);
                _lblParamHint.ForeColor = Color.FromArgb(30, 100, 180);
            }

            if (!string.IsNullOrWhiteSpace(meta.Description))
                _lblParamHint.Text += $" — {meta.Description}";
        }
    }
}
