using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ColumnWeaver.NET.UI.Services;

namespace ColumnWeaver.NET.UI.Controls
{
    /// <summary>
    /// Displays and edits the <see cref="Core.ParserConfiguration.ParserConfiguration.Variables"/>
    /// dictionary as a grid.  Includes:
    /// <list type="bullet">
    ///   <item>Add / Remove toolbar buttons for explicit row management.</item>
    ///   <item>An "In Engine" read-only column that lights up ✓ for every
    ///         valid, non-duplicate row, giving instant visual feedback that
    ///         the variable will be passed to the ColumnWeaver engine.</item>
    /// </list>
    /// </summary>
    public sealed class VariablesConfigControl : UserControl
    {
        private readonly UIParserService _service;
        private readonly DataGridView    _grid;
        private bool _isUpdating;

        // Column name constants — avoids magic strings throughout
        private const string ColName   = "ColName";
        private const string ColType   = "ColType";
        private const string ColValue  = "ColValue";
        private const string ColEngine = "ColEngine";  // "In Engine" indicator

        public VariablesConfigControl(UIParserService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));

            Dock = DockStyle.Fill;

            // ── Grid ────────────────────────────────────────────────────────
            _grid = new DataGridView
            {
                Dock                    = DockStyle.Fill,
                AllowUserToAddRows      = false,   // managed by Add button
                AllowUserToDeleteRows   = false,   // managed by Remove button
                AutoSizeColumnsMode     = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor         = Color.White,
                BorderStyle             = BorderStyle.None,
                RowHeadersVisible       = false,
                SelectionMode           = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect             = false,
                CellBorderStyle         = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor               = Color.FromArgb(220, 220, 220)
            };

            // ── Columns ──────────────────────────────────────────────────────
            var colName = new DataGridViewTextBoxColumn
            {
                HeaderText       = "Name",
                Name             = ColName,
                DataPropertyName = "Name",
                FillWeight       = 30
            };

            var colType = new DataGridViewComboBoxColumn
            {
                HeaderText       = "Type",
                Name             = ColType,
                DataPropertyName = "Type",
                DisplayStyle     = DataGridViewComboBoxDisplayStyle.ComboBox,
                FillWeight       = 20
            };
            colType.Items.AddRange("String", "Number", "Boolean");

            var colValue = new DataGridViewTextBoxColumn
            {
                HeaderText       = "Value",
                Name             = ColValue,
                DataPropertyName = "Value",
                FillWeight       = 35
            };

            // Read-only checkbox column: TRUE when the row will be passed to engine
            var colEngine = new DataGridViewCheckBoxColumn
            {
                HeaderText     = "In Engine",
                Name           = ColEngine,
                ReadOnly       = true,
                Width          = 72,
                AutoSizeMode   = DataGridViewAutoSizeColumnMode.None,
                FillWeight     = 15,
                ToolTipText    = "Checked when this variable will be passed to the ColumnWeaver engine",
                FalseValue     = false,
                TrueValue      = true,
                ThreeState     = false
            };

            _grid.Columns.Add(colName);
            _grid.Columns.Add(colType);
            _grid.Columns.Add(colValue);
            _grid.Columns.Add(colEngine);

            // Style the "In Engine" header
            _grid.ColumnHeadersDefaultCellStyle.Font =
                new Font("Segoe UI", 8.5f, FontStyle.Regular);

            // ── Toolbar ──────────────────────────────────────────────────────
            var toolbar = BuildToolbar();

            // ── Layout ───────────────────────────────────────────────────────
            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                RowCount    = 2,
                ColumnCount = 1,
                Padding     = new Padding(0)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(_grid,   0, 1);

            Controls.Add(layout);

            // ── Events ───────────────────────────────────────────────────────
            _grid.CellValueChanged += Grid_CellValueChanged;
            _grid.CurrentCellDirtyStateChanged += Grid_CurrentCellDirtyStateChanged;
            _grid.SelectionChanged += (s, e) => UpdateRemoveButtonState();

