using System;
using System.Drawing;
using System.Windows.Forms;
using Npgsql;

namespace RentalApp
{
    public class FormRegister : Form
    {
        private const string ConnStr = "Host=localhost;Port=5432;Database=MyDataBase;Username=user1;Password=12345678";

        private TextBox txtLogin, txtEmail, txtPassword, txtConfirm;
        private Button btnRegister;

        public FormRegister()
        {
            this.Font = new Font("Segoe UI", 10f);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(800, 600);
            this.Text = "Регистрация";

            var table = new TableLayoutPanel
            {
                Anchor = AnchorStyles.None,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(10)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));

            txtLogin = new TextBox { Dock = DockStyle.Fill };
            txtEmail = new TextBox { Dock = DockStyle.Fill };
            txtPassword = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
            txtConfirm = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };

            AddRow(table, "Логин:", txtLogin);
            AddRow(table, "Email:", txtEmail);
            AddRow(table, "Пароль:", txtPassword);
            AddRow(table, "Повтор пароля:", txtConfirm);

            btnRegister = new Button
            {
                Text = "Зарегистрироваться",
                Height = 40,
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.White
            };
            btnRegister.Click += BtnRegister_Click;
            table.Controls.Add(new Label(), 0, table.RowCount);
            table.Controls.Add(btnRegister, 1, table.RowCount);
            table.RowCount++;

            var host = new Panel { Dock = DockStyle.Fill };
            host.Controls.Add(table);
            host.Resize += (s, e) =>
            {
                table.Left = (host.ClientSize.Width - table.Width) / 2;
                table.Top = (host.ClientSize.Height - table.Height) / 2;
            };
            this.Controls.Add(host);
            this.AcceptButton = btnRegister;
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

        private void BtnRegister_Click(object sender, EventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string email = txtEmail.Text.Trim();
            string password = txtPassword.Text;

            if (login.Length == 0 || email.Length == 0)
            {
                MessageBox.Show("Заполните логин и email.");
                return;
            }
            if (password != txtConfirm.Text)
            {
                MessageBox.Show("Пароли не совпадают.");
                return;
            }
            if (password.Length <= 5)
            {
                MessageBox.Show("Пароль должен быть длиннее 5 символов.");
                return;
            }

            try
            {
                string hash = BCrypt.Net.BCrypt.HashPassword(password);

                using (var conn = new NpgsqlConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("INSERT INTO users (login, password_hash, email, role_id, is_active) " +
                        "VALUES (@login, @hash, @email, 3, true)", conn))
                    {
                        cmd.Parameters.Add(new NpgsqlParameter("@login", login));
                        cmd.Parameters.Add(new NpgsqlParameter("@hash", hash));
                        cmd.Parameters.Add(new NpgsqlParameter("@email", email));
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Регистрация прошла успешно!");
                this.Close();
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                MessageBox.Show("Этот логин уже существует");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}