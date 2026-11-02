using Npgsql;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace GameAccountRental
{
    public class FormManager : Form
    {
        private const string ConnStr = "Host=localhost;Port=5432;Database=MyDataBase;Username=user1;Password=12345678";

        private Button btnRefresh, btnAccept, btnIssue, btnReject, btnLogout;
        private DataGridView dgv;

        public FormManager()
        {
            this.Font = new Font("Segoe UI", 10f);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 600);
            this.Text = "Панель менеджера";

            // Fill -> Bottom -> Top
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            Theme.StyleGrid(dgv);
            gridHost.Controls.Add(dgv);
            this.Controls.Add(gridHost);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(16, 10, 16, 16) };
            btnLogout = Theme.MakeButton("Выйти", BtnKind.Danger);
            btnLogout.Dock = DockStyle.Fill;
            btnLogout.Click += (s, e) => this.Close();
            bottom.Controls.Add(btnLogout);
            this.Controls.Add(bottom);

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 62,
                Padding = new Padding(13, 12, 13, 6)
            };
            btnRefresh = Theme.MakeButton("Обновить", BtnKind.Secondary);
            btnAccept = Theme.MakeButton("Принять", BtnKind.Primary);
            btnIssue = Theme.MakeButton("Выдать", BtnKind.Success);
            btnReject = Theme.MakeButton("Отклонить", BtnKind.Danger);
            btnRefresh.Click += (s, e) => LoadData();
            btnAccept.Click += (s, e) => ChangeStatus("принят", "создан");
            btnIssue.Click += (s, e) => ChangeStatus("выдан", "принят");
            btnReject.Click += BtnReject_Click;
            toolbar.Controls.Add(btnRefresh);
            toolbar.Controls.Add(btnAccept);
            toolbar.Controls.Add(btnIssue);
            toolbar.Controls.Add(btnReject);
            this.Controls.Add(toolbar);

            this.Controls.Add(Theme.MakeHeader("Панель менеджера", true));

            Theme.Apply(this);
            this.Load += (s, e) => LoadData();
        }

        private void LoadData()
        {
            try
            {
                const string sql =
                    "SELECT o.id, u.login AS \"Клиент\", a.login AS \"Аккаунт\", " +
                    "       o.created_at AS \"Дата создания\", " +
                    "       o.start_date AS \"Начало\", o.end_date AS \"Конец\", " +
                    "       o.total_price AS \"Сумма\", s.name AS \"Статус\" " +
                    "FROM orders o " +
                    "JOIN users u         ON u.id = o.user_id " +
                    "JOIN account a       ON a.id = o.account_id " +
                    "JOIN order_status s  ON s.id = o.order_status_id " +
                    "WHERE s.name IN ('создан', 'принят') " +
                    "ORDER BY CASE WHEN s.name = 'создан' THEN 0 ELSE 1 END, o.id";

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

                // Сортировка по клику на заголовок — для всех колонок
                foreach (DataGridViewColumn col in dgv.Columns)
                    col.SortMode = DataGridViewColumnSortMode.Automatic;

                if (dgv.Columns["Дата создания"] != null) dgv.Columns["Дата создания"].DefaultCellStyle.Format = "dd.MM.yyyy HH:mm";
                if (dgv.Columns["Начало"] != null) dgv.Columns["Начало"].DefaultCellStyle.Format = "dd.MM.yyyy";
                if (dgv.Columns["Конец"] != null) dgv.Columns["Конец"].DefaultCellStyle.Format = "dd.MM.yyyy";
                if (dgv.Columns["Сумма"] != null)
                {
                    dgv.Columns["Сумма"].DefaultCellStyle.Format = "N2";
                    dgv.Columns["Сумма"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
            }
            catch (Exception ex)
            {
                Theme.Error(ex.Message);
            }
        }

        // newStatus — новый статус заказа, requiredStatus — статус, из которого разрешён переход
        private void ChangeStatus(string newStatus, string requiredStatus)
        {
            if (dgv.CurrentRow == null)
            {
                Theme.Warn("Выберите заказ.");
                return;
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
                    Theme.Warn("Заказ должен иметь статус «" + requiredStatus + "».");

                LoadData();
            }
            catch (Exception ex)
            {
                Theme.Error(ex.Message);
            }
        }

        // ---------- «Отклонить»: заказ -> «отменён», аккаунт -> «свободен» ----------

        private static int? GetId(NpgsqlConnection conn, string sql, string name)
        {
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.Add(new NpgsqlParameter("@n", name));
                object res = cmd.ExecuteScalar();
                return res == null || res == DBNull.Value ? (int?)null : Convert.ToInt32(res);
            }
        }

        private void BtnReject_Click(object sender, EventArgs e)
        {
            if (dgv.CurrentRow == null)
            {
                Theme.Warn("Выберите заказ.");
                return;
            }

            if (MessageBox.Show("Отклонить выбранный заказ? Аккаунт снова станет свободным.", "Подтверждение",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                int orderId = Convert.ToInt32(dgv.CurrentRow.Cells["id"].Value);

                using (var conn = new NpgsqlConnection(ConnStr))
                {
                    conn.Open();

                    int? cancelledId = GetId(conn, "SELECT id FROM order_status WHERE name = @n", "отменён");
                    int? freeId = GetId(conn, "SELECT id FROM account_status WHERE name = @n", "свободен");
                    if (!cancelledId.HasValue)
                    {
                        Theme.Error("В справочнике статусов заказов нет значения «отменён».");
                        return;
                    }
                    if (!freeId.HasValue)
                    {
                        Theme.Error("В справочнике статусов аккаунтов нет значения «свободен».");
                        return;
                    }

                    using (var tx = conn.BeginTransaction())
                    {
                        object accountId;
                        using (var cmd = new NpgsqlCommand(
                            "UPDATE orders SET order_status_id = @cancel, manager_id = @mid " +
                            "WHERE id = @oid " +
                            "  AND order_status_id IN (SELECT id FROM order_status WHERE name IN ('создан', 'принят')) " +
                            "RETURNING account_id", conn, tx))
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@cancel", cancelledId.Value));
                            cmd.Parameters.Add(new NpgsqlParameter("@mid", Session.CurrentUser.Id));
                            cmd.Parameters.Add(new NpgsqlParameter("@oid", orderId));
                            accountId = cmd.ExecuteScalar();
                        }

                        if (accountId == null || accountId == DBNull.Value)
                        {
                            tx.Rollback();
                            Theme.Warn("Этот заказ уже обработан, отклонить его нельзя.");
                            LoadData();
                            return;
                        }

                        using (var cmd = new NpgsqlCommand(
                            "UPDATE account SET status_id = @free WHERE id = @aid", conn, tx))
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@free", freeId.Value));
                            cmd.Parameters.Add(new NpgsqlParameter("@aid", Convert.ToInt32(accountId)));
                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                    }
                }

                Theme.Info("Заказ отклонён, аккаунт снова свободен.");
                LoadData();
            }
            catch (Exception ex)
            {
                Theme.Error(ex.Message);
            }
        }
    }
}
