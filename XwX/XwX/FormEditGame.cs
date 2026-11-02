using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Npgsql;
using NpgsqlTypes;

namespace GameAccountRental
{
    public class FormEditGame : Form
    {
        private const string ConnStr = "Host=localhost;Port=5432;Database=MyDataBase;Username=user1;Password=12345678";

        private static readonly string[] Genres =
        {
            "Shooter", "RPG", "MOBA", "Strategy", "Action", "Adventure", "Simulator", "Sports",
            "Racing", "Horror", "Survival", "Fighting", "Battle Royale", "MMO", "Sandbox", "Puzzle", "Indie"
        };

        private TextBox txtId, txtTitle, txtCoverUrl;
        private ComboBox cmbGenre, cmbPlatform;
        private Button btnSave;

        public FormEditGame(int? gameId = null)
        {
            this.Font = new Font("Segoe UI", 10f);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 600);
            this.Text = gameId.HasValue ? "Редактирование игры" : "Новая игра";

            var host = new Panel { Dock = DockStyle.Fill };
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

            txtId = new TextBox { ReadOnly = true, Enabled = false };   // ID менять нельзя — серое, неактивное поле
            txtTitle = new TextBox();
            cmbGenre = new ComboBox();
            txtCoverUrl = new TextBox();
            cmbPlatform = new ComboBox();

            Theme.StyleInput(txtId);
            Theme.StyleInput(txtTitle);
            Theme.StyleCombo(cmbGenre);
            cmbGenre.DropDownStyle = ComboBoxStyle.DropDown;            // можно выбрать из списка или вписать свой жанр
            cmbGenre.Items.AddRange(Genres);
            Theme.StyleInput(txtCoverUrl);
            Theme.StyleCombo(cmbPlatform);
            Theme.SetPlaceholder(txtTitle, "Например, Counter-Strike 2");
            Theme.SetPlaceholder(txtCoverUrl, "https://…/cover.jpg");

            Theme.AddStacked(table, "ID", txtId);
            Theme.AddStacked(table, "Название", txtTitle);
            Theme.AddStacked(table, "Жанр", cmbGenre);
            Theme.AddStacked(table, "Ссылка на обложку", txtCoverUrl);
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

            Theme.Apply(this);
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
                    "SELECT id, title, genre, platform_id, cover_url FROM game WHERE id = @id", conn))
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
                        cmbGenre.Text = r["genre"] == DBNull.Value ? "" : Convert.ToString(r["genre"]);
                        txtCoverUrl.Text = r["cover_url"] == DBNull.Value ? "" : Convert.ToString(r["cover_url"]);
                        cmbPlatform.SelectedValue = Convert.ToInt32(r["platform_id"]);
                    }
                }
            }
            catch (Exception ex) { Theme.Error(ex.Message); }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string title = txtTitle.Text.Trim();
            string cover = txtCoverUrl.Text.Trim();

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
            if (cover.Length > 0)
            {
                Uri uri;
                if (!Uri.TryCreate(cover, UriKind.Absolute, out uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    Theme.Warn("Ссылка на обложку должна начинаться с http:// или https://");
                    return;
                }
            }

            try
            {
                bool isNew = string.IsNullOrWhiteSpace(txtId.Text);
                string sql = isNew
                    ? "INSERT INTO game (title, genre, platform_id, cover_url) VALUES (@title, @genre, @pid, @cover)"
                    : "UPDATE game SET title = @title, genre = @genre, platform_id = @pid, cover_url = @cover WHERE id = @id";

                using (var conn = new NpgsqlConnection(ConnStr))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("@title", title));
                    cmd.Parameters.Add(new NpgsqlParameter("@genre", cmbGenre.Text.Trim()));
                    cmd.Parameters.Add(new NpgsqlParameter("@pid", Convert.ToInt32(cmbPlatform.SelectedValue)));
                    cmd.Parameters.Add(new NpgsqlParameter("@cover", NpgsqlDbType.Text)
                    {
                        Value = cover.Length == 0 ? (object)DBNull.Value : cover
                    });
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
