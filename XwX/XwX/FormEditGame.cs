using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Npgsql;

namespace GameAccountRental
{
    public class FormEditGame : Form
    {
        private const string ConnStr = "Host=localhost;Port=5432;Database=MyDataBase;Username=user1;Password=12345678";

        private TextBox txtId, txtTitle, txtGenre;
        private ComboBox cmbPlatform;
        private Button btnSave;

        public FormEditGame(int? gameId = null)
        {
            this.Font = new Font("Segoe UI", 10f);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 600);
            this.Padding = new Padding(20);
            this.Text = gameId.HasValue ? "Редактирование игры" : "Новая игра";
            Theme.Apply(this);

            var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            this.Controls.Add(host);
            this.Controls.Add(Theme.MakeHeader(this.Text, false));

            var card = Theme.MakeCard(440);
            host.Controls.Add(card);

            var table = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                Location = new Point(32, 32)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 376));
            card.Controls.Add(table);

            txtId = new TextBox { ReadOnly = true };
            txtTitle = new TextBox();
            txtGenre = new TextBox();
            cmbPlatform = new ComboBox();

            Theme.StyleInput(txtId);
            txtId.BackColor = Theme.Bg;      // ID нельзя менять — визуально «приглушаем»
            txtId.ForeColor = Theme.Muted;
            Theme.StyleInput(txtTitle);
            Theme.StyleInput(txtGenre);
            Theme.StyleCombo(cmbPlatform);
            Theme.SetPlaceholder(txtTitle, "Например, Counter-Strike 2");
            Theme.SetPlaceholder(txtGenre, "Например, Шутер");

            Theme.AddStacked(table, "ID", txtId);
            Theme.AddStacked(table, "Название", txtTitle);
            Theme.AddStacked(table, "Жанр", txtGenre);
            Theme.AddStacked(table, "Платформа", cmbPlatform);

            btnSave = Theme.MakeButton("Сохранить", BtnKind.Primary);
            btnSave.Height = 44;
            btnSave.Dock = DockStyle.Fill;
            btnSave.Margin = new Padding(3, 16, 3, 0);
            btnSave.Click += BtnSave_Click;
            int r = table.RowCount;
            table.Controls.Add(btnSave, 0, r);
            table.RowCount = r + 1;

            card.Height = table.GetPreferredSize(Size.Empty).Height + 64;
            Theme.CenterIn(host, card);

            LoadPlatforms();
            if (gameId.HasValue) LoadGame(gameId.Value);
        }

        private void LoadPlatforms()
        {
            try
            {
                var dt = new DataTable();
                using (var conn = new NpgsqlConnection(ConnStr))
                using (var da = new NpgsqlDataAdapter("SELECT id, name FROM platform ORDER BY name", conn))
                    da.Fill(dt);

                cmbPlatform.DataSource = dt;
                cmbPlatform.DisplayMember = "name";
                cmbPlatform.ValueMember = "id";
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
        }

        private void LoadGame(int id)
        {
            try
            {
                using (var conn = new NpgsqlConnection(ConnStr))
                using (var cmd = new NpgsqlCommand(
                    "SELECT id, title, genre, platform_id FROM game WHERE id = @id", conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("@id", id));
                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                        {
                            Theme.Warn("Игра не найдена.");
                            return;
                        }
                        txtId.Text = r["id"].ToString();
                        txtTitle.Text = Convert.ToString(r["title"]);
                        txtGenre.Text = r["genre"] == DBNull.Value ? "" : Convert.ToString(r["genre"]);
                        cmbPlatform.SelectedValue = Convert.ToInt32(r["platform_id"]);
                    }
                }
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string title = txtTitle.Text.Trim();
            if (title.Length == 0)
            {
                Theme.Warn("Введите название игры.");
                return;
            }
            if (cmbPlatform.SelectedValue == null)
            {
                Theme.Warn("Выберите платформу.");
                return;
            }

            try
            {
                bool isNew = string.IsNullOrWhiteSpace(txtId.Text);
                string sql = isNew
                    ? "INSERT INTO game (title, genre, platform_id) VALUES (@title, @genre, @pid)"
                    : "UPDATE game SET title = @title, genre = @genre, platform_id = @pid WHERE id = @id";

                using (var conn = new NpgsqlConnection(ConnStr))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("@title", title));
                    cmd.Parameters.Add(new NpgsqlParameter("@genre", txtGenre.Text.Trim()));
                    cmd.Parameters.Add(new NpgsqlParameter("@pid", Convert.ToInt32(cmbPlatform.SelectedValue)));
                    if (!isNew)
                        cmd.Parameters.Add(new NpgsqlParameter("@id", int.Parse(txtId.Text)));

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
        }
    }
}
