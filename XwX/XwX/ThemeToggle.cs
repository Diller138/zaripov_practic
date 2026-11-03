using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GameAccountRental
{
    /// <summary>
    /// Переключатель светлой/тёмной темы (Toggle Switch) с анимацией.
    /// Сам синхронизируется с Theme.IsDarkMode через событие Theme.ThemeChanged,
    /// поэтому все тумблеры на открытых формах переключаются одновременно.
    /// </summary>
    public class ThemeToggle : Control
    {
        private const int TrackW = 52;
        private const int TrackH = 26;
        private const int RightGap = 20;       // отступ справа от дорожки

        private readonly Timer _timer = new Timer { Interval = 15 };
        private float _pos;                    // 0 = светлая, 1 = тёмная
        private bool _target;

        public ThemeToggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
            Size = new Size(TrackW + RightGap, 64);
            Cursor = Cursors.Hand;
            TabStop = false;

            _target = Theme.IsDarkMode;
            _pos = _target ? 1f : 0f;

            _timer.Tick += (s, e) => Step();
            Theme.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            _target = Theme.IsDarkMode;
            _timer.Start();
        }

        private void Step()
        {
            float goal = _target ? 1f : 0f;
            float d = goal - _pos;
            if (Math.Abs(d) < 0.05f)
            {
                _pos = goal;
                _timer.Stop();
            }
            else
            {
                _pos += d * 0.35f;
            }
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Theme.SetDark(!Theme.IsDarkMode);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float top = (Height - TrackH) / 2f;
            var track = new RectangleF(0.5f, top, TrackW - 1f, TrackH);

            Color off = Theme.IsDarkMode ? Color.FromArgb(90, 90, 96) : Color.FromArgb(176, 182, 192);
            Color fill = Lerp(off, Theme.Accent, _pos);

            using (var path = Pill(track))
            using (var br = new SolidBrush(fill))
                g.FillPath(br, path);

            const int knob = TrackH - 6;
            float travel = TrackW - 6 - knob;
            float x = 3 + _pos * travel;

            using (var br = new SolidBrush(Color.White))
                g.FillEllipse(br, x, top + 3, knob, knob);
        }

        private static GraphicsPath Pill(RectangleF r)
        {
            float d = r.Height;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 90, 180);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            p.CloseFigure();
            return p;
        }

        private static Color Lerp(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Theme.ThemeChanged -= OnThemeChanged;
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
