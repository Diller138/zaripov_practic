using GameAccountRental;
using Npgsql;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace RentalApp
{
    public class FormManager : Form
    {
        private const string ConnStr = "Host=localhost;Port=5432;Database=MyDataBase;Username=user1;Password=12345678";

        private Button btnRefresh, btnAccept, btnIssue, btnLogout;
        private DataGridView dgv;

        public FormManager()
        {
            this.Font = new Font("Segoe UI", 10f);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 600);
            this.Text = "Панель менеджера";

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

            btnLogout = MakeButton("Выйти");
            btnLogout.Dock = DockStyle.Bottom;
            btnLogout.Height = 45;
            btnLogout.Click += (s, e) => this.Close();
            this.Controls.Add(btnLogout);

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 55, Padding = new Padding(8) };
            btnRefresh = MakeButton("Обновить");
            btnAccept = MakeButton("Принять");
            btnIssue = MakeButton("Выдать");
            btnRefresh.Click += (s, e) => LoadData();
            btnAccept.Click += (s, e) => ChangeStatus("принят", "создан");
            btnIssue.Click += (s, e) => ChangeStatus("выдан", "принят");
            top.Controls.Add(btnRefresh);
            top.Controls.Add(btnAccept);
            top.Controls.Add(btnIssue);
            this.Controls.Add(top);

            this.Load += (s, e) => LoadData();
        }

        private static Button MakeButton(string text)
        {
            return new Button
            {
                Text = text,
                Width = 130,
                Height = 36,
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
                    "SELECT o.id, u.login AS \"Клиент\", a.title AS \"Аккаунт\", " +
                    "       o.start_date AS \"Начало\", o.end_date AS \"Конец\", " +
                    "       o.total_price AS \"Сумма\", s.name AS \"Статус\" " +
                    "FROM orders o " +
                    "JOIN users u         ON u.id = o.user_id " +
                    "JOIN account a       ON a.id = o.account_id " +
                    "JOIN order_status s  ON s.id = o.order_status_id " +
                    "WHERE s.name IN ('создан', 'принят') " +
                    "ORDER BY o.id";

                var dt = new DataTable();
                using (var conn = new NpgsqlConnection(ConnStr))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    conn.Open();
                    using (var da = new NpgsqlDataAdapter(cmd))
                        da.Fill(dt);
                }

                dgv.DataSource = dt;
                if (dgv.Columns["id"] != null)
                    dgv.Columns["id"].Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // newStatus — новый статус заказа, requiredStatus — статус, из которого разрешён переход
        private void ChangeStatus(string newStatus, string requiredStatus)
        {
            if (dgv.CurrentRow == null)
            {
                MessageBox.Show("Выберите заказ."); return;
            }

            try
            {
                int orderId = Convert.ToInt32(dgv.CurrentRow.Cells["id"].Value);
                int rows;

                using (var conn = new NpgsqlConnection(ConnStr))
                using (var cmd = new NpgsqlCommand(
                    "UPDATE orders SET " +
                    "  order_status_id = (SELECT id FROM order_status WHERE name = @new), " +
                    "  manager_id = @mid " +
                    "WHERE id = @oid " +
                    "  AND order_status_id = (SELECT id FROM order_status WHERE name = @req)", conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("@new", newStatus));
                    cmd.Parameters.Add(new NpgsqlParameter("@req", requiredStatus));
                    cmd.Parameters.Add(new NpgsqlParameter("@mid", Session.CurrentUser.Id));
                    cmd.Parameters.Add(new NpgsqlParameter("@oid", orderId));
                    conn.Open();
                    rows = cmd.ExecuteNonQuery();
                }

                if (rows == 0)
                    MessageBox.Show("Заказ должен иметь статус «" + requiredStatus + "».");

                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}