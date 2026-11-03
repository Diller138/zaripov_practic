using Npgsql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Windows.Forms;

namespace GameAccountRental
{
    // =====================================================================
    //  1. Кэш картинок в памяти
    // =====================================================================

    /// <summary>
    /// Кэш миниатюр: url + размер -> Bitmap. Живёт всё время работы приложения,
    /// поэтому обложки не пропадают при перезагрузке DataSource, сортировке и фильтрации.
    /// Вызывать только из UI-потока.
    /// </summary>
    public static class ImageCache
    {
        private static readonly Dictionary<string, Image> Cache = new Dictionary<string, Image>();
        private static readonly HashSet<string> Loading = new HashSet<string>();
        private static readonly HashSet<string> Failed = new HashSet<string>();

        /// <summary>Срабатывает (в UI-потоке) после загрузки очередной картинки — таблице пора перерисоваться.</summary>
        public static event Action Loaded;

        /// <summary>
        /// Возвращает картинку из кэша мгновенно. Если её там нет — возвращает заглушку
        /// и запускает фоновую загрузку (после неё сработает событие Loaded).
        /// </summary>
        public static Image Get(string url, Size size)
        {
            if (string.IsNullOrWhiteSpace(url))
                return Theme.Placeholder(size.Width, size.Height);

            url = url.Trim();
            string key = url + "|" + size.Width + "x" + size.Height;

            Image img;
            if (Cache.TryGetValue(key, out img)) return img;

            if (!Failed.Contains(key) && Loading.Add(key))
                Download(key, url, size);

            return Theme.Placeholder(size.Width, size.Height);
        }

        /// <summary>Разрешить повторную попытку для ссылок, которые не загрузились (например, при обновлении таблицы).</summary>
        public static void ClearFailed()
        {
            Failed.Clear();
        }

        private static async void Download(string key, string url, Size size)
        {
            try
            {
                Uri uri;
                if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    throw new InvalidOperationException("bad url");

                using (var wc = new WebClient())
                {
                    byte[] data = await wc.DownloadDataTaskAsync(uri);   // продолжится в UI-потоке
                    using (var ms = new MemoryStream(data))
                    using (var src = Image.FromStream(ms))
                    {
                        // Вписываем с сохранением пропорций
                        var bmp = new Bitmap(size.Width, size.Height);
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            float k = Math.Min((float)size.Width / src.Width, (float)size.Height / src.Height);
                            int w = Math.Max(1, (int)(src.Width * k));
                            int h = Math.Max(1, (int)(src.Height * k));
                            g.DrawImage(src, (size.Width - w) / 2, (size.Height - h) / 2, w, h);
                        }
                        Cache[key] = bmp;
                    }
                }
            }
            catch
            {
                Failed.Add(key);
            }
            finally
            {
                Loading.Remove(key);
            }

