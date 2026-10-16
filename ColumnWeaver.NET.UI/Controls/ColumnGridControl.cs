using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.UI.Controls.Modern;

namespace ColumnWeaver.NET.UI.Controls
{
    /// <summary>
    /// Displays all configured columns in a <see cref="DataGridView"/> with a
    /// toolbar for adding, removing, and reordering columns.
    /// </summary>
    /// <remarks>
    /// This control raises typed events; the host form forwards them to
    /// <see cref="Services.UIParserService"/> and calls <see cref="RefreshGrid"/>
    /// after any mutation.
    /// </remarks>
    [System.ComponentModel.DesignerCategory("Code")]
    public sealed class ColumnGridControl : UserControl
    {
        // -----------------------------------------------------------------
        //  Events
        // -----------------------------------------------------------------

        /// <summary>Raised when the user selects a different row. Value is the column or null.</summary>
        public event EventHandler<ColumnDefinition?>?          ColumnSelected;

        /// <summary>Raised when the user clicks an "Add X Column" menu item.</summary>
        public event EventHandler<string>?                     AddColumnRequested;

        /// <summary>Raised when the user clicks Remove. Value is the column Id.</summary>
        public event EventHandler<Guid>?                       RemoveRequested;

        /// <summary>Raised when the user clicks Move Up. Value is the column Id.</summary>
        public event EventHandler<Guid>?                       MoveUpRequested;

        /// <summary>Raised when the user clicks Move Down. Value is the column Id.</summary>
        public event EventHandler<Guid>?                       MoveDownRequested;

        /// <summary>Raised when the user drag-drops to reorder. Value is Item1: Id, Item2: New Index.</summary>
        public event EventHandler<Tuple<Guid, int>>?           ReorderRequested;

        // -----------------------------------------------------------------
        //  Controls
        // -----------------------------------------------------------------

        private ModernColumnList _list     = null!;
        private ModernButton     _btnAdd   = null!;
        private ContextMenuStrip _ctxMenu  = null!;
        private ContextMenuStrip _addMenu  = null!;
        private bool _suppressSelection;
        private Point _dragStartPoint;

        public ColumnGridControl()
        {
            BuildUI();
        }

        public ColumnDefinition? SelectedColumn { get; private set; }

        private Guid? _pendingSelectionId;

        public void SelectColumnNextRefresh(Guid columnId)
        {
            _pendingSelectionId = columnId;
        }

        public void RefreshGrid(IEnumerable<ColumnDefinition?> columns)
        {
            var prevId = SelectedColumn?.Id;

            _suppressSelection = true;
            _list.Items.Clear();

            foreach (var col in columns.Where(c => c != null).OrderBy(c => c!.Order))
            {
                _list.Items.Add(col!);
            }

            _suppressSelection = false;

            if (prevId.HasValue)
                SelectColumn(prevId.Value);
        }

        public void SelectColumn(Guid columnId)
        {
            for (int i = 0; i < _list.Items.Count; i++)
            {
                if (_list.Items[i] is ColumnDefinition col && col.Id == columnId)
                {
                    _list.SelectedIndex = i;
                    return;
                }
            }
        }

        private void BuildUI()
        {
            SuspendLayout();
            BackColor = Color.FromArgb(240, 242, 245);
            Padding = new Padding(10);

            // Container for Header
            var pnlHeader = new Panel { Dock = DockStyle.Top, Height = 55 };
            var lblTitle = new Label { Text = "Columns", Font = new Font("Segoe UI", 12f, FontStyle.Bold), AutoSize = true, Location = new Point(0, 5) };
            
            var lblHeaders = new Label { Text = "No       Name                                    Type", Font = new Font("Segoe UI", 9f), ForeColor = Color.Gray, Location = new Point(0, 35), AutoSize = true };
            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblHeaders);

            // Add Button
            _btnAdd = new ModernButton
            {
                Text = "＋ Add Column",
                Dock = DockStyle.Bottom,
                Height = 35,
                Margin = new Padding(0, 10, 0, 0)
            };

            // Spacer between list and button
            var spacer = new Panel { Dock = DockStyle.Bottom, Height = 10 };

            // The exact list
            _list = new ModernColumnList
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(240, 242, 245),
                AllowDrop = true
            };
            _list.SelectedIndexChanged += OnListSelectionChanged;
            _list.MouseDown += OnListMouseDown;
            _list.MouseMove += OnListMouseMove;
            _list.DragOver += OnListDragOver;
            _list.DragDrop += OnListDragDrop;