            _service.ConfigurationChanged += (s, e) => TryRefreshGrid();
        }

        // -----------------------------------------------------------------
        //  Toolbar
        // -----------------------------------------------------------------

        private Button?  _btnRemove;

        private Panel BuildToolbar()
        {
            var panel = new Panel
            {
                Dock      = DockStyle.Fill,
                Height    = 34,
                BackColor = Color.FromArgb(245, 246, 248),
                Padding   = new Padding(4, 3, 4, 3)
            };

            var flow = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                Padding       = new Padding(0)
            };

            var btnAdd = new Button
            {
                Text      = "+ Add Variable",
                FlatStyle = FlatStyle.Flat,
                Height    = 26,
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8.5f),
                BackColor = Color.FromArgb(41, 121, 230),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
                Margin    = new Padding(0, 0, 6, 0)
            };
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Click += (_, _) => AddRow();

            _btnRemove = new Button
            {
                Text      = "✕ Remove Selected",
                FlatStyle = FlatStyle.Flat,
                Height    = 26,
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8.5f),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
                Enabled   = false,
                Margin    = new Padding(0, 0, 6, 0)
            };
            _btnRemove.FlatAppearance.BorderSize = 0;
            _btnRemove.Click += (_, _) => RemoveSelected();

            // Info label on the right
            var lblInfo = new Label
            {
                Text      = "Variables are passed to the ColumnWeaver engine at parse time.",
                AutoSize  = true,
                ForeColor = Color.FromArgb(120, 120, 120),
                Font      = new Font("Segoe UI", 8f, FontStyle.Italic),
                Margin    = new Padding(12, 5, 0, 0)
            };

            flow.Controls.Add(btnAdd);
            flow.Controls.Add(_btnRemove);
            flow.Controls.Add(lblInfo);
            panel.Controls.Add(flow);
            return panel;
        }

        private void UpdateRemoveButtonState()
        {
            if (_btnRemove == null) return;
            _btnRemove.Enabled =
                _grid.SelectedRows.Count > 0 &&
                !_grid.SelectedRows[0].IsNewRow;
        }

        // -----------------------------------------------------------------
        //  Row add / remove
        // -----------------------------------------------------------------

        private void AddRow()
        {
            _isUpdating = true;
            try
            {
                var rowIdx = _grid.Rows.Add("NewVar", "String", "", false);
                _grid.Rows[rowIdx].Cells[ColName].Selected = true;
                _grid.CurrentCell = _grid.Rows[rowIdx].Cells[ColName];
                _grid.BeginEdit(true);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void RemoveSelected()
        {
            if (_grid.SelectedRows.Count == 0) return;
            var row = _grid.SelectedRows[0];
            if (row.IsNewRow) return;

            _grid.Rows.Remove(row);
            SaveVariables();
        }

        // -----------------------------------------------------------------
        //  Grid refresh
        // -----------------------------------------------------------------

        private void TryRefreshGrid()
        {
            if (_isUpdating) return;
            RefreshGrid();
        }

        public void RefreshGrid()
        {
            _isUpdating = true;
            try
            {
                _grid.Rows.Clear();
                var variables = _service.CurrentConfiguration.Variables;
                if (variables != null)
                {
                    foreach (var kvp in variables)
                    {
                        string typeStr = "String";
                        string valStr  = kvp.Value?.ToString() ?? "";

                        if (kvp.Value is bool)
                            typeStr = "Boolean";
                        else if (kvp.Value is int    || kvp.Value is long  ||
                                 kvp.Value is float  || kvp.Value is double ||
                                 kvp.Value is decimal)
                            typeStr = "Number";

                        // "In Engine" = true for all persisted variables (they passed validation)
                        _grid.Rows.Add(kvp.Key, typeStr, valStr, true);
                    }
                }
            }
            finally
            {
                _isUpdating = false;
            }
            UpdateRemoveButtonState();
        }

        // -----------------------------------------------------------------
        //  Save to service
        // -----------------------------------------------------------------

        private void SaveVariables()
        {
            if (_isUpdating) return;

            // First pass: determine which rows are valid so we can set InEngine
            var newVars   = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool hasErrors = false;

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                row.ErrorText = string.Empty;

                var name = row.Cells[ColName].Value?.ToString()?.Trim();

                // Validate name
                bool nameOk = !string.IsNullOrEmpty(name) &&
                              System.Text.RegularExpressions.Regex.IsMatch(
                                  name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");

                if (string.IsNullOrEmpty(name))
                {
                    row.ErrorText = "Name cannot be empty.";
                    row.Cells[ColEngine].Value = false;
                    hasErrors = true;
                    continue;
                }

                if (!nameOk)
                {
                    row.ErrorText = "Name must start with a letter/underscore and contain only letters, digits, underscores.";
                    row.Cells[ColEngine].Value = false;
                    hasErrors = true;
                    continue;
                }

                if (seenNames.Contains(name!))
                {
                    row.ErrorText = "Duplicate variable name.";
                    row.Cells[ColEngine].Value = false;
                    hasErrors = true;
                    continue;
                }

                var typeStr = row.Cells[ColType].Value?.ToString()  ?? "String";
                var valStr  = row.Cells[ColValue].Value?.ToString() ?? "";
                object? realValue = valStr;

                if (typeStr == "Boolean")
                {
                    if (bool.TryParse(valStr, out bool b))
                    {
                        realValue = b;
                    }
                    else
                    {
                        row.ErrorText = "Value must be true or false.";
                        row.Cells[ColEngine].Value = false;
                        hasErrors = true;
                        continue;
                    }
                }
                else if (typeStr == "Number")
                {
                    if (double.TryParse(valStr, out double d))
                    {
                        realValue = d;
                    }
                    else
                    {
                        row.ErrorText = "Value must be a valid number.";
                        row.Cells[ColEngine].Value = false;
                        hasErrors = true;
                        continue;
                    }
                }

                // Row is valid — mark In Engine = true
                row.Cells[ColEngine].Value = true;
                seenNames.Add(name!);
                newVars[name!] = realValue;
            }

            if (hasErrors)
            {
                // Don't push invalid state to the engine, but update In Engine indicators
                return;
            }

            _isUpdating = true;
            try
            {
                _service.UpdateVariables(newVars);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        // -----------------------------------------------------------------
        //  Grid events
        // -----------------------------------------------------------------

        private void Grid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            // Commit combo-box changes immediately so CellValueChanged fires
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewComboBoxCell)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            // Don't react to changes in the read-only InEngine column itself
            if (e.RowIndex >= 0 && e.ColumnIndex != _grid.Columns[ColEngine].Index)
                SaveVariables();
        }
    }
}
