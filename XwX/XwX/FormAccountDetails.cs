using Npgsql;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace GameAccountRental
{
    /// <summary>Подробная информация об аккаунте: логин, платформа, цена, статус и игры с обложками.</summary>
    public class FormAccountDetails : Form
    {
        private const string ConnStr = "Host=localhost;Port=5432;Database=MyDataBase;Username=user1;Password=12345678";

        private readonly int _accountId;
        private Label lblLogin, lblPlatform, lblPrice, lblStatus;
        private FlowLayoutPanel flowGames;
        private Label lblGamesCaption;

        public FormAccountDetails(int accountId)
        {
            _accountId = accountId;

            this.Font = new Font("Segoe UI", 10f);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(800, 600);
            this.Text = "Информация об аккаунте";

            // Fill -> Bottom -> Top (в порядке «снизу вверх»)
            flowGames = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                Padding = new Padding(12, 0, 12, 0),
                Tag = Skin.Bg
            };
            this.Controls.Add(flowGames);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(16, 10, 16, 16) };
            var btnClose = Theme.MakeButton("Закрыть", BtnKind.Secondary);
            btnClose.Dock = DockStyle.Right;
            btnClose.Width = 160;
            btnClose.Click += (s, e) => this.Close();
            bottom.Controls.Add(btnClose);
            this.Controls.Add(bottom);

            lblGamesCaption = new Label
            {
                Text = "Игры на аккаунте",
                Dock = DockStyle.Top,
                Height = 44,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 6, 0, 0)
            };
            this.Controls.Add(lblGamesCaption);

            // Карточка с основными данными
            var infoHost = new Panel { Dock = DockStyle.Top, Height = 118, Padding = new Padding(16, 16, 16, 0) };
            var infoCard = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 14, 20, 10), Tag = Skin.Surface };
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2 };
            for (int i = 0; i < 4; i++) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            string[] captions = { "Логин аккаунта", "Платформа", "Цена за сутки", "Статус" };
            for (int i = 0; i < 4; i++)
                table.Controls.Add(new Label
                {
                    Text = captions[i],
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.BottomLeft,
                    Tag = Skin.Muted
                }, i, 0);

            lblLogin = MakeValue();
            lblPlatform = MakeValue();
            lblPrice = MakeValue();
            lblStatus = MakeValue();
            table.Controls.Add(lblLogin, 0, 1);
            table.Controls.Add(lblPlatform, 1, 1);
            table.Controls.Add(lblPrice, 2, 1);
            table.Controls.Add(lblStatus, 3, 1);

            infoCard.Controls.Add(table);
            infoHost.Controls.Add(infoCard);
            this.Controls.Add(infoHost);

            this.Controls.Add(Theme.MakeHeader("Информация об аккаунте", false));

            Theme.Apply(this);
            this.Load += (s, e) => LoadAccount();
        }

        private static Label MakeValue()
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };
        }

        private void LoadAccount()
        {
            try
            {
                using (var conn = new NpgsqlConnection(ConnStr))
                {
                    conn.Open();

                    using (var cmd = new NpgsqlCommand(
                        "SELECT a.login, a.price_per_day, p.name AS platform, s.name AS status " +
                        "FROM account a " +
                        "JOIN platform p       ON p.id = a.platform_id " +
                        "JOIN account_status s ON s.id = a.status_id " +
                        "WHERE a.id = @id", conn))
                    {
                        cmd.Parameters.Add(new NpgsqlParameter("@id", _accountId));
                        using (var r = cmd.ExecuteReader())
                        {
                            if (!r.Read())
                            {
                                Theme.Warn("Аккаунт не найден.");
                                this.Close();
                                return;
                            }

                            lblLogin.Text = Convert.ToString(r["login"]);
                            lblPlatform.Text = Convert.ToString(r["platform"]);
                            lblPrice.Text = Convert.ToDecimal(r["price_per_day"]).ToString("N2");
                            string status = Convert.ToString(r["status"]);
                            lblStatus.Text = status;
                            Color? c = Theme.StatusColor(status);
                            if (c.HasValue) lblStatus.ForeColor = c.Value;
                        }
                    }

                    flowGames.SuspendLayout();
                    flowGames.Controls.Clear();

                    int count = 0;
                    using (var cmd = new NpgsqlCommand(
                        "SELECT g.title, g.genre, g.cover_url " +
                        "FROM game g JOIN account_game ag ON ag.game_id = g.id " +
                        "WHERE ag.account_id = @id ORDER BY g.title", conn))
                    {
                        cmd.Parameters.Add(new NpgsqlParameter("@id", _accountId));
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                flowGames.Controls.Add(MakeGameCard(
                                    Convert.ToString(r["title"]),
                                    r["genre"] == DBNull.Value ? "" : Convert.ToString(r["genre"]),
                                    r["cover_url"] == DBNull.Value ? "" : Convert.ToString(r["cover_url"])));
                                count++;
                            }
                        }
                    }

                    if (count == 0)
                        flowGames.Controls.Add(new Label
                        {
                            Text = "К этому аккаунту пока не привязано ни одной игры.",
                            AutoSize = true,
                            Margin = new Padding(8, 12, 8, 8),
                            Tag = Skin.Muted
                        });

                    flowGames.ResumeLayout(true);
                    Theme.ApplyTheme(flowGames);   // перекрасить созданные карточки
                }
            }
            catch (Exception ex)
            {
                Theme.Error(ex.Message);
            }
        }

        private Control MakeGameCard(string title, string genre, string coverUrl)
        {
            var card = new Panel
            {
                Width = 220,
                Height = 206,
                Margin = new Padding(8),
                Tag = Skin.Surface
            };

            Bitmap ph = Theme.Placeholder(200, 112);
            var pb = new PictureBox
            {
                Left = 10,
                Top = 10,
                Width = 200,
                Height = 112,
                SizeMode = PictureBoxSizeMode.Zoom,
                InitialImage = ph,
                ErrorImage = ph
            };

            Uri uri;
            if (Uri.TryCreate(coverUrl, UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                try { pb.LoadAsync(uri.ToString()); }
                catch { pb.Image = ph; }
            }
            else
            {
                pb.Image = ph;
            }

            var lblTitle = new Label
            {
                Left = 10,
                Top = 130,
                Width = 200,
                Height = 26,
                Text = title,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                AutoEllipsis = true
            };
            var lblGenre = new Label
            {
                Left = 10,
                Top = 158,
                Width = 200,
                Height = 22,
                Text = genre,
                Tag = Skin.Muted,
                AutoEllipsis = true
            };

            card.Controls.Add(pb);
            card.Controls.Add(lblTitle);
            card.Controls.Add(lblGenre);
            return card;
        }
    }
}
