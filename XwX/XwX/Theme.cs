using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using XwX;

namespace GameAccountRental
{
    public enum BtnKind { Primary, Secondary, Danger, Success }
    public enum Skin { Bg, Surface, Input, Text, Muted, Accent }

    public static class Theme
    {
        // ======================= ТЕМА =======================
        public static event EventHandler ThemeChanged;
        public static bool IsDarkMode { get; set; } = false;

        public static Color Bg { get { return IsDarkMode ? Color.FromArgb(30, 30, 30) : Color.FromArgb(243, 244, 247); } }
        public static Color Surface { get { return IsDarkMode ? Color.FromArgb(37, 37, 38) : Color.White; } }
        public static Color Surface2 { get { return IsDarkMode ? Color.FromArgb(51, 51, 55) : Color.FromArgb(236, 239, 244); } }
        public static Color Border { get { return IsDarkMode ? Color.FromArgb(63, 63, 70) : Color.FromArgb(208, 213, 221); } }
        public static Color Text { get { return IsDarkMode ? Color.FromArgb(241, 241, 241) : Color.FromArgb(31, 35, 41); } }
        public static Color Muted { get { return IsDarkMode ? Color.FromArgb(156, 156, 160) : Color.FromArgb(99, 106, 117); } }
        public static Color Accent { get { return Color.FromArgb(0, 122, 204); } }

        private static readonly Font BtnFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        private static readonly Font HeaderFont = new Font("Segoe UI", 15f, FontStyle.Bold);
        private static readonly Font GridHeaderFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        private static readonly Font InputFont = new Font("Segoe UI", 11f);
        private static readonly Font ListFont = new Font("Segoe UI", 10.5f);

        public static void SetDark(bool dark)
        {
            if (IsDarkMode == dark) return;
            IsDarkMode = dark;

            // перекрашиваем все открытые формы
            foreach (Form f in Application.OpenForms.Cast<Form>().ToArray())
                ApplyTheme(f);

            // сообщаем тумблерам (ThemeToggle), чтобы они доехали до нужного положения
            EventHandler handler = ThemeChanged;
            if (handler != null) handler(null, EventArgs.Empty);
        }

        public static void Apply(Form f)
        {
            f.Icon = AppIcon;
            ApplyTheme(f);
        }

        public static void ApplyTheme(Control root)
        {
            root.SuspendLayout();
            ThemeControl(root);
            root.ResumeLayout(true);
            root.Invalidate(true);
        }

        public static T Mark<T>(T c, Skin skin) where T : Control
        {
            c.Tag = skin;
            return c;
        }

        private static Color SkinColor(Skin s)
        {
            switch (s)
            {
                case Skin.Surface: return Surface;
                case Skin.Input: return Surface2;
                case Skin.Accent: return Accent;
                case Skin.Text: return Text;
                case Skin.Muted: return Muted;
                default: return Bg;
            }
        }

        private static void ThemeControl(Control c)
        {
            Skin? skin = c.Tag is Skin ? (Skin?)(Skin)c.Tag : null;
            Button btn = c as Button;
            TextBox tb = c as TextBox;

            if (c is Form)
            {
                c.BackColor = Bg;
                c.ForeColor = Text;
            }
            else if (btn != null)
            {
                if (btn.Tag is BtnKind) StyleButton(btn, (BtnKind)btn.Tag);
            }
            else if (tb != null)
            {
                StyleTextColors(tb);
            }
            else if (c is ComboBox || c is NumericUpDown || c is ListBox)
            {
                c.BackColor = Surface2;
                c.ForeColor = Text;
            }
            else if (c is DataGridView)
            {
                RestyleGrid((DataGridView)c);
            }
            else if (c is TabPage)
            {
                c.BackColor = Bg;
                c.ForeColor = Text;
            }
            else if (c is TabControl)
            {
                c.Invalidate();
            }
            else if (c is TableLayoutPanel || c is FlowLayoutPanel)
            {
                c.BackColor = skin.HasValue ? SkinColor(skin.Value) : (c.Parent != null ? c.Parent.BackColor : Bg);
            }
            else if (c is Panel)
            {
                c.BackColor = SkinColor(skin ?? Skin.Bg);
            }
            else if (c is Label)
            {
                c.ForeColor = skin.HasValue && skin.Value == Skin.Muted ? Muted
                            : skin.HasValue && skin.Value == Skin.Accent ? Accent
                            : Text;
            }
            else if (c is CheckBox)
            {
                c.ForeColor = Text;
            }

            foreach (Control child in c.Controls)
                ThemeControl(child);
        }

