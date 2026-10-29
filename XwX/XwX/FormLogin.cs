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

            // Пароль скрываем точками
            txtPassword.UseSystemPasswordChar = true;

            // Enter в форме = нажатие «Войти»
            this.AcceptButton = btnLogin;
        }

        /// <summary>
        /// Кнопка «Войти».
        /// </summary>
        private void btnLogin_Click(object sender, EventArgs e)
        {
            string login = txtLogin.Text.Trim();
            string password = txtPassword.Text;

            // Проверка на пустые поля
            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Введите логин и пароль", "Внимание",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Ищем пользователя в БД
                UserInfo user = DatabaseHelper.GetUserByLogin(login);

                // Сообщение одинаковое для «нет такого логина» и «неверный пароль»,
                // чтобы не подсказывать, какие логины существуют
                if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    MessageBox.Show("Неверный логин или пароль", "Ошибка входа",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtPassword.Clear();
                    txtPassword.Focus();
                    return;
                }

                // Запоминаем текущего пользователя для других форм
                Session.CurrentUser = user;

                // Выбираем форму по роли
                Form nextForm;
                switch (user.RoleName.ToLower())
                {
                    case "admin":
                        nextForm = new FormAdmin();
                        break;
                    case "manager":
                        nextForm = new FormManager();
                        break;
                    case "user":
                        nextForm = new FormMain();
                        break;
                    default:
                        MessageBox.Show("Неизвестная роль пользователя: " + user.RoleName,
                            "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                }

                // Когда новая форма закроется — возвращаем окно входа
                nextForm.FormClosed += (s, args) =>
                {
                    Session.CurrentUser = null;
                    txtPassword.Clear();
                    this.Show();
                };

                this.Hide();
                nextForm.Show();
            }
            catch (NpgsqlException ex)
            {
                // Ошибки подключения к БД / SQL
                MessageBox.Show("Не удалось подключиться к базе данных:\n" + ex.Message,
                    "Ошибка БД", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                // В БД лежит не bcrypt-хэш (например, пароль в открытом виде)
                MessageBox.Show("Неверный логин или пароль", "Ошибка входа",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Непредвиденная ошибка:\n" + ex.Message,
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Кнопка «Регистрация».
        /// </summary>
        private void btnRegister_Click(object sender, EventArgs e)
        {
            // TODO: создайте форму FormRegister и раскомментируйте
            // var reg = new FormRegister();
            // reg.ShowDialog();
            MessageBox.Show("Форма регистрации пока не создана");
        }
    }
}
