using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Npgsql;

namespace RentalApp
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

            var table = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(10)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));

            txtId = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            txtTitle = new TextBox { Dock = DockStyle.Fill };
            txtGenre = new TextBox { Dock = DockStyle.Fill };
            cmbPlatform = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };

            AddRow(table, "ID:", txtId);
            AddRow(table, "Название:", txtTitle);
            AddRow(table, "Жанр:", txtGenre);
            AddRow(table, "Платформа:", cmbPlatform);

            btnSave = new Button
            {
                Text = "Сохранить",
                Height = 40,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };
            btnSave.Click += BtnSave_Click;
            table.Controls.Add(new Label(), 0, table.RowCount);
            table.Controls.Add(btnSave, 1, table.RowCount);
            table.RowCount++;

            var host = new Panel { Dock = DockStyle.Fill };
            host.Controls.Add(table);
            host.Resize += (s, e) =>
            {
                table.Left = Math.Max(0, (host.ClientSize.Width - table.Width) / 2);
                table.Top = Math.Max(0, (host.ClientSize.Height - table.Height) / 2);
            };
            this.Controls.Add(host);

            LoadPlatforms();
            if (gameId.HasValue) LoadGame(gameId.Value);
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
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void LoadGame(int id)
        {
            try
            {
                using (var conn = new NpgsqlConnection(ConnStr))
                using (var cmd = new NpgsqlCommand(
                    "SELECT id, name, genre, platform_id FROM game WHERE id = @id", conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("@id", id)); conn.Open();
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                        {
                            MessageBox.Show("Игра не найдена.");
                            return;
                        }
                        txtId.Text = r["id"].ToString();
                        txtTitle.Text = Convert.ToString(r["name"]);
                        txtGenre.Text = r["genre"] == DBNull.Value ? "" : Convert.ToString(r["genre"]);
                        cmbPlatform.SelectedValue = Convert.ToInt32(r["platform_id"]);
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string title = txtTitle.Text.Trim();
            if (title.Length == 0)
            {
                MessageBox.Show("Введите название игры.");
                return;
            }
            if (cmbPlatform.SelectedValue == null)
            {
                MessageBox.Show("Выберите платформу.");
                return;
            }

            try
            {
                bool isNew = string.IsNullOrWhiteSpace(txtId.Text);
                string sql = isNew
                    ? "INSERT INTO game (name, genre, platform_id) VALUES (@name, @genre, @pid)"
                    : "UPDATE game SET name = @name, genre = @genre, platform_id = @pid WHERE id = @id";

                using (var conn = new NpgsqlConnection(ConnStr))
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.Add(new NpgsqlParameter("@name", title));
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
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }
}