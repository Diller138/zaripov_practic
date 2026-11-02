using System;
using System.Windows.Forms;

namespace GameAccountRental
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Главный цикл приложения
            while (true)
            {
                using (var loginForm = new FormLogin())
                {
                    // Если пользователь зашел успешно
                    if (loginForm.ShowDialog() == DialogResult.OK)
                    {
                        // Открываем форму в зависимости от роли
                        if (Session.CurrentUser.RoleName == "admin")
                        {
                            Application.Run(new FormAdmin());
                        }
                        else if (Session.CurrentUser.RoleName == "manager")
                        {
                            Application.Run(new FormManager());
                        }
                        else
                        {
                            Application.Run(new FormMain());
                        }
                    }
                    else
                    {
                        // Если нажали крестик на форме логина — выходим из программы
                        break;
                    }
                }
            }
        }
    }
}