            Action handler = Loaded;
            if (handler != null) handler();
        }
    }

    // =====================================================================
    //  2. Сохранение скролла / выделения / сортировки таблицы
    // =====================================================================

    public static class GridState
    {
        /// <summary>
        /// Запоминает позицию скролла, выбранную строку (по колонке "id") и сортировку.
        /// Использование:
        ///     var restore = GridState.Capture(dgv);
        ///     dgv.DataSource = ...;   // + настройка колонок
        ///     restore();
        /// </summary>
        public static Action Capture(DataGridView dgv)
        {
            int first = -1;
            try { if (dgv.RowCount > 0) first = dgv.FirstDisplayedScrollingRowIndex; }
            catch { first = -1; }

            object selectedId = null;
            try
            {
                if (dgv.CurrentRow != null && dgv.Columns.Contains("id"))
                    selectedId = dgv.CurrentRow.Cells["id"].Value;
            }
            catch { selectedId = null; }

            string sortName = dgv.SortedColumn != null ? dgv.SortedColumn.Name : null;
            ListSortDirection dir = dgv.SortOrder == SortOrder.Descending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            return () => Restore(dgv, first, selectedId, sortName, dir);
        }

        private static void Restore(DataGridView dgv, int first, object selectedId, string sortName, ListSortDirection dir)
        {
            try
            {
                // 1. сортировка, которую выбрал пользователь
                if (sortName != null && dgv.Columns.Contains(sortName))
                {
                    try { dgv.Sort(dgv.Columns[sortName], dir); }
                    catch { /* колонка не сортируется — пропускаем */ }
                }

                if (dgv.RowCount == 0) return;

                // 2. выбранная строка
                if (selectedId != null && dgv.Columns.Contains("id"))
                {
                    string want = Convert.ToString(selectedId);
                    DataGridViewColumn firstVisible = dgv.Columns.GetFirstColumn(DataGridViewElementStates.Visible);
                    foreach (DataGridViewRow row in dgv.Rows)
                    {
                        if (Convert.ToString(row.Cells["id"].Value) == want)
                        {
                            if (firstVisible != null) dgv.CurrentCell = row.Cells[firstVisible.Index];
                            row.Selected = true;
                            break;
                        }
                    }
                }

                // 3. скролл (после выбора строки, т.к. смена CurrentCell может прокрутить таблицу)
                if (first >= 0)
                    dgv.FirstDisplayedScrollingRowIndex = Math.Min(first, dgv.RowCount - 1);
            }
            catch
            {
                // таблица может быть скрыта (вкладка не выбрана) — тогда скролл восстановить нельзя, это не критично
            }
        }
    }

    // =====================================================================
    //  3. Аренда аккаунта (общая логика для FormMain и FormAccountDetails)
    // =====================================================================

    public static class RentService
    {
        private const string ConnStr = "Host=localhost;Port=5432;Database=MyDataBase;Username=user1;Password=12345678";

        public const string FreeStatus = "свободен";
        public const string BusyStatus = "арендован";

        /// <summary>
        /// Спрашивает срок аренды (Theme.PromptNumber) и создаёт заказ.
        /// Возвращает true, если заказ создан. Все ошибки показывает сама.
        /// </summary>
        public static bool TryRent(IWin32Window owner, int accountId)
        {
            try
            {
                using (var conn = new NpgsqlConnection(ConnStr))
                {
                    conn.Open();

                    // Быстрая проверка до вопроса о сроке
                    string status = GetStatus(conn, accountId);
                    if (!string.Equals(status, FreeStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        Theme.Warn("Этот аккаунт уже занят. Выберите другой — свободные аккаунты показаны в начале списка.");
                        return false;
                    }

                    int? freeId = GetStatusId(conn, FreeStatus);
                    int? busyId = GetStatusId(conn, BusyStatus);
                    if (!freeId.HasValue || !busyId.HasValue)
                    {
                        Theme.Error("В справочнике статусов аккаунтов должны быть значения «" + FreeStatus + "» и «" + BusyStatus + "».");
                        return false;
                    }

                    int? days = Theme.PromptNumber(owner, "Аренда", "На сколько дней арендовать?", 1, 365, 1);
                    if (!days.HasValue) return false; // отмена

                    using (var tx = conn.BeginTransaction())
                    {
                        // Атомарно «занимаем» аккаунт: сработает, только если он всё ещё свободен
                        int updated;
                        using (var cmd = new NpgsqlCommand(
                            "UPDATE account SET status_id = @busy WHERE id = @aid AND status_id = @free", conn, tx))
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@busy", busyId.Value));
                            cmd.Parameters.Add(new NpgsqlParameter("@free", freeId.Value));
                            cmd.Parameters.Add(new NpgsqlParameter("@aid", accountId));
                            updated = cmd.ExecuteNonQuery();
                        }

                        if (updated == 0)
                        {
                            tx.Rollback();
                            Theme.Warn("Пока вы выбирали срок, этот аккаунт уже арендовал другой пользователь. Выберите другой.");
                            return false;
                        }

                        using (var cmd = new NpgsqlCommand(
                            "INSERT INTO orders (user_id, account_id, start_date, end_date, total_price, order_status_id) " +
                            "VALUES (@uid, @aid, CURRENT_DATE, CURRENT_DATE + @days, 0, 1)", conn, tx))
                        {
                            cmd.Parameters.Add(new NpgsqlParameter("@uid", Session.CurrentUser.Id));
                            cmd.Parameters.Add(new NpgsqlParameter("@aid", accountId));
                            cmd.Parameters.Add(new NpgsqlParameter("@days", days.Value));
                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                    }
                }

                Theme.Info("Заявка на аренду создана. Аккаунт закреплён за вами до подтверждения менеджером.");
                return true;
            }
            catch (PostgresException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                if (ex.SqlState == "23505" || ex.SqlState == "23P01" || ex.SqlState == "23514" || ex.SqlState == "P0001")
                    Theme.Warn("Этот аккаунт уже арендован или занят на выбранные даты. Пожалуйста, выберите другой аккаунт.");
                else
                    Theme.Error("Не удалось оформить аренду. Попробуйте ещё раз или обратитесь к администратору.");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                Theme.Error("Не удалось оформить аренду. Проверьте подключение к базе данных и попробуйте ещё раз.");
                return false;
            }
        }

        private static string GetStatus(NpgsqlConnection conn, int accountId)
        {
            using (var cmd = new NpgsqlCommand(
                "SELECT s.name FROM account a JOIN account_status s ON s.id = a.status_id WHERE a.id = @aid", conn))
            {
                cmd.Parameters.Add(new NpgsqlParameter("@aid", accountId));
                return Convert.ToString(cmd.ExecuteScalar());
            }
        }

        private static int? GetStatusId(NpgsqlConnection conn, string name)
        {
            using (var cmd = new NpgsqlCommand("SELECT id FROM account_status WHERE name = @n", conn))
            {
                cmd.Parameters.Add(new NpgsqlParameter("@n", name));
                object res = cmd.ExecuteScalar();
                return res == null || res == DBNull.Value ? (int?)null : Convert.ToInt32(res);
            }
        }
    }
}
