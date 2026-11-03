using System;
using System.Drawing;
using System.Windows.Forms;
using Npgsql;

namespace GameAccountRental
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

            // ТУМБЛЕР ТЕМЫ (правый верхний угол)
            this.Controls.Add(new ThemeToggle { Left = this.ClientSize.Width - 80, Top = 15, Height = 40, Anchor = AnchorStyles.Top | AnchorStyles.Right });

            Theme.Apply(this);

            var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            this.Controls.Add(host);

            // Карточка по центру
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

            var lblTitle = new Label
            {
                Text = "Создание аккаунта",
                AutoSize = true,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                Margin = new Padding(3, 0, 3, 2)
            };
            var lblSub = new Label
            {
                Text = "Заполните данные, чтобы начать арендовать аккаунты",
                AutoSize = true,
                ForeColor = Theme.Muted,
                MaximumSize = new Size(376, 0),
                Margin = new Padding(3, 0, 3, 14)
            };
            table.Controls.Add(lblTitle, 0, 0);
            table.Controls.Add(lblSub, 0, 1);
            table.RowCount = 2;

            txtLogin = new TextBox();
            txtEmail = new TextBox();
            txtPassword = new TextBox { UseSystemPasswordChar = true };
            txtConfirm = new TextBox { UseSystemPasswordChar = true };
            foreach (var t in new[] { txtLogin, txtEmail, txtPassword, txtConfirm })
                Theme.StyleInput(t);

            Theme.SetPlaceholder(txtLogin, "Придумайте логин");
            Theme.SetPlaceholder(txtEmail, "name@example.com");
            Theme.SetPlaceholder(txtPassword, "Не короче 6 символов");
            Theme.SetPlaceholder(txtConfirm, "Повторите пароль");

            Theme.AddStacked(table, "Логин", txtLogin);
            Theme.AddStacked(table, "Email", txtEmail);
            Theme.AddStacked(table, "Пароль", txtPassword);
            Theme.AddStacked(table, "Повтор пароля", txtConfirm);

            btnRegister = Theme.MakeButton("Зарегистрироваться", BtnKind.Primary);
            btnRegister.Height = 44;
            btnRegister.Dock = DockStyle.Fill;
            btnRegister.Margin = new Padding(3, 16, 3, 0);
            btnRegister.Click += BtnRegister_Click;
            int r = table.RowCount;
            table.Controls.Add(btnRegister, 0, r);
            table.RowCount = r + 1;

            card.Height = table.GetPreferredSize(Size.Empty).Height + 64;
            Theme.CenterIn(host, card);
            this.AcceptButton = btnRegister;
        }

        private void BtnRegister_Click(object sender, EventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string email = txtEmail.Text.Trim();
            string password = txtPassword.Text;

            if (login.Length == 0 || email.Length == 0)
            {
                Theme.Warn("Заполните логин и email.");
                return;
            }
            if (password != txtConfirm.Text)
            {
                Theme.Warn("Пароли не совпадают.");
                return;
            }
            if (password.Length <= 5)
            {
                Theme.Warn("Пароль должен быть длиннее 5 символов.");
                return;
            }

            try
            {
                string hash = BCrypt.Net.BCrypt.HashPassword(password);

                using (var conn = new NpgsqlConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(
                        "INSERT INTO users (login, password_hash, email, role_id, is_active) " +
                        "VALUES (@login, @hash, @email, 3, true)", conn))
                    {
                        cmd.Parameters.Add(new NpgsqlParameter("@login", login));
                        cmd.Parameters.Add(new NpgsqlParameter("@hash", hash));
                        cmd.Parameters.Add(new NpgsqlParameter("@email", email));
                        cmd.ExecuteNonQuery();
                    }
                }

                Theme.Info("Регистрация прошла успешно!");
                this.Close();
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                Theme.Warn("Этот логин уже существует");
            }
            catch (Exception ex)
            {
                Theme.Error(ex.Message);
            }
        }
    }
}