using System;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Npgsql;
using OfficeOpenXml; // EPPlus 4.5.3.3

namespace RentalApp
{
    public class FormAdmin : Form
    {
        private const string ConnStr = "Host=localhost;Port=5432;Database=MyDataBase;Username=user1;Password=12345678";

        private TabControl tabs;
        private DataGridView dgvGames, dgvAccounts;
        private Button btnAddGame, btnEditGame, btnDeleteGame;
        private Button btnAddAccount, btnEditAccount, btnDeleteAccount;
        private Button btnExportExcel, btnLogout;

        public FormAdmin()
        {
            this.Font = new Font("Segoe UI", 10f);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 600);
            this.Text = "Панель администратора";

            // Fill добавляем первым, затем элементы Bottom
            tabs = new TabControl { Dock = DockStyle.Fill };
            this.Controls.Add(tabs);

            btnLogout = MakeButton("Выйти");
            btnLogout.Dock = DockStyle.Bottom;
            btnLogout.Height = 42;
            btnLogout.Click += (s, e) => this.Close();
            this.Controls.Add(btnLogout);

            btnExportExcel = MakeButton("Экспорт заказов в Excel");
            btnExportExcel.Dock = DockStyle.Bottom;
            btnExportExcel.Height = 42;
            btnExportExcel.Click += BtnExportExcel_Click;
            this.Controls.Add(btnExportExcel);

            // Вкладка «Игры»
            var tabGames = new TabPage("Игры") { Padding = new Padding(8) };
            dgvGames = MakeGrid();
            tabGames.Controls.Add(dgvGames);
            btnAddGame = MakeButton("Добавить");
            btnEditGame = MakeButton("Изменить");
            btnDeleteGame = MakeButton("Удалить");
            btnAddGame.Click += (s, e) => OpenGameEditor(null);
            btnEditGame.Click += (s, e) =>
            {
                int? id = GetSelectedId(dgvGames);
                if (id.HasValue) OpenGameEditor(id);
            };
            btnDeleteGame.Click += BtnDeleteGame_Click;
            tabGames.Controls.Add(MakeButtonPanel(btnAddGame, btnEditGame, btnDeleteGame));
            tabs.TabPages.Add(tabGames);

            // Вкладка «Аккаунты»
            var tabAccounts = new TabPage("Аккаунты") { Padding = new Padding(8) };
            dgvAccounts = MakeGrid();
            tabAccounts.Controls.Add(dgvAccounts);
            btnAddAccount = MakeButton("Добавить");
            btnEditAccount = MakeButton("Изменить");
            btnDeleteAccount = MakeButton("Удалить");
            btnAddAccount.Click += (s, e) => OpenAccountEditor(null);
            btnEditAccount.Click += (s, e) =>
            {
                int? id = GetSelectedId(dgvAccounts);
                if (id.HasValue) OpenAccountEditor(id);
            };
            btnDeleteAccount.Click += BtnDeleteAccount_Click;
            tabAccounts.Controls.Add(MakeButtonPanel(btnAddAccount, btnEditAccount, btnDeleteAccount));
            tabs.TabPages.Add(tabAccounts);

            this.Load += (s, e) => { LoadGames(); LoadAccounts(); };
        }

        // ---------- helpers ----------

        private static Button MakeButton(string text)
        {
            return new Button
            {
                Text = text,
                Width = 140,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };
        }

        private static DataGridView MakeGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White
            };
        }

        private static FlowLayoutPanel MakeButtonPanel(params Button[] buttons)
        {
            var p = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(4) };
            p.Controls.AddRange(buttons);
            return p;
        }

        private static int? GetSelectedId(DataGridView dgv)
        {
            if (dgv.CurrentRow == null)
            {
                MessageBox.Show("Выберите запись в таблице.");
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

        // ---------- загрузка ----------

        private void LoadGames()
        {
            try
            {
                Bind(dgvGames, Query(
                    "SELECT g.id, g.title AS \"Название\", g.genre AS \"Жанр\", p.name AS \"Платформа\" " +
                    "FROM game g JOIN platform p ON p.id = g.platform_id ORDER BY g.id"));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

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
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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
                            cmd.Parameters.Add(new NpgsqlParameter("@id", id.Value)); cmd.ExecuteNonQuery();
                        }
                        tx.Commit();
                    }
                }
                LoadGames();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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
                MessageBox.Show("Нельзя удалить: на аккаунт ссылаются заказы.");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
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

                    // Для EPPlus 5+ раскомментируйте:
                    // ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

                    using (var pkg = new ExcelPackage())
                    {
                        var ws = pkg.Workbook.Worksheets.Add("Заказы");
                        ws.Cells["A1"].LoadFromDataTable(dt, true);
                        ws.Cells[1, 1, 1, dt.Columns.Count].Style.Font.Bold = true;
                        ws.Cells[ws.Dimension.Address].AutoFitColumns();
                        ws.Cells["D:E"].Style.Numberformat.Format = "dd.mm.yyyy";
                        pkg.SaveAs(new System.IO.FileInfo(dlg.FileName));
                    }

                    Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }
}