        // ======================= КНОПКИ =======================
        public static Button MakeButton(string text, BtnKind kind = BtnKind.Secondary)
        {
            var b = new Button
            {
                Text = text,
                Width = 140,
                Height = 38,
                FlatStyle = FlatStyle.Flat,
                Font = BtnFont,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                Tag = kind
            };
            b.FlatAppearance.BorderSize = 0;
            StyleButton(b, kind);
            return b;
        }

        private static void StyleButton(Button b, BtnKind kind)
        {
            Color normal, hover, fore = Color.White;
            switch (kind)
            {
                case BtnKind.Primary: normal = Color.FromArgb(0, 122, 204); hover = Color.FromArgb(28, 151, 234); break;
                case BtnKind.Danger: normal = Color.FromArgb(192, 57, 43); hover = Color.FromArgb(220, 76, 60); break;
                case BtnKind.Success: normal = Color.FromArgb(39, 142, 76); hover = Color.FromArgb(52, 168, 92); break;
                default:
                    if (IsDarkMode) { normal = Color.FromArgb(62, 62, 66); hover = Color.FromArgb(80, 80, 86); }
                    else { normal = Color.FromArgb(222, 226, 233); hover = Color.FromArgb(207, 213, 222); fore = Text; }
                    break;
            }
            b.BackColor = normal;
            b.ForeColor = fore;
            b.FlatAppearance.MouseOverBackColor = hover;
            b.FlatAppearance.MouseDownBackColor = normal;
        }

        // ======================= ПОЛЯ ВВОДА =======================
        private static void StyleTextColors(TextBox t)
        {
            if (!t.Enabled || t.ReadOnly)
            {
                t.BackColor = Bg;
                t.ForeColor = Muted;
            }
            else
            {
                t.BackColor = Surface2;
                t.ForeColor = Text;
            }
        }

        public static void StyleInput(TextBox t)
        {
            t.BorderStyle = BorderStyle.FixedSingle;
            t.Font = InputFont;
            StyleTextColors(t);
        }

        public static void StyleCombo(ComboBox c)
        {
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.FlatStyle = FlatStyle.Flat;
            c.Font = InputFont;
            c.BackColor = Surface2;
            c.ForeColor = Text;
        }

        public static void StyleNumeric(NumericUpDown n)
        {
            n.BorderStyle = BorderStyle.FixedSingle;
            n.Font = InputFont;
            n.BackColor = Surface2;
            n.ForeColor = Text;
        }

        public static void StyleCheckedList(CheckedListBox l)
        {
            l.BorderStyle = BorderStyle.FixedSingle;
            l.Font = ListFont;
            l.BackColor = Surface2;
            l.ForeColor = Text;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        private const int EM_SETCUEBANNER = 0x1501;

        public static void SetPlaceholder(TextBox t, string text)
        {
            Action apply = () => SendMessage(t.Handle, EM_SETCUEBANNER, (IntPtr)1, text);
            if (t.IsHandleCreated) apply();
            else t.HandleCreated += (s, e) => apply();
        }

        // ======================= ТАБЛИЦЫ =======================
        public static Color? StatusColor(string name)
        {
            if (name == null) return null;
            switch (name.Trim().ToLowerInvariant())
            {
                case "свободен":
                case "активен":
                    return IsDarkMode ? Color.FromArgb(106, 212, 130) : Color.FromArgb(22, 130, 60);
                case "занят":
                case "арендован":
                case "заблокирован":
                case "отменён":
                    return IsDarkMode ? Color.FromArgb(238, 110, 110) : Color.FromArgb(192, 40, 40);
                case "создан":
                    return IsDarkMode ? Color.FromArgb(240, 190, 70) : Color.FromArgb(176, 120, 0);
                case "принят":
                    return IsDarkMode ? Color.FromArgb(90, 180, 245) : Color.FromArgb(0, 100, 180);
                case "выдан":
                    return IsDarkMode ? Color.FromArgb(170, 160, 255) : Color.FromArgb(96, 80, 200);
                default:
                    return null;
            }
        }

        public static void StyleGrid(DataGridView dgv)
        {
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.RowHeadersVisible = false;
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgv.ColumnHeadersHeight = 40;
            dgv.RowTemplate.Height = 36;
            RestyleGrid(dgv);

            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.Value == null || e.ColumnIndex < 0) return;
                if (dgv.Columns[e.ColumnIndex].HeaderText != "Статус") return;
                Color? c = StatusColor(e.Value.ToString());
                if (c.HasValue) e.CellStyle.ForeColor = c.Value;
            };
        }

