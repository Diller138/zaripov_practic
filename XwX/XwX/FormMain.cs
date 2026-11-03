using Microsoft.VisualBasic;
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
                LoadData();   // статус аккаунта мог измениться после аренды
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

                var restoreGrid = GridState.Capture(dgv);   // запоминаем скролл / выбор / сортировку
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

                restoreGrid();   // возвращаем скролл / выбор / сортировку
            }
            catch (Exception ex)
            {
                Theme.Error(ex.Message);
            }
        }

        private void BtnRent_Click(object sender, EventArgs e)
        {
            if (dgv.CurrentRow == null)
            {
                Theme.Warn("Выберите аккаунт.");
                return;
            }

            // Вся логика (проверка статуса, вопрос о сроке, транзакция, понятные ошибки) — в RentService
            RentService.TryRent(this, Convert.ToInt32(dgv.CurrentRow.Cells["id"].Value));

            LoadData();   // обновляем в любом случае: статус мог измениться
        }
    }
}
