using System;
using System.Collections.Generic;
using System.Text;
using Npgsql;
using NpgsqlTypes;

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
            "Host=localhost;Port=5432;Database=MyDataBase;Username=user1;Password=12345678";

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
        /// Возвращает каталог аккаунтов с необязательной фильтрацией.
        /// searchText — поиск по названию игры или логину аккаунта (без учёта регистра).
        /// maxPrice — максимальная цена за день.
        /// Игры аккаунта собираются в одну строку через string_agg.
        /// </summary>
        public static List<CatalogItem> GetAccountsForCatalog(string searchText = null, decimal? maxPrice = null)
        {
            var sql = new StringBuilder(@"
                SELECT a.id,
                       a.login,
                       a.price_per_day,
                       s.name AS status_name,
                       COALESCE(string_agg(g.title, ', ' ORDER BY g.title), '') AS games
                FROM account a
                JOIN account_status s ON s.id = a.status_id
                LEFT JOIN account_game ag ON ag.account_id = a.id
                LEFT JOIN game g ON g.id = ag.game_id
                WHERE 1 = 1");

            bool hasSearch = !string.IsNullOrWhiteSpace(searchText);

            // Условия добавляем только если параметры переданы,
            // поэтому NULL-параметры в Npgsql передавать не нужно
            if (hasSearch)
            {
                // EXISTS вместо фильтра по g.title в основном JOIN:
                // так в колонке «Игры» останутся ВСЕ игры найденного аккаунта,
                // а не только та, что совпала с поиском
                sql.Append(@"
                AND (a.login ILIKE '%' || @search || '%'
                     OR EXISTS (SELECT 1
                                FROM account_game ag2
                                JOIN game g2 ON g2.id = ag2.game_id
                                WHERE ag2.account_id = a.id
                                  AND g2.title ILIKE '%' || @search || '%'))");
            }

            if (maxPrice.HasValue)
            {
                sql.Append(@"
                AND a.price_per_day <= @maxPrice");
            }

            sql.Append(@"
                GROUP BY a.id, a.login, a.price_per_day, s.name
                ORDER BY a.id");

            var result = new List<CatalogItem>();

            using (var conn = CreateConnection())
            {
                conn.Open();

                using (var cmd = new NpgsqlCommand(sql.ToString(), conn))
                {
                    // Параметризованные запросы — защита от SQL-инъекций
                    if (hasSearch)
                        cmd.Parameters.AddWithValue("@search", searchText.Trim());

                    if (maxPrice.HasValue)
                        cmd.Parameters.AddWithValue("@maxPrice", maxPrice.Value);

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
            }

            return result;
        }

        /// <summary>
        /// Создаёт заказ на аренду аккаунта на указанное число дней.
        /// Цена берётся из БД (а не из таблицы на форме), итог = цена за день * дни.
        /// Возвращает id нового заказа или 0, если аккаунт не найден.
        /// </summary>
        public static int CreateOrder(int userId, int accountId, int days)
        {
            // order_status_id = 1 — «создан»; manager_id и created_at
            // остаются по умолчанию (NULL / DEFAULT в таблице)
            const string sql = @"
                INSERT INTO orders (user_id, account_id, start_date, end_date, total_price, order_status_id)
                SELECT @userId, a.id, @startDate, @endDate, a.price_per_day * @days, 1
                FROM account a
                WHERE a.id = @accountId
                RETURNING id";

            DateTime start = DateTime.Today;
            DateTime end = start.AddDays(days);

            using (var conn = CreateConnection())
            {
                conn.Open();

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);
                    cmd.Parameters.AddWithValue("@accountId", accountId);
                    cmd.Parameters.AddWithValue("@days", days);
                    // Явно указываем тип Date, чтобы не было конфликта с timestamp
                    cmd.Parameters.AddWithValue("@startDate", NpgsqlDbType.Date, start);
                    cmd.Parameters.AddWithValue("@endDate", NpgsqlDbType.Date, end);

                    object id = cmd.ExecuteScalar();
                    return id == null ? 0 : Convert.ToInt32(id);
                }
            }
        }
    }
}
