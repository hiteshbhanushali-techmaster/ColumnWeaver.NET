using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ColumnWeaver.NET.UI.Controls.Modern
{
    public class ModernToggle : CheckBox
    {
        private Color _onBackColor = Color.FromArgb(51, 141, 255);
        private Color _onToggleColor = Color.WhiteSmoke;
        private Color _offBackColor = Color.LightGray;
        private Color _offToggleColor = Color.Gainsboro;
        
        public Color OnBackColor
        {
            get => _onBackColor;
            set { _onBackColor = value; Invalidate(); }
        }

        public ModernToggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            AutoSize = false;
            Height = 22;
            Width = 45;
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            // Do not call base.OnPaint to skip drawing standard checkbox
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent.BackColor);

            var arcHeight = Height - 2;
            var path = GetFigurePath(new Rectangle(1, 1, Width - 2, Height - 2), arcHeight / 2);

            if (Checked)
            {
                // Draw On state
                g.FillPath(new SolidBrush(_onBackColor), path);
                g.FillEllipse(new SolidBrush(_onToggleColor), 
                    new Rectangle(Width - Height + 1, 2, Height - 4, Height - 4));
            }
            else
            {
                // Draw Off state
                g.FillPath(new SolidBrush(_offBackColor), path);
                g.FillEllipse(new SolidBrush(_offToggleColor), 
                    new Rectangle(2, 2, Height - 4, Height - 4));
            }
            
            // Text rendering can be added here if needed, but normally toggle is besides a label.
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
