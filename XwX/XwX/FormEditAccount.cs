using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GameAccountRental
{
    public class FormEditAccount : Form
    {
        private const string ConnStr = "Host=localhost;Port=5432;Database=MyDataBase;Username=user1;Password=12345678";

        private TextBox txtId, txtAccLogin, txtAccPassword, txtGameSearch;
        private NumericUpDown numPrice;
        private ComboBox cmbStatus, cmbPlatform;
        private CheckedListBox checkedListGames;
        private Button btnSave, btnSelectAll, btnSelectNone;

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
            this.ClientSize = new Size(900, 700);
            this.Text = accountId.HasValue ? "Редактирование аккаунта" : "Новый аккаунт";

            var host = new Panel { Dock = DockStyle.Fill };
            this.Controls.Add(host);
            this.Controls.Add(Theme.MakeHeader(this.Text, false));

            // Карточка тянется по высоте, ширина ограничена
            var card = Theme.MakeCard(680);
            card.Padding = new Padding(24);
            host.Controls.Add(card);

            Action placeCard = () =>
            {
                card.Width = Math.Max(300, Math.Min(680, host.ClientSize.Width - 32));
                card.Height = Math.Max(300, host.ClientSize.Height - 32);
                card.Left = Math.Max(0, (host.ClientSize.Width - card.Width) / 2);
                card.Top = 16;
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

            txtId = new TextBox { ReadOnly = true, Enabled = false };
            txtAccLogin = new TextBox();
            txtAccPassword = new TextBox();
            numPrice = new NumericUpDown
            {
                DecimalPlaces = 2,
                Minimum = 0,
                Maximum = 1000000,
                Increment = 10,
                ThousandsSeparator = true,
                TextAlign = HorizontalAlignment.Right,
                MinimumSize = new Size(260, 0)
            };
            cmbStatus = new ComboBox();
            cmbPlatform = new ComboBox();

            Theme.StyleInput(txtId);
            Theme.StyleInput(txtAccLogin);
            Theme.StyleInput(txtAccPassword);
            Theme.StyleNumeric(numPrice);
            numPrice.Font = new Font("Segoe UI", 12f);          // крупнее и шире поле цены
            Theme.StyleCombo(cmbStatus);
            Theme.StyleCombo(cmbPlatform);

            Theme.AddRow(table, "ID", txtId);
            Theme.AddRow(table, "Логин аккаунта", txtAccLogin);
            Theme.AddRow(table, "Пароль аккаунта", txtAccPassword);
            Theme.AddRow(table, "Цена за сутки", numPrice);
            Theme.AddRow(table, "Статус", cmbStatus);
            Theme.AddRow(table, "Платформа", cmbPlatform);

            // ---- блок «Игры»: поиск + кнопки + список ----
            checkedListGames = new CheckedListBox { CheckOnClick = true, IntegralHeight = false, Dock = DockStyle.Fill };
            checkedListGames.ItemCheck += CheckedListGames_ItemCheck;
            Theme.StyleCheckedList(checkedListGames);

            txtGameSearch = new TextBox();
            Theme.StyleInput(txtGameSearch);
            Theme.SetPlaceholder(txtGameSearch, "Поиск по играм…");
            txtGameSearch.TextChanged += (s, e) => RefreshGames();

            btnSelectAll = Theme.MakeButton("Выделить все", BtnKind.Secondary);
            btnSelectNone = Theme.MakeButton("Снять выделение", BtnKind.Secondary);
            btnSelectAll.Click += (s, e) => SetAllChecked(true);
            btnSelectNone.Click += (s, e) => SetAllChecked(false);

            var gamesToolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                ColumnCount = 3,
                RowCount = 1
            };
            gamesToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            gamesToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            gamesToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            txtGameSearch.Dock = DockStyle.Fill;
            txtGameSearch.Margin = new Padding(0, 4, 6, 4);
            btnSelectAll.Dock = DockStyle.Fill;
            btnSelectAll.Margin = new Padding(3, 3, 3, 3);
            btnSelectNone.Dock = DockStyle.Fill;
            btnSelectNone.Margin = new Padding(3, 3, 0, 3);
            gamesToolbar.Controls.Add(txtGameSearch, 0, 0);
            gamesToolbar.Controls.Add(btnSelectAll, 1, 0);
            gamesToolbar.Controls.Add(btnSelectNone, 2, 0);

            var gamesPanel = new Panel { Dock = DockStyle.Fill, Tag = Skin.Surface };
            gamesPanel.Controls.Add(checkedListGames);   // Fill первым
            gamesPanel.Controls.Add(gamesToolbar);       // затем Top

            var lblGames = Theme.MakeCaption("Игры");
            lblGames.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            lblGames.Margin = new Padding(3, 14, 14, 8);
            gamesPanel.Margin = new Padding(3, 6, 3, 6);

            int gamesRow = table.RowCount;
            table.Controls.Add(lblGames, 0, gamesRow);
            table.Controls.Add(gamesPanel, 1, gamesRow);
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

            Theme.Apply(this);
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

        // ---------- список игр: фильтр по платформе + поиск ----------

        private void RefreshGames()
        {
            if (allGames == null || cmbPlatform.SelectedValue == null) return;

            int platformId = Convert.ToInt32(cmbPlatform.SelectedValue);
            string q = (txtGameSearch == null ? "" : txtGameSearch.Text.Trim()).ToLowerInvariant();

            populating = true;
            checkedListGames.BeginUpdate();
            try
            {
                checkedListGames.Items.Clear();
                foreach (DataRow row in allGames.Select("platform_id = " + platformId))
                {
                    string name = Convert.ToString(row["name"]);
                    if (q.Length > 0 && name.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0)
                        continue;

                    int gid = Convert.ToInt32(row["id"]);
                    checkedListGames.Items.Add(new GameItem { Id = gid, Name = name }, checkedIds.Contains(gid));
                }
            }
            finally
            {
                checkedListGames.EndUpdate();
                populating = false;
            }
        }

        private void CheckedListGames_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (populating) return;
            var item = (GameItem)checkedListGames.Items[e.Index];
            if (e.NewValue == CheckState.Checked) checkedIds.Add(item.Id);
            else checkedIds.Remove(item.Id);
        }

        /// <summary>Выделить / снять выделение со всех игр, которые сейчас видны в списке (с учётом поиска).</summary>
        private void SetAllChecked(bool value)
        {
            populating = true;
            try
            {
                for (int i = 0; i < checkedListGames.Items.Count; i++)
                {
                    var item = (GameItem)checkedListGames.Items[i];
                    checkedListGames.SetItemChecked(i, value);
                    if (value) checkedIds.Add(item.Id);
                    else checkedIds.Remove(item.Id);
                }
            }
            finally { populating = false; }
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

                // Игры выбранной платформы, отмеченные галочками (учитываются и скрытые поиском)
                int platformId = Convert.ToInt32(cmbPlatform.SelectedValue);
                var allowed = new HashSet<int>(
                    allGames.Select("platform_id = " + platformId).Select(r => Convert.ToInt32(r["id"])));
                List<int> gameIds = checkedIds.Where(allowed.Contains).ToList();

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
                            cmd.Parameters.Add(new NpgsqlParameter("@pid", platformId));
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

                        foreach (int gid in gameIds)
                        {
                            using (var cmd = new NpgsqlCommand(
                                "INSERT INTO account_game (account_id, game_id) VALUES (@aid, @gid)", conn, tx))
                            {
                                cmd.Parameters.Add(new NpgsqlParameter("@aid", accountId));
                                cmd.Parameters.Add(new NpgsqlParameter("@gid", gid));
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
