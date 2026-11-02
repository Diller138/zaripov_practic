using Npgsql;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace GameAccountRental
{
    public class FormMain : Form
    {
        private const string ConnStr = "Host=localhost;Port=5432;Database=MyDataBase;Username=user1;Password=12345678";

        private TextBox txtSearch;
        private Button btnSearch, btnRent, btnLogout;
        private DataGridView dgv;

        public FormMain()
        {
            this.Font = new Font("Segoe UI", 10f);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 600);
            this.Text = "Каталог аккаунтов";

            // Порядок добавления: Fill -> Bottom -> Top (последний Top окажется выше всех)
            var gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 4, 16, 4) };
            dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ShowCellToolTips = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            Theme.StyleGrid(dgv);

            // Двойной клик — подробная информация об аккаунте
            dgv.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                OpenDetails(Convert.ToInt32(dgv.Rows[e.RowIndex].Cells["id"].Value));
            };

            // Полный список игр во всплывающей подсказке (колонка «Игры» обрезается многоточием)
            dgv.CellToolTipTextNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (dgv.Columns[e.ColumnIndex].HeaderText != "Игры") return;
                string full = Convert.ToString(dgv[e.ColumnIndex, e.RowIndex].Value);
                if (!string.IsNullOrEmpty(full))
                    e.ToolTipText = full.Replace(", ", Environment.NewLine);
            };

            gridHost.Controls.Add(dgv);
            this.Controls.Add(gridHost);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(16, 10, 16, 16) };
            btnRent = Theme.MakeButton("Арендовать выбранный аккаунт", BtnKind.Primary);
            btnRent.Dock = DockStyle.Fill;
            btnRent.Click += BtnRent_Click;

            btnLogout = Theme.MakeButton("Выйти", BtnKind.Danger);
            btnLogout.Dock = DockStyle.Right;
            btnLogout.Width = 160;
            btnLogout.Click += (s, e) => this.Close();

            // Порядок: Fill -> разделитель -> Right (последний докуется первым)
            bottom.Controls.Add(btnRent);
            bottom.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 12 });
            bottom.Controls.Add(btnLogout);
            this.Controls.Add(bottom);

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 64 };
            txtSearch = new TextBox { Left = 16, Top = 18, Width = 400 };
            Theme.StyleInput(txtSearch);
            Theme.SetPlaceholder(txtSearch, "Поиск по аккаунту...");
            btnSearch = Theme.MakeButton("Найти", BtnKind.Secondary);
            btnSearch.Left = 428;
            btnSearch.Top = 17;
            btnSearch.Width = 110;
            btnSearch.Height = 30;
            btnSearch.Click += (s, e) => LoadData();
            toolbar.Controls.Add(txtSearch);
            toolbar.Controls.Add(btnSearch);
            this.Controls.Add(toolbar);

            this.Controls.Add(Theme.MakeHeader("Каталог аккаунтов", true));
            this.AcceptButton = btnSearch;

            Theme.Apply(this);
            this.Load += (s, e) => LoadData();
        }

        private void OpenDetails(int accountId)
        {
            try
            {
                using (var f = new FormAccountDetails(accountId))
                    f.ShowDialog(this);
            }
            catch (Exception ex)
            {
                Theme.Error(ex.Message);
            }
        }

        private void LoadData()
        {
            try
            {
                const string sql =
                    "SELECT a.id, a.login AS \"Аккаунт\", p.name AS \"Платформа\", " +
                    "       COALESCE(string_agg(DISTINCT g.title, ', '), '') AS \"Игры\", " +
                    "       a.price_per_day AS \"Цена/сутки\", s.name AS \"Статус\" " +
                    "FROM account a " +
                    "JOIN platform p        ON p.id = a.platform_id " +
                    "JOIN account_status s  ON s.id = a.status_id " +
                    "LEFT JOIN account_game ag ON ag.account_id = a.id " +
                    "LEFT JOIN game g       ON g.id = ag.game_id " +
                    "WHERE @q = '' OR a.login ILIKE '%' || @q || '%' " +
                    "   OR p.name ILIKE '%' || @q || '%' " +
                    "   OR EXISTS (SELECT 1 FROM account_game ag2 JOIN game g2 ON g2.id = ag2.game_id " +
                    "              WHERE ag2.account_id = a.id AND g2.title ILIKE '%' || @q || '%') " +
                    "GROUP BY a.id, a.login, p.name, a.price_per_day, s.name " +
                    "ORDER BY CASE WHEN lower(s.name) = 'свободен' THEN 0 ELSE 1 END, a.id";

                var dt = new DataTable();
                using (var conn = new NpgsqlConnection(ConnStr))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("@q", txtSearch.Text.Trim()));
                    conn.Open();
                    using (var da = new NpgsqlDataAdapter(cmd))
                        da.Fill(dt);
                }

                dgv.DataSource = dt;
                if (dgv.Columns["id"] != null)
                    dgv.Columns["id"].Visible = false;
                foreach (DataGridViewColumn col in dgv.Columns)
                    col.SortMode = DataGridViewColumnSortMode.Automatic;

                var price = dgv.Columns["Цена/сутки"];
                if (price != null)
                {
                    price.DefaultCellStyle.Format = "N2";
                    price.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
            }
            catch (Exception ex)
            {
                Theme.Error(ex.Message);
            }
        }

        private static string GetStatus(NpgsqlConnection conn, int accountId)
        {
            using (var cmd = new NpgsqlCommand(
                "SELECT s.name FROM account a JOIN account_status s ON s.id = a.status_id WHERE a.id = @aid", conn))
            {
                cmd.Parameters.Add(new NpgsqlParameter("@aid", accountId));
                return Convert.ToString(cmd.ExecuteScalar());
            }
        }

        private static int? GetStatusId(NpgsqlConnection conn, string name)
        {
            using (var cmd = new NpgsqlCommand("SELECT id FROM account_status WHERE name = @n", conn))
            {
                cmd.Parameters.Add(new NpgsqlParameter("@n", name));
                object res = cmd.ExecuteScalar();
                return res == null || res == DBNull.Value ? (int?)null : Convert.ToInt32(res);
            }
        }

        private void BtnRent_Click(object sender, EventArgs e)
        {
            if (dgv.CurrentRow == null)
            {
                Theme.Warn("Выберите аккаунт.");
                return;
            }

            try
            {
                int accountId = Convert.ToInt32(dgv.CurrentRow.Cells["id"].Value);

                using (var conn = new NpgsqlConnection(ConnStr))
                {
                    conn.Open();

                    // Быстрая проверка до вопроса о сроке
                    string status = GetStatus(conn, accountId);
                    if (!string.Equals(status, "свободен", StringComparison.OrdinalIgnoreCase))
                    {
                        Theme.Warn("Этот аккаунт уже занят. Выберите другой — свободные аккаунты показаны в начале списка.");
                        LoadData();
                        return;
                    }

                    int? freeId = GetStatusId(conn, "свободен");
                    int? busyId = GetStatusId(conn, "арендован");
                    if (!freeId.HasValue || !busyId.HasValue)
                    {
                        Theme.Error("В справочнике статусов аккаунтов должны быть значения «свободен» и «арендован».");
                        return;
                    }

                    int? days = Theme.PromptNumber(this, "Аренда", "На сколько дней арендовать?", 1, 365, 1);
                    if (!days.HasValue) return; // отмена

                    using (var tx = conn.BeginTransaction())
                    {
                        // Атомарно «занимаем» аккаунт: сработает, только если он всё ещё свободен
                        int updated;
                        using (var cmd = new NpgsqlCommand(
                            "UPDATE account SET status_id = @busy WHERE id = @aid AND status_id = @free", conn, tx))
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@busy", busyId.Value));
                            cmd.Parameters.Add(new NpgsqlParameter("@free", freeId.Value));
                            cmd.Parameters.Add(new NpgsqlParameter("@aid", accountId));
                            updated = cmd.ExecuteNonQuery();
                        }

                        if (updated == 0)
                        {
                            tx.Rollback();
                            Theme.Warn("Пока вы выбирали срок, этот аккаунт уже арендовал другой пользователь. Выберите другой.");
                            LoadData();
                            return;
                        }

                        using (var cmd = new NpgsqlCommand(
                            "INSERT INTO orders (user_id, account_id, start_date, end_date, total_price, order_status_id) " +
                            "VALUES (@uid, @aid, CURRENT_DATE, CURRENT_DATE + @days, 0, 1)", conn, tx))
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@uid", Session.CurrentUser.Id));
                            cmd.Parameters.Add(new NpgsqlParameter("@aid", accountId));
                            cmd.Parameters.Add(new NpgsqlParameter("@days", days.Value));
                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                    }
                }

                Theme.Info("Заявка на аренду создана. Аккаунт закреплён за вами до подтверждения менеджером.");
                LoadData();
            }
            catch (PostgresException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                if (ex.SqlState == "23505" || ex.SqlState == "23P01" || ex.SqlState == "23514" || ex.SqlState == "P0001")
                    Theme.Warn("Этот аккаунт уже арендован или занят на выбранные даты. Пожалуйста, выберите другой аккаунт.");
                else
                    Theme.Error("Не удалось оформить аренду. Попробуйте ещё раз или обратитесь к администратору.");
                LoadData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                Theme.Error("Не удалось оформить аренду. Проверьте подключение к базе данных и попробуйте ещё раз.");
            }
        }
    }
}
