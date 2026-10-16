using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.UI.Controls;
using ColumnWeaver.NET.UI.Controls.Editors;
using ColumnWeaver.NET.UI.Demo;
using ColumnWeaver.NET.UI.Services;

namespace ColumnWeaver.NET.UI.Forms
{
    /// <summary>
    /// Application shell. Hosts the column grid, the dynamic column editor,
    /// and the live preview / test-input panel.
    /// </summary>
    /// <remarks>
    /// Layout:
    /// <code>
    /// ┌─ MenuStrip ─────────────────────────────────────────────────────┐
    /// ├─ ToolStrip ─────────────────────────────────────────────────────┤
    /// ├──────────────┬──────────────────────────────────────────────────┤
    /// │ Column Grid  │ ┌─ Column Editor (dynamic) ─────────────────────┐│
    /// │ Control      │ │  (swapped per selected column type)           ││
    /// │              │ ├──────────────────────────────────────────────┤│
    /// │              │ │ ┌─ Preview ──┐  ┌─ Test ────────────────────┐││
    /// │              │ │ │            │  │                           │││
    /// │              │ │ └────────────┘  └───────────────────────────┘││
    /// └──────────────┴─┴──────────────────────────────────────────────┴┘
    /// ├─ StatusStrip ───────────────────────────────────────────────────┤
    /// </code>
    /// </remarks>
    [System.ComponentModel.DesignerCategory("Code")]
    public sealed class MainForm : Form
    {
        // -----------------------------------------------------------------
        //  Dependencies
        // -----------------------------------------------------------------

        private readonly UIParserService _service;

        // -----------------------------------------------------------------
        //  Controls
        // -----------------------------------------------------------------


        private SplitContainer     _mainSplit       = null!;
        private SplitContainer     _rightSplit      = null!;
        private SplitContainer     _bottomSplit     = null!;
        private ColumnGridControl  _columnGrid      = null!;
        private Panel              _editorPanel     = null!;
        private LivePreviewControl _livePreview     = null!;
        private TestInputControl   _testInput       = null!;
        private Label              _editorPlaceholder = null!;

        private BaseColumnEditorControl? _activeEditor;
        
        // Debouncer to prevent rapid UI blinking during typing
        private readonly System.Windows.Forms.Timer _debounceTimer;

        private StatusStrip _statusStrip = null!;
        private ToolStripStatusLabel _lblStatusCount = null!;
        private ToolStripStatusLabel _lblStatusSep = null!;
        private ToolStripStatusLabel _lblStatusState = null!;
        private ToolStripStatusLabel _lblStatusFile = null!;

        // Config name textbox — promoted to field so RefreshAll() can sync it
        private TextBox _txtConfig = null!;
        private bool _isUpdatingConfigName;

        // -----------------------------------------------------------------
        //  Constructor
        // -----------------------------------------------------------------

        /// <summary>Initializes the form with the provided <see cref="UIParserService"/>.</summary>
        public MainForm(UIParserService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _debounceTimer = new System.Windows.Forms.Timer { Interval = 300 };
            KeyPreview = true;

            BuildUI();
            WireEvents();
            RefreshAll();
        }

        // -----------------------------------------------------------------
        //  UI Construction
        // -----------------------------------------------------------------

        private void BuildUI()
        {
            SuspendLayout();

            Text            = "ColumnWeaver.NET — Configuration Editor";
            Size            = new Size(1280, 820);
            MinimumSize     = new Size(900, 600);
            StartPosition   = FormStartPosition.CenterScreen;
            Font            = new Font("Segoe UI", 9f, FontStyle.Regular);
            BackColor       = Color.FromArgb(240, 242, 245);

            BuildLayout();
            BuildTopBar();
            BuildMenus();
            BuildStatusStrip();

            ResumeLayout(false);
            PerformLayout();
        }

        private void BuildStatusStrip()
        {
            _statusStrip = new StatusStrip { BackColor = Color.FromArgb(240, 242, 245) };
            
            _lblStatusState = new ToolStripStatusLabel { Text = "🟢 Saved", ForeColor = Color.Green, Margin = new Padding(10, 0, 10, 0) };
            _lblStatusCount = new ToolStripStatusLabel { Text = "0 Columns", Margin = new Padding(0, 0, 10, 0) };
            _lblStatusSep = new ToolStripStatusLabel { Text = "Separator: |", Margin = new Padding(0, 0, 10, 0) };
            
            var spacer = new ToolStripStatusLabel { Spring = true };
            _lblStatusFile = new ToolStripStatusLabel { Text = "Unsaved File", ForeColor = Color.Gray };

            _statusStrip.Items.Add(_lblStatusState);
            _statusStrip.Items.Add(_lblStatusCount);
            _statusStrip.Items.Add(_lblStatusSep);
            _statusStrip.Items.Add(spacer);
            _statusStrip.Items.Add(_lblStatusFile);

            Controls.Add(_statusStrip);
        }

