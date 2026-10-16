using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ColumnWeaver.NET.UI.Controls.Modern
{
    public class ModernButton : Button
    {
        private int _borderRadius = 8;
        private Color _hoverColor = Color.FromArgb(41, 121, 230);
        private Color _normalColor = Color.FromArgb(51, 141, 255);
        private Color _pressedColor = Color.FromArgb(31, 101, 210);

        private bool _isHovered;
        private bool _isPressed;

        public int BorderRadius
        {
            get => _borderRadius;
            set { _borderRadius = value; Invalidate(); }
        }

        public Color NormalColor
        {
            get => _normalColor;
            set { _normalColor = value; Invalidate(); }
        }
        
        public Color HoverColor
        {
            get => _hoverColor;
            set { _hoverColor = value; Invalidate(); }
        }

        public ModernButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = _normalColor;
            ForeColor = Color.White;
            Cursor = Cursors.Hand;
            Resize += (s, e) => Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            _isPressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            base.OnPaint(pevent);
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rectSurface = new Rectangle(0, 0, Width, Height);
            var rectBorder = Rectangle.Inflate(rectSurface, -1, -1);
            var smoothSize = 2;
            
            if (_borderRadius > 2)
            {
                using var pathSurface = GetFigurePath(rectSurface, _borderRadius);
                using var pathBorder = GetFigurePath(rectBorder, _borderRadius - 1);
                using var penSurface = new Pen(Parent.BackColor, smoothSize);
                
                Region = new Region(pathSurface);
                pevent.Graphics.DrawPath(penSurface, pathSurface);

                var bgColor = _isPressed ? _pressedColor : (_isHovered ? _hoverColor : _normalColor);
                if (!Enabled) bgColor = Color.Gray;

                using var brush = new SolidBrush(bgColor);
                pevent.Graphics.FillPath(brush, pathSurface);
            }

            // Draw text
            TextRenderer.DrawText(pevent.Graphics, Text, Font, rectSurface, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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