        public static void RestyleGrid(DataGridView dgv)
        {
            dgv.BackgroundColor = Bg;
            dgv.GridColor = Border;

            dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Surface2,
                ForeColor = Text,
                SelectionBackColor = Surface2,
                SelectionForeColor = Text,
                Font = GridHeaderFont,
                Padding = new Padding(8, 0, 8, 0)
            };
            dgv.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Surface,
                ForeColor = Text,
                SelectionBackColor = Accent,
                SelectionForeColor = Color.White,
                Padding = new Padding(8, 0, 8, 0)
            };
            dgv.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = IsDarkMode ? Color.FromArgb(43, 43, 46) : Color.FromArgb(247, 248, 250),
                ForeColor = Text,
                SelectionBackColor = Accent,
                SelectionForeColor = Color.White,
                Padding = new Padding(8, 0, 8, 0)
            };
            dgv.Invalidate();
        }

        // ======================= ШАПКА / КАРТОЧКА / СТРОКИ =======================
        public static Panel MakeHeader(string title, bool showUser)
        {
            var p = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Surface, Tag = Skin.Surface };

            var lbl = new Label
            {
                Text = title,
                ForeColor = Text,
                Font = HeaderFont,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0)
            };
            p.Controls.Add(lbl);

            if (showUser && Session.CurrentUser != null)
            {
                var user = new Label
                {
                    Text = Session.CurrentUser.Login + "  ·  " + Session.CurrentUser.RoleName,
                    ForeColor = Muted,
                    Tag = Skin.Muted,
                    AutoSize = false,
                    Dock = DockStyle.Right,
                    Width = 240,
                    TextAlign = ContentAlignment.MiddleRight,
                    Padding = new Padding(0, 0, 12, 0)
                };
                p.Controls.Add(user);
            }

            // Тумблер темы (самый правый элемент шапки)
            var toggleHost = new Panel { Dock = DockStyle.Right, Width = 170, BackColor = Surface, Tag = Skin.Surface };
            var toggleCaption = new Label
            {
                Text = "Тёмная тема",
                ForeColor = Muted,
                Tag = Skin.Muted,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 8, 0)
            };
            var toggle = new ThemeToggle { Dock = DockStyle.Right };
            toggleHost.Controls.Add(toggleCaption);   // Fill первым
            toggleHost.Controls.Add(toggle);          // затем Right
            p.Controls.Add(toggleHost);

            p.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 4, BackColor = Accent, Tag = Skin.Accent });
            return p;
        }

        public static Panel MakeCard(int width)
        {
            return new Panel
            {
                BackColor = Surface,
                Tag = Skin.Surface,
                Width = width,
                Padding = new Padding(32)
            };
        }

        public static void CenterIn(Control host, Control card)
        {
            Action place = () =>
            {
                card.Left = Math.Max(0, (host.ClientSize.Width - card.Width) / 2);
                card.Top = Math.Max(0, (host.ClientSize.Height - card.Height) / 2);
            };
            host.Resize += (s, e) => place();
            card.SizeChanged += (s, e) => place();
            place();
        }

        public static Label MakeCaption(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Muted,
                Tag = Skin.Muted,
                Margin = new Padding(3, 10, 3, 2)
            };
        }

        public static void AddStacked(TableLayoutPanel t, string caption, Control c)
        {
            int r = t.RowCount;
            t.Controls.Add(MakeCaption(caption), 0, r);
            t.RowCount = r + 1;

            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(3, 0, 3, 6);
            r = t.RowCount;
            t.Controls.Add(c, 0, r);
            t.RowCount = r + 1;
        }

        public static void AddRow(TableLayoutPanel t, string caption, Control c)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = true,
                ForeColor = Muted,
                Tag = Skin.Muted,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 8, 14, 8)
            };
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(3, 4, 3, 4);

            int r = t.RowCount;
            t.Controls.Add(lbl, 0, r);
            t.Controls.Add(c, 1, r);
            t.RowCount = r + 1;
        }

        // ======================= СООБЩЕНИЯ =======================
        public static void Info(string msg) { MessageBox.Show(msg, "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        public static void Warn(string msg) { MessageBox.Show(msg, "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        public static void Error(string msg) { MessageBox.Show(msg, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }

        // ======================= ДИАЛОГ ВЫБОРА ЧИСЛА =======================
        public static int? PromptNumber(IWin32Window owner, string title, string caption, int min, int max, int def)
        {
            int? result = null;

            using (var f = new Form())
            {
                f.Text = title;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MaximizeBox = false;
                f.MinimizeBox = false;
                f.ShowInTaskbar = false;
                f.ClientSize = new Size(360, 180);
                f.Font = new Font("Segoe UI", 10f);

                var lbl = new Label { Text = caption, Left = 20, Top = 18, Width = 320, Height = 24 };
                var btnMinus = MakeButton("−", BtnKind.Secondary);
                btnMinus.Left = 20; btnMinus.Top = 54; btnMinus.Width = 48; btnMinus.Height = 38;

                var txt = new TextBox { Left = 76, Top = 56, Width = 208, TextAlign = HorizontalAlignment.Center };
                StyleInput(txt);
                txt.Font = new Font("Segoe UI", 14f);
                txt.MaxLength = max.ToString().Length;
                txt.Text = def.ToString();

                var btnPlus = MakeButton("+", BtnKind.Secondary);
                btnPlus.Left = 292; btnPlus.Top = 54; btnPlus.Width = 48; btnPlus.Height = 38;

                var ok = MakeButton("Арендовать", BtnKind.Primary);
                ok.Left = 20; ok.Top = 116; ok.Width = 150;

                var cancel = MakeButton("Отмена", BtnKind.Secondary);
                cancel.Left = 190; cancel.Top = 116; cancel.Width = 150;
                cancel.DialogResult = DialogResult.Cancel;

                Func<int> read = () =>
                {
                    int v;
                    if (!int.TryParse(txt.Text, out v)) v = def;
                    return Math.Max(min, Math.Min(max, v));
                };
                Action<int> write = v => txt.Text = Math.Max(min, Math.Min(max, v)).ToString();

                btnMinus.Click += (s, e) => write(read() - 1);
                btnPlus.Click += (s, e) => write(read() + 1);

                txt.KeyPress += (s, e) => { e.Handled = !(char.IsDigit(e.KeyChar) || char.IsControl(e.KeyChar)); };
                txt.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Up) { write(read() + 1); e.Handled = true; }
                    else if (e.KeyCode == Keys.Down) { write(read() - 1); e.Handled = true; }
                };
                txt.Leave += (s, e) => write(read());

                ok.Click += (s, e) =>
                {
                    int v;
                    if (!int.TryParse(txt.Text, out v) || v < min || v > max)
                    {
                        Warn(string.Format("Введите число от {0} до {1}.", min, max));
                        return;
                    }
                    result = v;
                    f.DialogResult = DialogResult.OK;
                };

                f.Controls.Add(lbl);
                f.Controls.Add(btnMinus);
                f.Controls.Add(txt);
                f.Controls.Add(btnPlus);
                f.Controls.Add(ok);
                f.Controls.Add(cancel);
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                Apply(f);

                return f.ShowDialog(owner) == DialogResult.OK ? result : null;
            }
        }

        // ======================= ИКОНКА =======================
        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private static Icon _appIcon;

        public static Icon AppIcon
        {
            get
            {
                if (_appIcon == null) _appIcon = BuildIcon();
                return _appIcon;
            }
        }

        public static void SaveAppIconFile(string path)
        {
            using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Create))
                AppIcon.Save(fs);
        }

        private static Icon BuildIcon()
        {
            using (var bmp = new Bitmap(64, 64))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (var path = RoundRect(2, 2, 60, 60, 14))
                    using (var br = new SolidBrush(Accent))
                        g.FillPath(br, path);
                    DrawGamepad(g, 0, 0, 64, Color.White, Accent);
                }

                IntPtr h = bmp.GetHicon();
                try
                {
                    using (Icon tmp = Icon.FromHandle(h))
                        return (Icon)tmp.Clone();
                }
                finally { DestroyIcon(h); }
            }
        }

        private static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
        {
            float d = r * 2;
            var p = new GraphicsPath();
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private static void DrawGamepad(Graphics g, float x, float y, float size, Color body, Color detail)
        {
            float s = size / 64f;
            using (var bodyBr = new SolidBrush(body))
            using (var detBr = new SolidBrush(detail))
            {
                using (var path = RoundRect(x + 12 * s, y + 22 * s, 40 * s, 20 * s, 8 * s))
                    g.FillPath(bodyBr, path);
                g.FillEllipse(bodyBr, x + 8 * s, y + 26 * s, 20 * s, 26 * s);
                g.FillEllipse(bodyBr, x + 36 * s, y + 26 * s, 20 * s, 26 * s);
                g.FillRectangle(bodyBr, x + 15 * s, y + 17 * s, 11 * s, 5 * s);
                g.FillRectangle(bodyBr, x + 38 * s, y + 17 * s, 11 * s, 5 * s);

                g.FillRectangle(detBr, x + 16 * s, y + 30 * s, 10 * s, 4 * s);
                g.FillRectangle(detBr, x + 19 * s, y + 27 * s, 4 * s, 10 * s);

                g.FillEllipse(detBr, x + 40 * s, y + 27 * s, 5 * s, 5 * s);
                g.FillEllipse(detBr, x + 46 * s, y + 32 * s, 5 * s, 5 * s);
            }
        }

        // ======================= ЗАГЛУШКА ДЛЯ ОБЛОЖЕК =======================
        private static readonly Dictionary<string, Bitmap> PlaceholderCache = new Dictionary<string, Bitmap>();

        public static Bitmap Placeholder(int w, int h)
        {
            string key = w + "x" + h + (IsDarkMode ? "d" : "l");
            Bitmap bmp;
            if (PlaceholderCache.TryGetValue(key, out bmp)) return bmp;

            bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Surface2);
                float size = Math.Min(w, h) * 0.6f;
                DrawGamepad(g, (w - size) / 2f, (h - size) / 2f, size, Color.FromArgb(120, Muted), Surface2);
            }
            PlaceholderCache[key] = bmp;
            return bmp;
        }
    }

    public class ThemedTabControl : TabControl
    {
        public ThemedTabControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SizeMode = TabSizeMode.Fixed;
            ItemSize = new Size(150, 38);
            Padding = new Point(16, 6);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            int bottom = 0;

            for (int i = 0; i < TabCount; i++)
            {
                Rectangle r = GetTabRect(i);
                bool sel = i == SelectedIndex;

                using (var br = new SolidBrush(sel ? Theme.Surface : Theme.Bg))
                    g.FillRectangle(br, r);

                TextRenderer.DrawText(g, TabPages[i].Text, Font, r,
                    sel ? Theme.Text : Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                if (sel)
                    using (var br = new SolidBrush(Theme.Accent))
                        g.FillRectangle(br, r.Left, r.Bottom - 3, r.Width, 3);

                bottom = Math.Max(bottom, r.Bottom);
            }

            using (var pen = new Pen(Theme.Border))
                g.DrawLine(pen, 0, bottom, Width, bottom);
        }
    }
}