using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ColumnWeaver.NET.Core.Columns;

namespace ColumnWeaver.NET.UI.Controls.Modern
{
    public class ModernColumnList : ListBox
    {
        public ModernColumnList()
        {
            DrawMode = DrawMode.OwnerDrawVariable;
            BorderStyle = BorderStyle.None;
            IntegralHeight = false;
            ItemHeight = 40;
            DoubleBuffered = true;
        }

        protected override void OnMeasureItem(MeasureItemEventArgs e)
        {
            base.OnMeasureItem(e);
            e.ItemHeight = 40;
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;

            var col = Items[e.Index] as ColumnDefinition;
            if (col == null) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var rect = e.Bounds;

            // Background
            using (var b = new SolidBrush(isSelected ? Color.FromArgb(41, 121, 230) : Parent.BackColor))
            {
                g.FillRectangle(b, rect);
            }

            // Foreground Text
            var textColor = isSelected ? Color.White : ForeColor;
            var font = new Font(Font.FontFamily, 9.5f, FontStyle.Regular);
            
            // Icon Placeholder
            var iconChar = "📄";
            if (col is FormulaColumn) iconChar = "∑";
            if (col is ConditionalColumn) iconChar = "🔀";

            TextRenderer.DrawText(g, iconChar, font, new Point(rect.X + 10, rect.Y + 10), textColor);
            
            // Name
            TextRenderer.DrawText(g, col.Name, font, new Point(rect.X + 40, rect.Y + 10), textColor);

            // Type Badge
            var typeName = col.GetType().Name.Replace("Column", "");
            var badgeRect = new Rectangle(rect.Right - 80, rect.Y + 10, 70, 20);
            
            using (var path = GetFigurePath(badgeRect, 4))
            {
                if (!isSelected)
                {
                    g.FillPath(new SolidBrush(Color.FromArgb(240, 242, 245)), path);
                }
                var badgeColor = isSelected ? Color.White : Color.Gray;
                TextRenderer.DrawText(g, typeName, new Font(Font.FontFamily, 8f), badgeRect, badgeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private GraphicsPath GetFigurePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            var curveSize = radius * 2f;
            path.StartFigure();
            path.AddArc(rect.X, rect.Y, curveSize, curveSize, 180, 90);
            path.AddArc(rect.Right - curveSize, rect.Y, curveSize, curveSize, 270, 90);
            path.AddArc(rect.Right - curveSize, rect.Bottom - curveSize, curveSize, curveSize, 0, 90);
            path.AddArc(rect.X, rect.Bottom - curveSize, curveSize, curveSize, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
