using Npgsql;
using System;
using System.Windows.Forms;
using XwX;

namespace GameAccountRental
{
    public partial class FormLogin : Form
    {
        public FormLogin()
        {
            InitializeComponent();
        }

        // Этот метод вызывается при нажатии на кнопку "Войти"
        private void btnLogin_Click(object sender, EventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Введите логин и пароль", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Ищем пользователя в БД (вызываем наш DatabaseHelper)
                UserInfo user = DatabaseHelper.GetUserByLogin(login);

                if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    MessageBox.Show("Неверный логин или пароль", "Ошибка входа", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtPassword.Clear();
                    return;
                }

                Session.CurrentUser = user;

                Form nextForm;
                switch (user.RoleName.ToLower())
                {
                    case "admin": nextForm = new FormAdmin(); break;
                    case "manager": nextForm = new FormManager(); break;
                    default: nextForm = new FormMain(); break;
                }

                this.Hide();
                nextForm.ShowDialog(); // Показываем следующее окно
                this.Close(); // Закрываем окно логина, когда следующее закроется
            }
            catch (Exception ex)
            {
                // Если база данных не отвечает, мы УВИДИМ ошибку
                MessageBox.Show("Ошибка БД: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Заглушка для кнопки Регистрация
        private void btnRegister_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Регистрация в разработке");
        }
    }
}