using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GameAccountRental
{
    public enum BtnKind { Primary, Secondary, Danger, Success }

    /// <summary>Единая тёмная тема для всех форм.</summary>
    public static class Theme
    {
        // ---------- палитра ----------
        public static readonly Color Bg = Color.FromArgb(30, 30, 30);
        public static readonly Color Surface = Color.FromArgb(37, 37, 38);
        public static readonly Color Surface2 = Color.FromArgb(51, 51, 55);
        public static readonly Color Border = Color.FromArgb(63, 63, 70);
        public static readonly Color Text = Color.FromArgb(241, 241, 241);
        public static readonly Color Muted = Color.FromArgb(156, 156, 160);
        public static readonly Color Accent = Color.FromArgb(0, 122, 204);

        // ---------- placeholder (EM_SETCUEBANNER) ----------
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        private const int EM_SETCUEBANNER = 0x1501;

        public static void SetPlaceholder(TextBox t, string text)
        {
            Action apply = () => SendMessage(t.Handle, EM_SETCUEBANNER, (IntPtr)1, text);
            if (t.IsHandleCreated) apply();
            else t.HandleCreated += (s, e) => apply();
        }

        // ---------- форма ----------
        public static void Apply(Form f)
        {
            f.BackColor = Bg;
            f.ForeColor = Text;
        }

        // ---------- кнопки ----------
        public static Button MakeButton(string text, BtnKind kind = BtnKind.Secondary)
        {
            Color normal, hover;
            switch (kind)
            {
                case BtnKind.Primary: normal = Color.FromArgb(0, 122, 204); hover = Color.FromArgb(28, 151, 234); break;
                case BtnKind.Danger: normal = Color.FromArgb(192, 57, 43); hover = Color.FromArgb(220, 76, 60); break;
                case BtnKind.Success: normal = Color.FromArgb(39, 142, 76); hover = Color.FromArgb(52, 168, 92); break;
                default: normal = Color.FromArgb(62, 62, 66); hover = Color.FromArgb(80, 80, 86); break;
            }

            var b = new Button
            {
                Text = text,
                Width = 140,
                Height = 38,
                FlatStyle = FlatStyle.Flat,
                BackColor = normal,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = hover;
            b.FlatAppearance.MouseDownBackColor = normal;
            return b;
        }

        // ---------- поля ввода ----------
        public static void StyleInput(TextBox t)
        {
            t.BackColor = Surface2;
            t.ForeColor = Text;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.Font = new Font("Segoe UI", 11f);
        }

        public static void StyleCombo(ComboBox c)
        {
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = Surface2;
            c.ForeColor = Text;
            c.Font = new Font("Segoe UI", 11f);
        }

        public static void StyleNumeric(NumericUpDown n)
        {
            n.BackColor = Surface2;
            n.ForeColor = Text;
            n.BorderStyle = BorderStyle.FixedSingle;
            n.Font = new Font("Segoe UI", 11f);
        }

        public static void StyleCheckedList(CheckedListBox l)
        {
            l.BackColor = Surface2;
            l.ForeColor = Text;
            l.BorderStyle = BorderStyle.FixedSingle;
            l.Font = new Font("Segoe UI", 10.5f);
        }

        // ---------- таблица ----------
        public static void StyleGrid(DataGridView dgv)
        {
            dgv.BackgroundColor = Bg;
            dgv.BorderStyle = BorderStyle.None;
            dgv.GridColor = Border;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.RowHeadersVisible = false;
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgv.ColumnHeadersHeight = 40;
            dgv.RowTemplate.Height = 36;

            dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Surface2,
                ForeColor = Text,
                SelectionBackColor = Surface2,
                SelectionForeColor = Text,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
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
                BackColor = Color.FromArgb(43, 43, 46),
                ForeColor = Text,
                SelectionBackColor = Accent,
                SelectionForeColor = Color.White,
                Padding = new Padding(8, 0, 8, 0)
            };

            // Цветные статусы в колонке «Статус»
            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.Value == null || e.ColumnIndex < 0) return;
                if (dgv.Columns[e.ColumnIndex].HeaderText != "Статус") return;

                Color c;
                switch (e.Value.ToString().Trim().ToLowerInvariant())
                {
                    case "свободен": c = Color.FromArgb(106, 212, 130); break;
                    case "занят": c = Color.FromArgb(238, 110, 110); break;
                    case "создан": c = Color.FromArgb(240, 190, 70); break;
                    case "принят": c = Color.FromArgb(90, 180, 245); break;
                    case "выдан": c = Color.FromArgb(170, 160, 255); break;
                    default: return;
                }
                e.CellStyle.ForeColor = c;
            };
        }

        // ---------- шапка формы ----------
        public static Panel MakeHeader(string title, bool showUser)
        {
            var p = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Surface };

            var lbl = new Label
            {
                Text = title,
                ForeColor = Text,
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
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
                    AutoSize = false,
                    Dock = DockStyle.Right,
                    Width = 280,
                    TextAlign = ContentAlignment.MiddleRight,
                    Padding = new Padding(0, 0, 20, 0)
                };
                p.Controls.Add(user);
            }

            p.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 4, BackColor = Accent });
            return p;
        }

        // ---------- карточка по центру ----------
        public static Panel MakeCard(int width)
        {
            return new Panel
            {
                BackColor = Surface,
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

        // ---------- строки в TableLayoutPanel ----------
        public static Label MakeCaption(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = Muted,
                Margin = new Padding(3, 10, 3, 2)
            };
        }

        /// <summary>Подпись над полем (одна колонка).</summary>
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

        /// <summary>Подпись слева, поле справа (две колонки).</summary>
        public static void AddRow(TableLayoutPanel t, string caption, Control c)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = true,
                ForeColor = Muted,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 10, 14, 10)
            };
            c.Dock = DockStyle.Fill;
            c.Margin = new Padding(3, 6, 3, 6);

            int r = t.RowCount;
            t.Controls.Add(lbl, 0, r);
            t.Controls.Add(c, 1, r);
            t.RowCount = r + 1;
        }

        // ---------- сообщения ----------
        public static void Info(string msg) { MessageBox.Show(msg, "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        public static void Warn(string msg) { MessageBox.Show(msg, "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        public static void Error(string msg) { MessageBox.Show(msg, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }

        // ---------- диалог выбора числа (вместо InputBox) ----------
        public static int? PromptNumber(IWin32Window owner, string title, string caption, int min, int max, int def)
        {
            using (var f = new Form())
            {
                f.Text = title;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MaximizeBox = false;
                f.MinimizeBox = false;
                f.ShowInTaskbar = false;
                f.ClientSize = new Size(360, 170);
                f.BackColor = Bg;
                f.ForeColor = Text;
                f.Font = new Font("Segoe UI", 10f);

                var lbl = new Label { Text = caption, Left = 20, Top = 20, Width = 320, Height = 24, ForeColor = Text };
                var num = new NumericUpDown { Left = 20, Top = 52, Width = 320, Minimum = min, Maximum = max, Value = def };
                StyleNumeric(num);

                var ok = MakeButton("Арендовать", BtnKind.Primary);
                ok.Left = 20; ok.Top = 104; ok.Width = 150;
                ok.DialogResult = DialogResult.OK;

                var cancel = MakeButton("Отмена", BtnKind.Secondary);
                cancel.Left = 190; cancel.Top = 104; cancel.Width = 150;
                cancel.DialogResult = DialogResult.Cancel;

                f.Controls.Add(lbl);
                f.Controls.Add(num);
                f.Controls.Add(ok);
                f.Controls.Add(cancel);
                f.AcceptButton = ok;
                f.CancelButton = cancel;

                return f.ShowDialog(owner) == DialogResult.OK ? (int?)num.Value : null;
            }
        }
    }

    /// <summary>Тёмный TabControl с акцентной линией под активной вкладкой.</summary>
    public class DarkTabControl : TabControl
    {
        public DarkTabControl()
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
