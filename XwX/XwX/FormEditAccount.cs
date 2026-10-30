using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Npgsql;

namespace RentalApp
{
    public class FormEditAccount : Form
    {
        private const string ConnStr = "Host=localhost;Port=5432;Database=MyDataBase;Username=user1;Password=12345678";

        private TextBox txtId, txtAccLogin, txtAccPassword;
        private NumericUpDown numPrice;
        private ComboBox cmbStatus, cmbPlatform;
        private CheckedListBox checkedListGames;
        private Button btnSave;

        private DataTable allGames;                        // id, name, platform_id
        private readonly HashSet<int> checkedIds = new HashSet<int>();
        private bool populating;

        private class GameItem
        {
            public int Id;
            public string Name;
            public override string ToString() { return Name; }
        }

        public FormEditAccount(int? accountId = null)
        {
            this.Font = new Font("Segoe UI", 10f);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 600);
            this.Padding = new Padding(20);
            this.Text = accountId.HasValue ? "Редактирование аккаунта" : "Новый аккаунт";

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Padding = new Padding(10)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            txtId = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            txtAccLogin = new TextBox { Dock = DockStyle.Fill };
            txtAccPassword = new TextBox { Dock = DockStyle.Fill };
            numPrice = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                DecimalPlaces = 2,
                Minimum = 0,
                Maximum = 1000000,
                Increment = 10
            };
            cmbStatus = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPlatform = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            checkedListGames = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true,
                IntegralHeight = false
            };
            checkedListGames.ItemCheck += CheckedListGames_ItemCheck;

            AddRow(table, "ID:", txtId);
            AddRow(table, "Логин аккаунта:", txtAccLogin);
            AddRow(table, "Пароль аккаунта:", txtAccPassword);
            AddRow(table, "Цена за сутки:", numPrice);
            AddRow(table, "Статус:", cmbStatus);
            AddRow(table, "Платформа:", cmbPlatform);

            // Список игр занимает всё оставшееся место
            var lblGames = new Label
            {
                Text = "Игры:",
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Margin = new Padding(3, 10, 10, 10)
            };
            checkedListGames.Margin = new Padding(3, 8, 3, 8);
            table.RowStyles.Clear();
            for (int i = 0; i < table.RowCount; i++)
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(lblGames, 0, table.RowCount);
            table.Controls.Add(checkedListGames, 1, table.RowCount);
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            table.RowCount++;

            btnSave = new Button
            {
                Text = "Сохранить",
                Height = 42,
                Dock = DockStyle.Bottom,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };
            btnSave.Click += BtnSave_Click; this.Controls.Add(table);
            this.Controls.Add(btnSave);

            LoadLookups();
            if (accountId.HasValue) LoadAccount(accountId.Value);
            cmbPlatform.SelectedIndexChanged += (s, e) => RefreshGames();
            RefreshGames();
        }

        private static void AddRow(TableLayoutPanel t, string caption, Control c)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 10, 10, 10)
            };
            c.Margin = new Padding(3, 8, 3, 8);
            t.Controls.Add(lbl, 0, t.RowCount);
            t.Controls.Add(c, 1, t.RowCount);
            t.RowCount++;
        }

        private static DataTable Query(string sql)
        {
            var dt = new DataTable();
            using (var conn = new NpgsqlConnection(ConnStr))
            using (var da = new NpgsqlDataAdapter(sql, conn))
                da.Fill(dt);
            return dt;
        }

        // ---------- справочники ----------

        private void LoadLookups()
        {
            try
            {
                cmbStatus.DataSource = Query("SELECT id, name FROM account_status ORDER BY id");
                cmbStatus.DisplayMember = "name";
                cmbStatus.ValueMember = "id";

                cmbPlatform.DataSource = Query("SELECT id, name FROM platform ORDER BY name");
                cmbPlatform.DisplayMember = "name";
                cmbPlatform.ValueMember = "id";

                allGames = Query("SELECT id, name, platform_id FROM game ORDER BY name");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void LoadAccount(int id)
        {
            try
            {
                using (var conn = new NpgsqlConnection(ConnStr))
                {
                    conn.Open();

                    using (var cmd = new NpgsqlCommand(
                        "SELECT id, login, password, price_per_day, status_id, platform_id " +
                        "FROM account WHERE id = @id", conn))
                    {
                        cmd.Parameters.Add(new NpgsqlParameter("@id", id));
                        using (var r = cmd.ExecuteReader())
                        {
                            if (!r.Read())
                            {
                                MessageBox.Show("Аккаунт не найден.");
                                return;
                            }
                            txtId.Text = r["id"].ToString();
                            txtAccLogin.Text = Convert.ToString(r["login"]);
                            txtAccPassword.Text = Convert.ToString(r["password"]);
                            numPrice.Value = Convert.ToDecimal(r["price_per_day"]);
                            cmbStatus.SelectedValue = Convert.ToInt32(r["status_id"]);
                            cmbPlatform.SelectedValue = Convert.ToInt32(r["platform_id"]);
                        }
                    }

                    using (var cmd = new NpgsqlCommand(
                        "SELECT game_id FROM account_game WHERE account_id = @id", conn))
                    {
                        cmd.Parameters.Add(new NpgsqlParameter("@id", id));
                        using (var r = cmd.ExecuteReader())
                            while (r.Read())
                                checkedIds.Add(Convert.ToInt32(r["game_id"]));
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // ---------- фильтрация игр по платформе ----------

        private void RefreshGames()
        {
            if (allGames == null || cmbPlatform.SelectedValue == null) return;

            int platformId = Convert.ToInt32(cmbPlatform.SelectedValue);
            populating = true;
            try
            {
                checkedListGames.Items.Clear();
                foreach (DataRow row in allGames.Select("platform_id = " + platformId))
                {
                    int gid = Convert.ToInt32(row["id"]);
                    var item = new GameItem { Id = gid, Name = Convert.ToString(row["name"]) };
                    checkedListGames.Items.Add(item, checkedIds.Contains(gid));
                }
            }
            finally { populating = false; }
        }

        private void CheckedListGames_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (populating) return;
            var item = (GameItem)checkedListGames.Items[e.Index];
            if (e.NewValue == CheckState.Checked) checkedIds.Add(item.Id);
            else checkedIds.Remove(item.Id);
        }

        // ---------- сохранение ----------

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string login = txtAccLogin.Text.Trim();
            if (login.Length == 0 || txtAccPassword.Text.Length == 0)
            {
                MessageBox.Show("Заполните логин и пароль аккаунта.");
                return;
            }
            if (cmbStatus.SelectedValue == null || cmbPlatform.SelectedValue == null)
            {
                MessageBox.Show("Выберите статус и платформу.");
                return;
            }

            try
            {
                bool isNew = string.IsNullOrWhiteSpace(txtId.Text);
                int accountId;

                using (var conn = new NpgsqlConnection(ConnStr))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        string sql = isNew
                            ? "INSERT INTO account (login, password, price_per_day, status_id, platform_id) " +
                              "VALUES (@login, @pass, @price, @sid, @pid) RETURNING id"
                            : "UPDATE account SET login = @login, password = @pass, price_per_day = @price, " +
                              "status_id = @sid, platform_id = @pid WHERE id = @id RETURNING id";

                        using (var cmd = new NpgsqlCommand(sql, conn, tx))
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@login", login));
                            cmd.Parameters.Add(new NpgsqlParameter("@pass", txtAccPassword.Text));
                            cmd.Parameters.Add(new NpgsqlParameter("@price", numPrice.Value));
                            cmd.Parameters.Add(new NpgsqlParameter("@sid", Convert.ToInt32(cmbStatus.SelectedValue)));
                            cmd.Parameters.Add(new NpgsqlParameter("@pid", Convert.ToInt32(cmbPlatform.SelectedValue)));
                            if (!isNew)
                                cmd.Parameters.Add(new NpgsqlParameter("@id", int.Parse(txtId.Text)));

                            object res = cmd.ExecuteScalar();
                            if (res == null) throw new Exception("Аккаунт не найден.");
                            accountId = Convert.ToInt32(res);
                        }

                        using (var cmd = new NpgsqlCommand("DELETE FROM account_game WHERE account_id = @id", conn, tx))
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@id", accountId));
                            cmd.ExecuteNonQuery();
                        }

                        // Только игры выбранной платформы, отмеченные галочками
                        foreach (var obj in checkedListGames.CheckedItems)
                        {
                            var item = (GameItem)obj;
                            using (var cmd = new NpgsqlCommand(
                                "INSERT INTO account_game (account_id, game_id) VALUES (@aid, @gid)", conn, tx))
                            {
                                cmd.Parameters.Add(new NpgsqlParameter("@aid", accountId)); cmd.Parameters.Add(new NpgsqlParameter("@gid", item.Id));
                                cmd.ExecuteNonQuery();
                            }
                        }

                        tx.Commit();
                    }
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }
}