        private void BuildMenus()
        {
            var menu = new MenuStrip { BackColor = Color.FromArgb(240, 242, 245), Padding = new Padding(2) };
            
            var mnuFile = new ToolStripMenuItem("&File");
            mnuFile.DropDownItems.Add("New", null, OnMenuNew);
            mnuFile.DropDownItems.Add("Open...", null, OnMenuOpenJson);
            mnuFile.DropDownItems.Add("-");
            mnuFile.DropDownItems.Add("Save", null, OnMenuSave);
            mnuFile.DropDownItems.Add("Save As JSON...", null, OnMenuSaveJson);
            mnuFile.DropDownItems.Add("-");
            mnuFile.DropDownItems.Add("Exit", null, (_, _) => Close());

            var mnuEdit = new ToolStripMenuItem("&Edit");
            mnuEdit.DropDownItems.Add("Undo\t Ctrl+Z", null, (_, _) => HandleUndo());
            mnuEdit.DropDownItems.Add("Redo\t Ctrl+Y", null, (_, _) => HandleRedo());
            mnuEdit.DropDownItems.Add("-");
            mnuEdit.DropDownItems.Add("Copy\t Ctrl+C", null, (_, _) => HandleCopy());
            mnuEdit.DropDownItems.Add("Cut\t Ctrl+X", null, (_, _) => HandleCut());
            mnuEdit.DropDownItems.Add("Paste\t Ctrl+V", null, (_, _) => HandlePaste());

            // ── Demo menu — quick-load preset configurations ─────────────────
            var mnuDemo = new ToolStripMenuItem("&Demo");
            mnuDemo.DropDownItems.Add(
                "Order Summary (basic 7-column)",
                null,
                (_, _) => LoadDemoConfig(basic: true));
            mnuDemo.DropDownItems.Add(
                "Order Summary with Variable Store",
                null,
                (_, _) => LoadDemoConfig(basic: false));

            menu.Items.Add(mnuFile);
            menu.Items.Add(mnuEdit);
            menu.Items.Add(mnuDemo);

            Controls.Add(menu);
            MainMenuStrip = menu;
            menu.Dock = DockStyle.Top;
        }

        private void BuildTopBar()
        {
            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 45, BackColor = Color.FromArgb(240, 242, 245) };

            var flowLeft = new FlowLayoutPanel { Dock = DockStyle.Left, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(10), Padding = new Padding(10, 5, 0, 0) };
            
            var fontIcon = new Font("Segoe MDL2 Assets", 11f);
            
            var btnNew = new Button { Text = "\uE710 New", Font = fontIcon, FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(0, 30), BackColor = Color.Transparent, ForeColor = Color.FromArgb(60,60,60), Cursor = Cursors.Hand };
            btnNew.FlatAppearance.BorderSize = 0;
            btnNew.Click += OnMenuNew;

            var btnOpen = new Button { Text = "\uE8E5 Open", Font = fontIcon, FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(0, 30), BackColor = Color.Transparent, ForeColor = Color.FromArgb(60,60,60), Cursor = Cursors.Hand };
            btnOpen.FlatAppearance.BorderSize = 0;
            btnOpen.Click += OnMenuOpenJson;

            var btnSave = new Button { Text = "\uE74E Save", Font = fontIcon, FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(0, 30), BackColor = Color.Transparent, ForeColor = Color.FromArgb(60,60,60), Cursor = Cursors.Hand };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += OnMenuSaveJson;

            var lblSep = new Label { Text = " |  Separator: ", AutoSize = true, Margin = new Padding(10, 6, 0, 0), ForeColor = Color.Gray };
            var txtSep = new TextBox { Width = 30, Margin = new Padding(0, 4, 0, 0), Text = "|" };
            txtSep.TextChanged += (_, _) => _service.SetGlobalSeparator(txtSep.Text);

            flowLeft.Controls.AddRange(new Control[] { btnNew, btnOpen, btnSave, lblSep, txtSep });

