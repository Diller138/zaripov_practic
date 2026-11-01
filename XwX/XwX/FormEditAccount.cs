using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Npgsql;

namespace GameAccountRental
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
            Theme.Apply(this);

            var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            this.Controls.Add(host);
            this.Controls.Add(Theme.MakeHeader(this.Text, false));

            // Карточка тянется по высоте, ширина ограничена
            var card = Theme.MakeCard(640);
            host.Controls.Add(card);
            Action placeCard = () =>
            {
                card.Width = Math.Max(300, Math.Min(640, host.ClientSize.Width - 48));
                card.Height = Math.Max(300, host.ClientSize.Height - 48);
                card.Left = Math.Max(0, (host.ClientSize.Width - card.Width) / 2);
                card.Top = 24;
            };
            host.Resize += (s, e) => placeCard();
            this.Load += (s, e) => placeCard();
            placeCard();

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            txtId = new TextBox { ReadOnly = true };
            txtAccLogin = new TextBox();
            txtAccPassword = new TextBox();
            numPrice = new NumericUpDown
            {
                DecimalPlaces = 2,
                Minimum = 0,
                Maximum = 1000000,
                Increment = 10
            };
            cmbStatus = new ComboBox();
            cmbPlatform = new ComboBox();
            checkedListGames = new CheckedListBox
            {
                CheckOnClick = true,
                IntegralHeight = false
            };
            checkedListGames.ItemCheck += CheckedListGames_ItemCheck;

            Theme.StyleInput(txtId);
            txtId.BackColor = Theme.Bg;      // ID нельзя менять — «приглушаем»
            txtId.ForeColor = Theme.Muted;
            Theme.StyleInput(txtAccLogin);
            Theme.StyleInput(txtAccPassword);
            Theme.StyleNumeric(numPrice);
            Theme.StyleCombo(cmbStatus);
            Theme.StyleCombo(cmbPlatform);
            Theme.StyleCheckedList(checkedListGames);

            Theme.AddRow(table, "ID", txtId);
            Theme.AddRow(table, "Логин аккаунта", txtAccLogin);
            Theme.AddRow(table, "Пароль аккаунта", txtAccPassword);
            Theme.AddRow(table, "Цена за сутки", numPrice);
            Theme.AddRow(table, "Статус", cmbStatus);
            Theme.AddRow(table, "Платформа", cmbPlatform);

            // Список игр занимает всё оставшееся место
            var lblGames = Theme.MakeCaption("Игры");
            lblGames.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            lblGames.Margin = new Padding(3, 12, 14, 10);
            checkedListGames.Dock = DockStyle.Fill;
            checkedListGames.Margin = new Padding(3, 6, 3, 6);

            int gamesRow = table.RowCount;
            table.Controls.Add(lblGames, 0, gamesRow);
            table.Controls.Add(checkedListGames, 1, gamesRow);
            table.RowCount = gamesRow + 1;

            table.RowStyles.Clear();
            for (int i = 0; i < gamesRow; i++)
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            btnSave = Theme.MakeButton("Сохранить", BtnKind.Primary);
            btnSave.Height = 44;
            btnSave.Dock = DockStyle.Bottom;
            btnSave.Click += BtnSave_Click;

            // Fill -> Bottom
            card.Controls.Add(table);
            card.Controls.Add(btnSave);

            LoadLookups();
            if (accountId.HasValue) LoadAccount(accountId.Value);
            cmbPlatform.SelectedIndexChanged += (s, e) => RefreshGames();
            RefreshGames();
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

                allGames = Query("SELECT id, title AS name, platform_id FROM game ORDER BY title");
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
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
                                Theme.Warn("Аккаунт не найден.");
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
            catch (Exception ex) { Theme.Error(ex.Message); }
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
                Theme.Warn("Заполните логин и пароль аккаунта.");
                return;
            }
            if (cmbStatus.SelectedValue == null || cmbPlatform.SelectedValue == null)
            {
                Theme.Warn("Выберите статус и платформу.");
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
                                cmd.Parameters.Add(new NpgsqlParameter("@aid", accountId));
                                cmd.Parameters.Add(new NpgsqlParameter("@gid", item.Id));
                                cmd.ExecuteNonQuery();
                            }
                        }

                        tx.Commit();
                    }
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
        }
    }
}
