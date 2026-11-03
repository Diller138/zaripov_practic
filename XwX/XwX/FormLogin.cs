using System;
using System.Drawing;
using System.Windows.Forms;

namespace GameAccountRental
{
    public class FormLogin : Form
    {
        private TextBox txtLogin;
        private TextBox txtPassword;
        private Button btnLogin;
        private Button btnRegister;

        public FormLogin()
        {
            this.Text = "Вход в систему";
            this.Font = new Font("Segoe UI", 10f);
            this.ClientSize = new Size(420, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // Акцентная полоса сверху
            this.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 5, BackColor = Theme.Accent, Tag = Skin.Accent });

            // ТУМБЛЕР ТЕМЫ (правый верхний угол)
            this.Controls.Add(new ThemeToggle { Left = this.ClientSize.Width - 72, Top = 15, Height = 40 });

            this.Controls.Add(new Label
            {
                Text = "Аренда игровых аккаунтов",
                Font = new Font("Segoe UI", 17f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Left = 0,
                Top = 50,
                Width = 420,
                Height = 40
            });

            this.Controls.Add(Theme.Mark(new Label
            {
                Text = "Войдите, чтобы продолжить",
                TextAlign = ContentAlignment.MiddleCenter,
                Left = 0,
                Top = 92,
                Width = 420,
                Height = 24
            }, Skin.Muted));

            // Логин
            this.Controls.Add(Theme.Mark(new Label { Text = "Логин", Left = 40, Top = 150, Width = 340, Height = 22 }, Skin.Muted));
            txtLogin = new TextBox { Left = 40, Top = 176, Width = 340 };
            Theme.StyleInput(txtLogin);
            Theme.SetPlaceholder(txtLogin, "Введите логин");
            this.Controls.Add(txtLogin);

            // Пароль
            this.Controls.Add(Theme.Mark(new Label { Text = "Пароль", Left = 40, Top = 228, Width = 340, Height = 22 }, Skin.Muted));
            txtPassword = new TextBox { Left = 40, Top = 254, Width = 340, UseSystemPasswordChar = true };
            Theme.StyleInput(txtPassword);
            Theme.SetPlaceholder(txtPassword, "Введите пароль");
            this.Controls.Add(txtPassword);

            // Кнопки
            btnLogin = Theme.MakeButton("Войти", BtnKind.Primary);
            btnLogin.Left = 40;
            btnLogin.Top = 324;
            btnLogin.Width = 340;
            btnLogin.Height = 44;
            btnLogin.Click += BtnLogin_Click;
            this.Controls.Add(btnLogin);

            this.Controls.Add(Theme.Mark(new Label
            {
                Text = "Нет аккаунта?",
                TextAlign = ContentAlignment.MiddleCenter,
                Left = 0,
                Top = 388,
                Width = 420,
                Height = 22
            }, Skin.Muted));

            btnRegister = Theme.MakeButton("Создать аккаунт", BtnKind.Secondary);
            btnRegister.Left = 40;
            btnRegister.Top = 416;
            btnRegister.Width = 340;
            btnRegister.Height = 40;
            btnRegister.Click += BtnRegister_Click;
            this.Controls.Add(btnRegister);

            this.AcceptButton = btnLogin;
            Theme.Apply(this);
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                Theme.Warn("Введите логин и пароль");
                return;
            }

            try
            {
                UserInfo user = DatabaseHelper.GetUserByLogin(login);

                if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    Theme.Error("Неверный логин или пароль");
                    txtPassword.Clear();
                    return;
                }

                // Блокировку проверяем только после верного пароля
                if (!user.IsActive)
                {
                    Theme.Warn("Вы заблокированы");
                    txtPassword.Clear();
                    return;
                }

                Session.CurrentUser = user;

                // Сообщаем Program.cs, что вход успешен
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                Theme.Error("Ошибка БД: " + ex.Message);
            }
        }

        private void BtnRegister_Click(object sender, EventArgs e)
        {
            using (var regForm = new FormRegister())
            {
                regForm.ShowDialog(this);
            }
        }
    }
}