            // Context Menu for right click -> Move/Delete
            _ctxMenu = new ContextMenuStrip();
            _ctxMenu.Items.Add("Move ▲ Up", null, (_, _) => { if (SelectedColumn != null) MoveUpRequested?.Invoke(this, SelectedColumn.Id); });
            _ctxMenu.Items.Add("Move ▼ Down", null, (_, _) => { if (SelectedColumn != null) MoveDownRequested?.Invoke(this, SelectedColumn.Id); });
            _ctxMenu.Items.Add("-");
            _ctxMenu.Items.Add("✕ Remove", null, (_, _) => { if (SelectedColumn != null) RemoveRequested?.Invoke(this, SelectedColumn.Id); });
            _list.ContextMenuStrip = _ctxMenu;

            // Add Menu mapping
            _addMenu = new ContextMenuStrip();
            foreach (var typeName in new[] { nameof(PropertyColumn), nameof(FormulaColumn), nameof(ConditionalColumn), nameof(FixedTextColumn), nameof(CustomFunctionColumn) })
            {
                var label = typeName.Replace("Column", " Column");
                var tn = typeName;
                _addMenu.Items.Add(label, null, (_, _) => AddColumnRequested?.Invoke(this, tn));
            }

            _btnAdd.Click += (_, _) => _addMenu.Show(_btnAdd, new Point(0, 0), ToolStripDropDownDirection.AboveRight);

            Controls.Clear();
            Controls.Add(_btnAdd);
            Controls.Add(spacer);
            Controls.Add(pnlHeader);
            Controls.Add(_list);
            _list.BringToFront();

            ResumeLayout(false);
        }

        private void OnListSelectionChanged(object? sender, EventArgs e)
        {
            if (_suppressSelection) return;

            SelectedColumn = _list.SelectedItem as ColumnDefinition;
            ColumnSelected?.Invoke(this, SelectedColumn);
        }
        
        private void OnListMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                var index = _list.IndexFromPoint(e.Location);
                if (index != ListBox.NoMatches)
                {
                    _list.SelectedIndex = index;
                }
            }
            else if (e.Button == MouseButtons.Left)
            {
                _dragStartPoint = e.Location;
            }
        }

        private void OnListMouseMove(object? sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                if (Math.Abs(e.X - _dragStartPoint.X) > SystemInformation.DragSize.Width ||
                    Math.Abs(e.Y - _dragStartPoint.Y) > SystemInformation.DragSize.Height)
                {
                    var index = _list.IndexFromPoint(_dragStartPoint);
                    if (index != ListBox.NoMatches)
                    {
                        var item = _list.Items[index];
                        _list.DoDragDrop(item, DragDropEffects.Move);
                    }
                }
            }
        }

        private void OnListDragOver(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(typeof(ColumnDefinition)) == true)
                e.Effect = DragDropEffects.Move;
            else
                e.Effect = DragDropEffects.None;
        }

        private void OnListDragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(typeof(ColumnDefinition)) == true)
            {
                var draggedCol = (ColumnDefinition)e.Data.GetData(typeof(ColumnDefinition))!;
                var point = _list.PointToClient(new Point(e.X, e.Y));
                var targetIndex = _list.IndexFromPoint(point);
                if (targetIndex == ListBox.NoMatches) targetIndex = _list.Items.Count - 1;

                if (targetIndex >= 0 && targetIndex < _list.Items.Count)
                {
                    ReorderRequested?.Invoke(this, Tuple.Create(draggedCol.Id, targetIndex));
                }
            }
        }

        public void RefreshGrid(List<ColumnDefinition> columns)
        {
            var prevId = _pendingSelectionId ?? SelectedColumn?.Id;
            _pendingSelectionId = null;
            
            _suppressSelection = true;
            
            _list.BeginUpdate();
            _list.Items.Clear();

            foreach (var col in columns.Where(c => c != null).OrderBy(c => c!.Order))
            {
                _list.Items.Add(col!);
            }
            _list.EndUpdate();

            if (_list.Items.Count == 0)
            {
                _suppressSelection = false;
                SelectedColumn = null;
                ColumnSelected?.Invoke(this, null);
                return;
            }

            // Try to restore previous selection WITHOUT firing selection event 
            // (If we fire it, SwapEditor will destroy the very textbox the user is typing in)
            bool Restored = false;
            if (prevId.HasValue)
            {
                for (int i = 0; i < _list.Items.Count; i++)
                {
                    if (_list.Items[i] is ColumnDefinition c && c.Id == prevId.Value)
                    {
                        _list.SelectedIndex = i;
                        SelectedColumn = c; // Update local ref (name might have changed)
                        Restored = true;
                        break;
                    }
                }
            }

            _suppressSelection = false;

            // Only fire selection if we didn't restore the active one
            if (!Restored && _list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }
        }
    }
}