            var pnlCenter = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            var lblConfig = new Label { Text = "Config Name:", AutoSize = true, ForeColor = Color.FromArgb(41, 121, 230), Margin = new Padding(0,5,0,0) };
            // Promoted to field (_txtConfig) so RefreshAll() can sync name from service
            _txtConfig = new TextBox { Text = string.Empty, Width = 200, Margin = new Padding(0,2,0,0), BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
            _txtConfig.TextChanged += (_, _) =>
            {
                if (!_isUpdatingConfigName)
                    _service.SetName(_txtConfig.Text);
            };
            
            var centerFlow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Left, WrapContents = false };
            centerFlow.Controls.Add(lblConfig);
            centerFlow.Controls.Add(_txtConfig);
            pnlCenter.Controls.Add(centerFlow);

            var flowRight = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 5, 10, 0), WrapContents = false };
            var btnSaveAction = new ColumnWeaver.NET.UI.Controls.Modern.ModernButton { Text = "Save", Width = 80, Height = 30 };
            btnSaveAction.Click += OnMenuSaveJson;

            // Edit Actions
            var btnPaste = new Button { Text = "\uE77F Paste", Font = fontIcon, FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(0, 30), BackColor = Color.Transparent, ForeColor = Color.FromArgb(60,60,60), Cursor = Cursors.Hand, Margin = new Padding(15,3,3,3) };
            btnPaste.FlatAppearance.BorderSize = 0; btnPaste.Click += (_, _) => HandlePaste();

            var btnCopy = new Button { Text = "\uE8C8 Copy", Font = fontIcon, FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(0, 30), BackColor = Color.Transparent, ForeColor = Color.FromArgb(60,60,60), Cursor = Cursors.Hand };
            btnCopy.FlatAppearance.BorderSize = 0; btnCopy.Click += (_, _) => HandleCopy();
            
            var btnRedo = new Button { Text = "\uE7A6 Redo", Font = fontIcon, FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(0, 30), BackColor = Color.Transparent, ForeColor = Color.FromArgb(60,60,60), Cursor = Cursors.Hand, Margin = new Padding(15,3,3,3) };
            btnRedo.FlatAppearance.BorderSize = 0; btnRedo.Click += (_, _) => HandleRedo();

            var btnUndo = new Button { Text = "\uE7A7 Undo", Font = fontIcon, FlatStyle = FlatStyle.Flat, AutoSize = true, MinimumSize = new Size(0, 30), BackColor = Color.Transparent, ForeColor = Color.FromArgb(60,60,60), Cursor = Cursors.Hand };
            btnUndo.FlatAppearance.BorderSize = 0; btnUndo.Click += (_, _) => HandleUndo();

            flowRight.Controls.Add(btnSaveAction);
            flowRight.Controls.Add(btnPaste);
            flowRight.Controls.Add(btnCopy);
            flowRight.Controls.Add(btnRedo);
            flowRight.Controls.Add(btnUndo);

            pnlTop.Controls.Add(flowLeft);
            pnlTop.Controls.Add(flowRight);
            pnlTop.Controls.Add(pnlCenter);
            pnlCenter.BringToFront();

            Controls.Add(pnlTop);
            
            // Ensure proper overall Dock Z-Order logic
            // To be at the absolute top, it must be sent to the back of the Controls stack
            if (MainMenuStrip != null) MainMenuStrip.SendToBack();
            pnlTop.SendToBack();
        }

        private void BuildLayout()
        {
            // ── Main split: Left = grid, Right = editor+preview ─────────────
            _mainSplit = new SplitContainer
            {
                Dock        = DockStyle.Fill,
                Orientation = Orientation.Vertical
            };

            // Left panel — tabbed list (columns / variables)
            var tabControl = new TabControl { Dock = DockStyle.Fill };
            
            var tabColumns = new TabPage("Columns");
            _columnGrid = new ColumnGridControl { Dock = DockStyle.Fill };
            tabColumns.Controls.Add(_columnGrid);

            var tabVariables = new TabPage("Variables");
            var varControl = new VariablesConfigControl(_service) { Dock = DockStyle.Fill };
            tabVariables.Controls.Add(varControl);

            tabControl.TabPages.Add(tabColumns);
            tabControl.TabPages.Add(tabVariables);

            _mainSplit.Panel1.Controls.Add(tabControl);

            // Right panel — editor + preview/test
            _rightSplit = new SplitContainer
            {
                Dock        = DockStyle.Fill,
                Orientation = Orientation.Horizontal
            };

            // Editor area (top right)
            _editorPanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.White,
                Padding   = new Padding(4)
            };
            _editorPlaceholder = new Label
            {
                Text      = "Select or add a column to begin editing.",
                ForeColor = Color.Gray,
                Font      = new Font("Segoe UI", 10f, FontStyle.Italic),
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _editorPanel.Controls.Add(_editorPlaceholder);
            _rightSplit.Panel1.Controls.Add(_editorPanel);

            // Preview + Test panel (bottom right, split horizontally)
            _bottomSplit = new SplitContainer
            {
                Dock        = DockStyle.Fill,
                Orientation = Orientation.Vertical
            };

            _livePreview = new LivePreviewControl { Dock = DockStyle.Fill };
            _testInput   = new TestInputControl(_service) { Dock = DockStyle.Fill };

            _bottomSplit.Panel1.Controls.Add(_livePreview);
            _bottomSplit.Panel2.Controls.Add(_testInput);
            _rightSplit.Panel2.Controls.Add(_bottomSplit);

            _mainSplit.Panel2.Controls.Add(_rightSplit);
            Controls.Add(_mainSplit);
        }

        // -----------------------------------------------------------------
        //  Event wiring
        // -----------------------------------------------------------------

        private void WireEvents()
        {
            _service.ConfigurationChanged += OnServiceConfigurationChanged;

            _debounceTimer.Tick += (_, _) =>
            {
                _debounceTimer.Stop();
                RefreshAll();
            };

            _columnGrid.ColumnSelected        += OnColumnSelected;
            _columnGrid.AddColumnRequested    += OnAddColumnRequested;
            _columnGrid.RemoveRequested       += OnRemoveRequested;
            _columnGrid.MoveUpRequested       += OnMoveUpRequested;
            _columnGrid.MoveDownRequested     += OnMoveDownRequested;
            _columnGrid.ReorderRequested      += OnReorderRequested;
        }

        // -----------------------------------------------------------------
        //  Service events
        // -----------------------------------------------------------------

        private void OnServiceConfigurationChanged(object? sender, EventArgs e)
        {
            // Debounce rapid input events
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void RefreshAll()
        {
            _columnGrid.RefreshGrid(_service.CurrentConfiguration.Columns);
            _livePreview.UpdatePreview(_service.TryPreviewParse()); // uses stored PreviewSource
            UpdateTitle();
            UpdateStatus();
            SyncToolbarFromConfig();
            SyncConfigName();
        }

        /// <summary>
        /// Keeps the Config Name textbox in sync with the service's current
        /// configuration name without triggering a re-entrant SetName call.
        /// </summary>
        private void SyncConfigName()
        {
            if (_txtConfig == null) return;
            var name = _service.CurrentConfiguration.Name ?? string.Empty;
            if (_txtConfig.Text == name) return;

            _isUpdatingConfigName = true;
            try   { _txtConfig.Text = name; }
            finally { _isUpdatingConfigName = false; }
        }

        private void UpdateTitle()
        {
            var name = _service.CurrentConfiguration.Name;
            var dirty = _service.IsModified ? " *" : "";
            var file  = _service.CurrentFilePath != null
                ? $" — {System.IO.Path.GetFileName(_service.CurrentFilePath)}"
                : "";
            Text = $"{name}{dirty}{file} — ColumnWeaver.NET Editor";
        }

        private void UpdateStatus()
        {
            if (_lblStatusCount == null) return;
            
            _lblStatusCount.Text = $"{_service.CurrentConfiguration.Columns.Count} Columns";
            _lblStatusSep.Text   = $"Separator: '{_service.CurrentConfiguration.GlobalSeparator}'";
            _lblStatusFile.Text  = _service.CurrentFilePath ?? "Unsaved Document";

            if (_service.IsModified)
            {
                _lblStatusState.Text = "🟠 Unsaved Changes";
                _lblStatusState.ForeColor = Color.OrangeRed;
            }
            else
            {
                _lblStatusState.Text = "🟢 Saved";
                _lblStatusState.ForeColor = Color.Green;
            }
        }

        private void SyncToolbarFromConfig()
        {
            // Toolbar managed directly through binding now
        }

        // -----------------------------------------------------------------
        //  Column grid events
        // -----------------------------------------------------------------

        private void OnColumnSelected(object? sender, ColumnDefinition? column)
        {
            SwapEditor(column);
        }

        private void OnAddColumnRequested(object? sender, string columnTypeName)
        {
            ColumnDefinition newCol = columnTypeName switch
            {
                nameof(PropertyColumn)       => new PropertyColumn       { Name = "NewProperty" },
                nameof(FormulaColumn)        => new FormulaColumn        { Name = "NewFormula" },
                nameof(ConditionalColumn)    => new ConditionalColumn    { Name = "NewCondition" },
                nameof(FixedTextColumn)      => new FixedTextColumn      { Name = "NewText" },
                nameof(CustomFunctionColumn) => new CustomFunctionColumn { Name = "NewFunction" },
                _                            => new PropertyColumn       { Name = "NewColumn" }
            };

            _columnGrid.SelectColumnNextRefresh(newCol.Id);
            _service.AddColumn(newCol);
        }

        private void OnRemoveRequested(object? sender, Guid columnId)
        {
            var col = _service.CurrentConfiguration.Columns.FirstOrDefault(c => c?.Id == columnId);
            if (col == null) return;

            var r = MessageBox.Show(
                $"Remove column '{col.Name}'?",
                "Confirm Remove",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (r == DialogResult.Yes)
            {
                _service.RemoveColumn(columnId);
                SwapEditor(null);
            }
        }

        private void OnMoveUpRequested(object? sender, Guid columnId)
            => _service.MoveColumnUp(columnId);

        private void OnMoveDownRequested(object? sender, Guid columnId)
            => _service.MoveColumnDown(columnId);

        private void OnReorderRequested(object? sender, Tuple<Guid, int> e)
            => _service.MoveColumnToIndex(e.Item1, e.Item2);


        // -----------------------------------------------------------------
        //  Dynamic editor swapping
        // -----------------------------------------------------------------

        private Guid? _activeColumnId;

        private void SwapEditor(ColumnDefinition? column)
        {
            if (column != null && column.Id == _activeColumnId)
                return; // Prevent focus reset while continuing to edit the same column
            
            _activeColumnId = column?.Id;

            // Unsubscribe old editor
            if (_activeEditor != null)
            {
                _activeEditor.ColumnChanged -= OnEditorColumnChanged;
                _editorPanel.Controls.Remove(_activeEditor);
                _activeEditor.Dispose();
                _activeEditor = null;
            }

            if (column == null)
            {
                _editorPlaceholder.Visible = true;
                return;
            }

            _editorPlaceholder.Visible = false;

            _activeEditor = column switch
            {
                PropertyColumn       => new PropertyColumnEditorControl(),
                FormulaColumn        => new FormulaColumnEditorControl(_service),
                ConditionalColumn    => new ConditionalColumnEditorControl(),
                FixedTextColumn      => new FixedTextColumnEditorControl(),
                CustomFunctionColumn => new CustomFunctionColumnEditorControl(_service),
                _                    => new PropertyColumnEditorControl()
            };

            _activeEditor.Dock = DockStyle.Fill;
            _activeEditor.LoadColumn(column);
            _activeEditor.ColumnChanged += OnEditorColumnChanged;
            _editorPanel.Controls.Add(_activeEditor);
            _activeEditor.BringToFront();
        }

        private void OnEditorColumnChanged(object? sender, EventArgs e)
        {
            if (_activeEditor == null) return;

            var updated = _activeEditor.SaveColumn();
            if (updated != null)
                _service.UpdateColumn(updated);
        }

        // -----------------------------------------------------------------
        //  Menu handlers
        // -----------------------------------------------------------------

        private void OnMenuNew(object? sender, EventArgs e)
        {
            if (_service.IsModified && !ConfirmDiscard()) return;
            _service.New();
            SwapEditor(null);
        }

        private void LoadDemoConfig(bool basic)
        {
            if (_service.IsModified && !ConfirmDiscard()) return;

            var config = basic
                ? DemoConfigurationFactory.Create()
                : DemoConfigurationFactory.CreateWithVariables();

            _service.LoadConfiguration(config);
            _service.SetPreviewSource(DemoConfigurationFactory.CreateSampleOrder());
            SwapEditor(null);
        }

        private void OnMenuOpenJson(object? sender, EventArgs e)
        {
            if (_service.IsModified && !ConfirmDiscard()) return;

            using var dlg = new OpenFileDialog
            {
                Filter = "JSON Configuration|*.json|All files|*.*",
                Title  = "Open JSON Configuration"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try   { _service.Load(dlg.FileName, isXml: false); SwapEditor(null); }
            catch (Exception ex) { ShowError("Load failed", ex.Message); }
        }

        private void OnMenuOpenXml(object? sender, EventArgs e)
        {
            if (_service.IsModified && !ConfirmDiscard()) return;

            using var dlg = new OpenFileDialog
            {
                Filter = "XML Configuration|*.xml|All files|*.*",
                Title  = "Open XML Configuration"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try   { _service.Load(dlg.FileName, isXml: true); SwapEditor(null); }
            catch (Exception ex) { ShowError("Load failed", ex.Message); }
        }

        private void OnMenuSave(object? sender, EventArgs e)
        {
            if (_service.CurrentFilePath != null)
            {
                try   { _service.Save(); }
                catch (Exception ex) { ShowError("Save failed", ex.Message); }
            }
            else
            {
                OnMenuSaveJson(sender, e);
            }
        }

        private void OnMenuSaveJson(object? sender, EventArgs e)
        {
            using var dlg = new SaveFileDialog
            {
                Filter      = "JSON Configuration|*.json|All files|*.*",
                DefaultExt  = "json",
                Title       = "Save as JSON"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try   { _service.Save(dlg.FileName, isXml: false); }
            catch (Exception ex) { ShowError("Save failed", ex.Message); }
        }

        private void OnMenuSaveXml(object? sender, EventArgs e)
        {
            using var dlg = new SaveFileDialog
            {
                Filter     = "XML Configuration|*.xml|All files|*.*",
                DefaultExt = "xml",
                Title      = "Save as XML"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try   { _service.Save(dlg.FileName, isXml: true); }
            catch (Exception ex) { ShowError("Save failed", ex.Message); }
        }

        // -----------------------------------------------------------------
        //  Shortcuts and State Operations
        // -----------------------------------------------------------------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z)) { HandleUndo(); return true; }
            if (keyData == (Keys.Control | Keys.Y)) { HandleRedo(); return true; }
            
            // Only capture copy/cut/paste specifically for the grid or form level 
            // if we are NOT inside a textbox. Otherwise we break native text editing!
            var isTextEditing = ActiveControl is TextBox || 
                               (ActiveControl != null && ActiveControl.GetType().Name.Contains("TextBox"));

            if (!isTextEditing)
            {
                if (keyData == (Keys.Control | Keys.C)) { HandleCopy(); return true; }
                if (keyData == (Keys.Control | Keys.X)) { HandleCut(); return true; }
                if (keyData == (Keys.Control | Keys.V)) { HandlePaste(); return true; }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void HandleUndo() => _service.Undo();
        private void HandleRedo() => _service.Redo();
        
        private void HandleCopy()
        {
            if (_activeColumnId.HasValue) _service.CopyColumn(_activeColumnId.Value);
        }

        private void HandleCut()
        {
            if (_activeColumnId.HasValue) _service.CutColumn(_activeColumnId.Value);
        }

        private void HandlePaste()
        {
            var pasted = _service.PasteColumn();
            if (pasted != null)
            {
                _columnGrid.SelectColumnNextRefresh(pasted.Id);
            }
        }

        // -----------------------------------------------------------------
        //  Helpers
        // -----------------------------------------------------------------

        private bool ConfirmDiscard()
            => MessageBox.Show("You have unsaved changes. Discard?", "Unsaved Changes",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

        private void ShowError(string title, string message)
            => MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // All SplitContainer sizing is deferred to OnLoad.
            // Setting Panel*MinSize or SplitterDistance while the container has zero
            // width (during constructor) causes InvalidOperationException in WinForms.
            var w = ClientSize.Width;
            var h = ClientSize.Height;

            // Main split: ~25 % left column list
            _mainSplit.Panel1MinSize   = 180;
            _mainSplit.Panel2MinSize   = 350;
            _mainSplit.SplitterDistance = Math.Max(180, (int)(w * 0.25));

            // Right split: ~58 % top editor area
            _rightSplit.Panel1MinSize   = 120;
            _rightSplit.Panel2MinSize   = 150;
            _rightSplit.SplitterDistance = Math.Max(120, (int)(h * 0.55));

            // Bottom split: 50 / 50 preview vs test
            _bottomSplit.Panel1MinSize   = 120;
            _bottomSplit.Panel2MinSize   = 120;
            _bottomSplit.SplitterDistance = Math.Max(120, (int)(_bottomSplit.Width * 0.50));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_service.IsModified && !ConfirmDiscard())
                e.Cancel = true;
            base.OnFormClosing(e);
        }
    }
}
