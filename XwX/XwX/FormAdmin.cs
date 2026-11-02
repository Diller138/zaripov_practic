using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using Npgsql;
using OfficeOpenXml; // EPPlus 4.5.3.3

namespace GameAccountRental
{
    public class FormAdmin : Form
    {
        private const string ConnStr = "Host=localhost;Port=5432;Database=MyDataBase;Username=user1;Password=12345678";

        private const string UsersSql =
            "SELECT u.id, u.login AS \"Логин\", u.email AS \"Email\", r.name AS \"Роль\", " +
            "       CASE WHEN u.is_active THEN 'Активен' ELSE 'Заблокирован' END AS \"Статус\" " +
            "FROM users u LEFT JOIN role r ON r.id = u.role_id ORDER BY u.id";

        private ThemedTabControl tabs;
        private DataGridView dgvGames, dgvAccounts, dgvUsers;
        private Button btnAddGame, btnEditGame, btnDeleteGame;
        private Button btnAddAccount, btnEditAccount, btnDeleteAccount;
        private Button btnToggleUser;
        private Button btnExportExcel, btnLogout;
        private CheckBox chkDark;

        private int _gamesVersion;   // чтобы устаревшие загрузки миниатюр не писали в новую таблицу

        public FormAdmin()
        {
            this.Font = new Font("Segoe UI", 10f);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 600);
            this.Text = "Панель администратора";

            // Fill -> Bottom -> Top
            tabs = new ThemedTabControl { Dock = DockStyle.Fill };
            this.Controls.Add(tabs);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(16, 10, 16, 16) };

