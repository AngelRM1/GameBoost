using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GameBoost
{
    /// <summary>
    /// Tema minimalista v1.5: fondo casi negro, tarjetas planas con borde
    /// sutil, un solo color de acento y menu vertical a la izquierda.
    /// Todo en un solo lugar para que el diseno sea coherente.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(14, 17, 22);          // #0E1116
        public static readonly Color PanelBg = Color.FromArgb(21, 26, 33);     // #151A21
        public static readonly Color CardBg = Color.FromArgb(23, 28, 36);      // #171C24
        public static readonly Color CardBorder = Color.FromArgb(35, 42, 53);  // #232A35
        public static readonly Color Accent = Color.FromArgb(34, 197, 94);
        public static readonly Color AccentBlue = Color.FromArgb(59, 130, 246);
        public static readonly Color Warn = Color.FromArgb(245, 158, 11);
        public static readonly Color Danger = Color.FromArgb(239, 68, 68);
        public static readonly Color TextLight = Color.FromArgb(232, 236, 241);
        public static readonly Color TextMuted = Color.FromArgb(138, 148, 166);
        public static readonly Color InfoBg = Color.FromArgb(22, 32, 44);
        public static readonly Color InfoBorder = Color.FromArgb(44, 74, 107);

        // (Antes habia un StyleTabs para el TabControl nativo; el menu ahora
        // es un Panel propio con botones, asi que ya no hace falta.)

        public static GraphicsPath RoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>Panel con esquinas redondeadas y borde, para las tarjetas.</summary>
    public class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; } = 10;
        public Color BorderColor { get; set; } = Theme.CardBorder;
        public int BorderWidth { get; set; } = 1;

        public RoundedPanel()
        {
            BackColor = Theme.CardBg;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = Theme.RoundedRect(rect, CornerRadius);
            using var brush = new SolidBrush(BackColor);
            g.FillPath(brush, path);
            if (BorderWidth > 0)
            {
                using var pen = new Pen(BorderColor, BorderWidth);
                g.DrawPath(pen, path);
            }
            base.OnPaint(e);
        }
    }

    /// <summary>Caja de informacion (azul/amarilla) con texto descriptivo.</summary>
    public class InfoBox : RoundedPanel
    {
        public InfoBox(string text, Color? border = null)
        {
            BackColor = Theme.InfoBg;
            BorderColor = border ?? Theme.InfoBorder;
            CornerRadius = 10;
            var lbl = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(219, 234, 254),
                Font = new Font("Segoe UI", 9.5f),
                Padding = new Padding(12, 8, 12, 8),
            };
            Controls.Add(lbl);
        }
    }
}
