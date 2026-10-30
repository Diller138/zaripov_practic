using GameAccountRental;
using Microsoft.VisualBasic; // Добавьте ссылку на сборку Microsoft.VisualBasic
using Npgsql;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace RentalApp
{
    public class FormMain : Form
    {
        private const string ConnStr = "Host=localhost;Port=5432;Database=MyDataBase;Username=user1;Password=12345678";

        private TextBox txtSearch;
        private Button btnSearch, btnRent;
        private DataGridView dgv;

        public FormMain()
        {
            this.Font = new Font("Segoe UI", 10f);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 600);
            this.Text = "Каталог аккаунтов";

            // Fill добавляем первым, затем Bottom и Top
            dgv = new DataGridView
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
            this.Controls.Add(dgv);

            btnRent = MakeButton("Арендовать");
            btnRent.Dock = DockStyle.Bottom;
            btnRent.Height = 45;
            btnRent.Click += BtnRent_Click;
            this.Controls.Add(btnRent);

            var top = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(8) };
            txtSearch = new TextBox { Left = 8, Top = 12, Width = 350 };
            btnSearch = MakeButton("Поиск");
            btnSearch.Left = 368;
            btnSearch.Top = 10;
            btnSearch.Width = 100;
            btnSearch.Height = 30;
            btnSearch.Click += (s, e) => LoadData();
            top.Controls.Add(txtSearch);
            top.Controls.Add(btnSearch);
            this.Controls.Add(top);
            this.AcceptButton = btnSearch;

            this.Load += (s, e) => LoadData();
        }

        private static Button MakeButton(string text)
        {
            return new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };
        }

        private void LoadData()
        {
            try
            {
                const string sql =
                    "SELECT a.id, a.login AS \"Аккаунт\", p.name AS \"Платформа\", " +
                    "       COALESCE(string_agg(DISTINCT g.name, ', '), '') AS \"Игры\", " +
                    "       a.price_per_day AS \"Цена/сутки\", s.name AS \"Статус\" " +
                    "FROM account a " +
                    "JOIN platform p        ON p.id = a.platform_id " +
                    "JOIN account_status s  ON s.id = a.status_id " +
                    "LEFT JOIN account_game ag ON ag.account_id = a.id " +
                    "LEFT JOIN game g       ON g.id = ag.game_id " +
                    "WHERE @q = '' OR a.login ILIKE '%' || @q || '%' " +
                    "   OR p.name ILIKE '%' || @q || '%' " +
                    "   OR EXISTS (SELECT 1 FROM account_game ag2 JOIN game g2 ON g2.id = ag2.game_id " +
                    "              WHERE ag2.account_id = a.id AND g2.name ILIKE '%' || @q || '%') " +
                    "GROUP BY a.id, a.login, p.name, a.price_per_day, s.name " +
                    "ORDER BY a.id";

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
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void BtnRent_Click(object sender, EventArgs e)
        {
            if (dgv.CurrentRow == null)
            {
                MessageBox.Show("Выберите аккаунт.");
                return;
            }

            try
            {
                int accountId = Convert.ToInt32(dgv.CurrentRow.Cells["id"].Value);

                using (var conn = new NpgsqlConnection(ConnStr))
                {
                    conn.Open();

                    // Актуальный статус из БД
                    string status;
                    using (var cmd = new NpgsqlCommand(
                        "SELECT s.name FROM account a JOIN account_status s ON s.id = a.status_id WHERE a.id = @aid", conn))
                    {
                        cmd.Parameters.Add(new NpgsqlParameter("@aid", accountId));
                        status = Convert.ToString(cmd.ExecuteScalar());
                    }

                    if (!string.Equals(status, "свободен", StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("Аккаунт недоступен для аренды (статус: " + status + ").");
                        return;
                    }

                    string input = Interaction.InputBox("На сколько дней арендовать?", "Аренда", "1");
                    if (string.IsNullOrWhiteSpace(input)) return; // отмена

                    int days;
                    if (!int.TryParse(input, out days) || days <= 0)
                    {
                        MessageBox.Show("Введите целое число дней больше нуля.");
                        return;
                    }

                    using (var cmd = new NpgsqlCommand(
                        "INSERT INTO orders (user_id, account_id, start_date, end_date, total_price, order_status_id) " +
                        "VALUES (@uid, @aid, CURRENT_DATE, CURRENT_DATE + @days, 0, 1)", conn))
                    {
                        cmd.Parameters.Add(new NpgsqlParameter("@uid", Session.CurrentUser.Id));
                        cmd.Parameters.Add(new NpgsqlParameter("@aid", accountId));
                        cmd.Parameters.Add(new NpgsqlParameter("@days", days));
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Заявка на аренду создана.");
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}