            chkDark = new CheckBox
            {
                Text = "Темная тема",
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0),
                Cursor = Cursors.Hand,
                Checked = Theme.IsDarkMode
            };
            chkDark.CheckedChanged += (s, e) => Theme.SetDark(chkDark.Checked);

            btnExportExcel = Theme.MakeButton("Экспорт заказов в Excel", BtnKind.Success);
            btnExportExcel.Dock = DockStyle.Left;
            btnExportExcel.Width = 260;
            btnExportExcel.Click += BtnExportExcel_Click;

            btnLogout = Theme.MakeButton("Выйти", BtnKind.Danger);
            btnLogout.Dock = DockStyle.Right;
            btnLogout.Width = 160;
            btnLogout.Click += (s, e) => this.Close();

            bottom.Controls.Add(chkDark);
            bottom.Controls.Add(btnExportExcel);
            bottom.Controls.Add(btnLogout);
            this.Controls.Add(bottom);

            this.Controls.Add(Theme.MakeHeader("Панель администратора", true));

            // ---------- Вкладка «Игры» ----------
            var tabGames = MakeTab("Игры");
            dgvGames = MakeGrid();
            dgvGames.RowTemplate.Height = 56;                       // место под миниатюру
            dgvGames.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                    OpenGameEditor(Convert.ToInt32(dgvGames.Rows[e.RowIndex].Cells["id"].Value));
            };
            tabGames.Controls.Add(dgvGames);
            btnAddGame = Theme.MakeButton("Добавить", BtnKind.Primary);
            btnEditGame = Theme.MakeButton("Изменить", BtnKind.Secondary);
            btnDeleteGame = Theme.MakeButton("Удалить", BtnKind.Danger);
            btnAddGame.Click += (s, e) => OpenGameEditor(null);
            btnEditGame.Click += (s, e) =>
            {
                int? id = GetSelectedId(dgvGames);
                if (id.HasValue) OpenGameEditor(id);
            };
            btnDeleteGame.Click += BtnDeleteGame_Click;
            tabGames.Controls.Add(MakeButtonPanel(btnAddGame, btnEditGame, btnDeleteGame));
            tabs.TabPages.Add(tabGames);

            // ---------- Вкладка «Аккаунты» ----------
            var tabAccounts = MakeTab("Аккаунты");
            dgvAccounts = MakeGrid();
            dgvAccounts.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                    OpenAccountEditor(Convert.ToInt32(dgvAccounts.Rows[e.RowIndex].Cells["id"].Value));
            };
            tabAccounts.Controls.Add(dgvAccounts);
            btnAddAccount = Theme.MakeButton("Добавить", BtnKind.Primary);
            btnEditAccount = Theme.MakeButton("Изменить", BtnKind.Secondary);
            btnDeleteAccount = Theme.MakeButton("Удалить", BtnKind.Danger);
            btnAddAccount.Click += (s, e) => OpenAccountEditor(null);
            btnEditAccount.Click += (s, e) =>
            {
                int? id = GetSelectedId(dgvAccounts);
                if (id.HasValue) OpenAccountEditor(id);
            };
            btnDeleteAccount.Click += BtnDeleteAccount_Click;
            tabAccounts.Controls.Add(MakeButtonPanel(btnAddAccount, btnEditAccount, btnDeleteAccount));
            tabs.TabPages.Add(tabAccounts);

            // ---------- Вкладка «Пользователи» ----------
            var tabUsers = MakeTab("Пользователи");
            dgvUsers = MakeGrid();
            dgvUsers.SelectionChanged += (s, e) => UpdateToggleButton();
            tabUsers.Controls.Add(dgvUsers);
            btnToggleUser = Theme.MakeButton("Заблокировать", BtnKind.Danger);
            btnToggleUser.Width = 200;
            btnToggleUser.Click += BtnToggleUser_Click;
            tabUsers.Controls.Add(MakeButtonPanel(btnToggleUser));
            tabs.TabPages.Add(tabUsers);

            Theme.Apply(this);
            this.Load += (s, e) => { LoadGames(); LoadAccounts(); LoadUsers(); };
        }

        // ---------- helpers ----------

        private static TabPage MakeTab(string title)
        {
            return new TabPage(title)
            {
                UseVisualStyleBackColor = false,
                BackColor = Theme.Bg,
                ForeColor = Theme.Text,
                Padding = new Padding(16, 8, 16, 8)
            };
        }

        private static DataGridView MakeGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            Theme.StyleGrid(g);
            return g;
        }

        private static FlowLayoutPanel MakeButtonPanel(params Button[] buttons)
        {
            var p = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 56,
                Padding = new Padding(0, 6, 0, 6)
            };
            p.Controls.AddRange(buttons);
            return p;
        }

        private static int? GetSelectedId(DataGridView dgv)
        {
            if (dgv.CurrentRow == null)
            {
                Theme.Warn("Выберите запись в таблице.");
                return null;
            }
            return Convert.ToInt32(dgv.CurrentRow.Cells["id"].Value);
        }

        private static DataTable Query(string sql)
        {
            var dt = new DataTable();
            using (var conn = new NpgsqlConnection(ConnStr))
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                conn.Open();
                using (var da = new NpgsqlDataAdapter(cmd))
                    da.Fill(dt);
            }
            return dt;
        }

        private static void Bind(DataGridView dgv, DataTable dt)
        {
            dgv.DataSource = dt;
            if (dgv.Columns["id"] != null)
                dgv.Columns["id"].Visible = false;
        }

        // ---------- загрузка: игры (с миниатюрами) ----------

        private static readonly Dictionary<string, Image> ThumbCache = new Dictionary<string, Image>();

        private static async Task<Image> GetThumbAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return Theme.Placeholder(96, 54);

            Image cached;
            if (ThumbCache.TryGetValue(url, out cached)) return cached;

            try
            {
                using (var wc = new WebClient())
                {
                    byte[] data = await wc.DownloadDataTaskAsync(url);
                    using (var ms = new MemoryStream(data))
                    using (var src = Image.FromStream(ms))
                    {
                        var thumb = new Bitmap(src, new Size(96, 54));
                        ThumbCache[url] = thumb;
                        return thumb;
                    }
                }
            }
            catch
            {
                return Theme.Placeholder(96, 54);
            }
        }

        private void LoadGames()
        {
            try
            {
                DataTable dt = Query(
                    "SELECT g.id, g.title AS \"Название\", g.genre AS \"Жанр\", p.name AS \"Платформа\", g.cover_url " +
                    "FROM game g JOIN platform p ON p.id = g.platform_id ORDER BY g.id");
                Bind(dgvGames, dt);

                if (dgvGames.Columns["cover_url"] != null)
                    dgvGames.Columns["cover_url"].Visible = false;

                if (dgvGames.Columns["Обложка"] == null)
                {
                    dgvGames.Columns.Add(new DataGridViewImageColumn
                    {
                        Name = "Обложка",
                        HeaderText = "Обложка",
                        ImageLayout = DataGridViewImageCellLayout.Zoom,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                        Width = 110,
                        SortMode = DataGridViewColumnSortMode.NotSortable,
                        ReadOnly = true
                    });
                }
                dgvGames.Columns["Обложка"].DisplayIndex = 0;

                FillCovers(++_gamesVersion);
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
        }

        private async void FillCovers(int version)
        {
            try
            {
                List<DataGridViewRow> rows = dgvGames.Rows.Cast<DataGridViewRow>().ToList();

                // Сначала ставим заглушки, затем качаем обложки параллельно
                foreach (var r in rows)
                    r.Cells["Обложка"].Value = Theme.Placeholder(96, 54);

                List<Task<Image>> tasks = rows
                    .Select(r => GetThumbAsync(Convert.ToString(r.Cells["cover_url"].Value)))
                    .ToList();

                for (int i = 0; i < rows.Count; i++)
                {
                    Image img = await tasks[i];
                    if (version != _gamesVersion || rows[i].DataGridView == null) return;
                    rows[i].Cells["Обложка"].Value = img;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);   // обложки — не критично
            }
        }

        // ---------- загрузка: аккаунты и пользователи ----------

        private void LoadAccounts()
        {
            try
            {
                Bind(dgvAccounts, Query(
                    "SELECT a.id, a.login AS \"Логин\", a.price_per_day AS \"Цена/сутки\", " +
                    "       p.name AS \"Платформа\", s.name AS \"Статус\" " +
                    "FROM account a " +
                    "JOIN platform p       ON p.id = a.platform_id " +
                    "JOIN account_status s ON s.id = a.status_id ORDER BY a.id"));

                var price = dgvAccounts.Columns["Цена/сутки"];
                if (price != null)
                {
                    price.DefaultCellStyle.Format = "N2";
                    price.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
        }

        private void LoadUsers()
        {
            try
            {
                Bind(dgvUsers, Query(UsersSql));
                UpdateToggleButton();
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
        }

        private void UpdateToggleButton()
        {
            if (btnToggleUser == null || dgvUsers.CurrentRow == null || dgvUsers.Columns["Статус"] == null) return;
            bool active = Convert.ToString(dgvUsers.CurrentRow.Cells["Статус"].Value) == "Активен";
            btnToggleUser.Text = active ? "Заблокировать" : "Разблокировать";
        }

        // ---------- формы редактирования ----------

        private void OpenGameEditor(int? id)
        {
            using (var f = new FormEditGame(id))
            {
                if (f.ShowDialog(this) == DialogResult.OK) LoadGames();
            }
        }

        private void OpenAccountEditor(int? id)
        {
            using (var f = new FormEditAccount(id))
            {
                if (f.ShowDialog(this) == DialogResult.OK) LoadAccounts();
            }
        }

        // ---------- блокировка пользователей ----------

        private void BtnToggleUser_Click(object sender, EventArgs e)
        {
            int? id = GetSelectedId(dgvUsers);
            if (!id.HasValue) return;

            if (Session.CurrentUser != null && id.Value == Convert.ToInt32(Session.CurrentUser.Id))
            {
                Theme.Warn("Нельзя заблокировать самого себя.");
                return;
            }

            string login = Convert.ToString(dgvUsers.CurrentRow.Cells["Логин"].Value);
            bool isActive = Convert.ToString(dgvUsers.CurrentRow.Cells["Статус"].Value) == "Активен";
            string action = isActive ? "заблокировать" : "разблокировать";

            if (MessageBox.Show("Вы действительно хотите " + action + " пользователя «" + login + "»?",
                    "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                using (var conn = new NpgsqlConnection(ConnStr))
                using (var cmd = new NpgsqlCommand("UPDATE users SET is_active = @active WHERE id = @id", conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("@active", !isActive));
                    cmd.Parameters.Add(new NpgsqlParameter("@id", id.Value));
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                LoadUsers();
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
        }

        // ---------- удаление ----------

        private void BtnDeleteGame_Click(object sender, EventArgs e)
        {
            int? id = GetSelectedId(dgvGames);
            if (!id.HasValue) return;
            if (MessageBox.Show("Удалить выбранную игру?", "Подтверждение",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                using (var conn = new NpgsqlConnection(ConnStr))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        using (var cmd = new NpgsqlCommand("DELETE FROM account_game WHERE game_id = @id", conn, tx))
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@id", id.Value));
                            cmd.ExecuteNonQuery();
                        }
                        using (var cmd = new NpgsqlCommand("DELETE FROM game WHERE id = @id", conn, tx))
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@id", id.Value));
                            cmd.ExecuteNonQuery();
                        }
                        tx.Commit();
                    }
                }
                LoadGames();
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
        }

        private void BtnDeleteAccount_Click(object sender, EventArgs e)
        {
            int? id = GetSelectedId(dgvAccounts);
            if (!id.HasValue) return;
            if (MessageBox.Show("Удалить выбранный аккаунт?", "Подтверждение",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                using (var conn = new NpgsqlConnection(ConnStr))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        using (var cmd = new NpgsqlCommand("DELETE FROM account_game WHERE account_id = @id", conn, tx))
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@id", id.Value));
                            cmd.ExecuteNonQuery();
                        }
                        using (var cmd = new NpgsqlCommand("DELETE FROM account WHERE id = @id", conn, tx))
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@id", id.Value));
                            cmd.ExecuteNonQuery();
                        }
                        tx.Commit();
                    }
                }
                LoadAccounts();
            }
            catch (PostgresException ex) when (ex.SqlState == "23503")
            {
                Theme.Error("Нельзя удалить: на аккаунт ссылаются заказы.");
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
        }

        // ---------- экспорт ----------

        private void BtnExportExcel_Click(object sender, EventArgs e)
        {
            try
            {
                DataTable dt = Query(
                    "SELECT o.id AS \"ID\", u.login AS \"Клиент\", a.login AS \"Аккаунт\", " +
                    "       o.start_date AS \"Начало\", o.end_date AS \"Конец\", " +
                    "       o.total_price AS \"Сумма\", s.name AS \"Статус\", m.login AS \"Менеджер\" " +
                    "FROM orders o " +
                    "JOIN users u        ON u.id = o.user_id " +
                    "JOIN account a      ON a.id = o.account_id " +
                    "JOIN order_status s ON s.id = o.order_status_id " +
                    "LEFT JOIN users m   ON m.id = o.manager_id " +
                    "ORDER BY o.id");

                using (var dlg = new SaveFileDialog
                {
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = "orders_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx"
                })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;

                    // Указываем, что используем EPPlus для личных/учебных целей
                    ExcelPackage.License.SetNonCommercialPersonal("Student");

                    using (var pkg = new ExcelPackage())
                    {
                        var ws = pkg.Workbook.Worksheets.Add("Заказы");
                        ws.Cells["A1"].LoadFromDataTable(dt, true);
                        ws.Cells[1, 1, 1, dt.Columns.Count].Style.Font.Bold = true;
                        ws.Cells[ws.Dimension.Address].AutoFitColumns();
                        ws.Cells["D:E"].Style.Numberformat.Format = "dd.mm.yyyy";
                        pkg.SaveAs(new FileInfo(dlg.FileName));
                    }

                    Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
        }
    }
}
