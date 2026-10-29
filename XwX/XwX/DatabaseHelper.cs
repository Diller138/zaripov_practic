using System;
using System.Collections.Generic;
using Npgsql;

namespace GameAccountRental
{
    /// <summary>
    /// Данные пользователя (результат GetUserByLogin).
    /// </summary>
    public class UserInfo
    {
        public int Id { get; set; }
        public string Login { get; set; }
        public string PasswordHash { get; set; }
        public string Email { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }   // admin / manager / user
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Строка каталога аккаунтов.
    /// </summary>
    public class CatalogItem
    {
        public int AccountId { get; set; }
        public string AccountLogin { get; set; }
        public decimal PricePerDay { get; set; }
        public string Status { get; set; }
        public string Games { get; set; }      // игры через запятую
    }

    /// <summary>
    /// Текущий авторизованный пользователь (доступен из любой формы).
    /// </summary>
    public static class Session
    {
        public static UserInfo CurrentUser { get; set; }
    }

    /// <summary>
    /// Статический класс для работы с БД PostgreSQL.
    /// </summary>
    public static class DatabaseHelper
    {
        // TODO: подставьте свои значения
        private const string ConnectionString =
            "Host=localhost;Port=5432;Database=имя_твоей_базы;Username=postgres;Password=твой_пароль";

        /// <summary>
        /// Создаёт новое подключение (открывается в блоке using у вызывающего метода).
        /// </summary>
        private static NpgsqlConnection CreateConnection()
        {
            return new NpgsqlConnection(ConnectionString);
        }

        /// <summary>
        /// Возвращает пользователя по логину вместе с названием роли.
        /// Если пользователь не найден — возвращает null.
        /// </summary>
        public static UserInfo GetUserByLogin(string login)
        {
            const string sql = @"
                SELECT u.id, u.login, u.password_hash, u.email,
                       u.role_id, r.name AS role_name, u.created_at
                FROM users u
                JOIN role r ON r.id = u.role_id
                WHERE u.login = @login";

            using (var conn = CreateConnection())
            {
                conn.Open();

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    // Параметризованный запрос — защита от SQL-инъекций
                    cmd.Parameters.AddWithValue("@login", login);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return null; // пользователь не найден

                        return new UserInfo
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("id")),
                            Login = reader.GetString(reader.GetOrdinal("login")),
                            PasswordHash = reader.GetString(reader.GetOrdinal("password_hash")),
                            Email = reader.IsDBNull(reader.GetOrdinal("email"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("email")),
                            RoleId = reader.GetInt32(reader.GetOrdinal("role_id")),
                            RoleName = reader.GetString(reader.GetOrdinal("role_name")),
                            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Возвращает каталог аккаунтов: логин, цена за день, статус и список игр.
        /// Игры собираются в одну строку через string_agg, чтобы на аккаунт была одна строка.
        /// </summary>
        public static List<CatalogItem> GetAccountsForCatalog()
        {
            const string sql = @"
                SELECT a.id,
                       a.login,
                       a.price_per_day,
                       s.name AS status_name,
                       COALESCE(string_agg(g.title, ', ' ORDER BY g.title), '') AS games
                FROM account a
                JOIN account_status s ON s.id = a.status_id
                LEFT JOIN account_game ag ON ag.account_id = a.id
                LEFT JOIN game g ON g.id = ag.game_id
                GROUP BY a.id, a.login, a.price_per_day, s.name
                ORDER BY a.id";

            var result = new List<CatalogItem>();

            using (var conn = CreateConnection())
            {
                conn.Open();

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new CatalogItem
                        {
                            AccountId = reader.GetInt32(0),
                            AccountLogin = reader.GetString(1),
                            PricePerDay = reader.GetDecimal(2),
                            Status = reader.GetString(3),
                            Games = reader.GetString(4)
                        });
                    }
                }
            }

            return result;
        }
    }
}
