using Microsoft.Win32;
using OnlineLibrary1.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OnlineLibrary1.Pages
{
    /// <summary>
    /// Логика взаимодействия для Profiel.xaml
    /// </summary>
    public partial class Profiel : Page
    {
        private readonly string _cs = ConfigurationManager.ConnectionStrings["bibleoteka"].ConnectionString;
        private int? _userId;

        public Profiel()
        {
            InitializeComponent();
            Loaded += Profiel_Loaded;
        }

        private void Profiel_Loaded(object sender, RoutedEventArgs e)
        {
            ReloadAll();
        }

        private void ReloadAll()
        {
            LoadProfile();
            LoadAvatarFromDisk();
            LoadStats();
        }

        private void LoadProfile()
        {
            _userId = AppSession.UserId;

            if (!AppSession.IsAuthenticated)
            {
                NotAuthPanel.Visibility = Visibility.Visible;
                SubtitleText.Text = "Данные гостя (для полного доступа выполните вход)";

                
                TryLoadProfileFromFile();
                return;
            }

            NotAuthPanel.Visibility = Visibility.Collapsed;
            SubtitleText.Text = "Данные вашего аккаунта";

            // Из сессии
            //UsernameText.Text = string.IsNullOrWhiteSpace(AppSession.Username) ? "—" : AppSession.Username;
            //EmailText.Text = string.IsNullOrWhiteSpace(AppSession.Email) ? "—" : AppSession.Email;
            //RoleText.Text = string.IsNullOrWhiteSpace(AppSession.Role) ? "—" : AppSession.Role;
            //CreatedText.Text = AppSession.CreatedAt.HasValue ? AppSession.CreatedAt.Value.ToString("dd.MM.yyyy") : "—";

            // Из БД 
            if (_userId.HasValue)
            {
                try
                {
                    using (var con = new SqlConnection(_cs))
                    using (var cmd = new SqlCommand(
                        "SELECT u.Username, u.Email, r.RoleName, u.Created " +
                        "FROM Users u " +
                        "LEFT JOIN Roles r ON r.RolesId = u.RolesId " +
                        "WHERE u.UsersId = @id", con))
                    {
                        cmd.Parameters.AddWithValue("@id", _userId.Value);
                        con.Open();

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                UsernameText.Text = reader["Username"]?.ToString() ?? "—";
                                EmailText.Text = reader["Email"]?.ToString() ?? "—";
                                RoleText.Text = reader["RoleName"]?.ToString() ?? "—";

                                if (reader["Created"] != DBNull.Value)
                                {
                                    var created = Convert.ToDateTime(reader["Created"], CultureInfo.InvariantCulture);
                                    CreatedText.Text = created.ToString("dd.MM.yyyy");
                                }
                            }
                        }
                    }
                }
                catch
                {
                   
                }
            }
        }

        private void TryLoadProfileFromFile()
        {
            try
            {
                if (!File.Exists("profile.txt"))
                {
                    UsernameText.Text = "Гость";
                    EmailText.Text = "—";
                    RoleText.Text = "—";
                    CreatedText.Text = "—";
                    return;
                }

                var raw = File.ReadAllText("profile.txt");
                var parts = raw.Split('|');

                // Формат из RegistrPage: "Имя |email|dd.MM.yyyy"
                var name = parts.Length > 0 ? parts[0].Trim() : "Гость";
                var email = parts.Length > 1 ? parts[1].Trim() : "—";
                var created = parts.Length > 2 ? parts[2].Trim() : "—";

                UsernameText.Text = string.IsNullOrWhiteSpace(name) ? "Гость" : name;
                EmailText.Text = string.IsNullOrWhiteSpace(email) ? "—" : email;
                RoleText.Text = "Читатель";
                CreatedText.Text = string.IsNullOrWhiteSpace(created) ? "—" : created;
            }
            catch
            {
                UsernameText.Text = "Гость";
                EmailText.Text = "—";
                RoleText.Text = "—";
                CreatedText.Text = "—";
            }
        }

        private void LoadStats()
        {
            TotalBooksText.Text = "—";
            ReadingBooksText.Text = "—";
            PlannedBooksText.Text = "—";
            FinishedBooksText.Text = "—";
            StatsHintText.Visibility = Visibility.Collapsed;

            if (!_userId.HasValue)
            {
                StatsHintText.Text = "Войдите в аккаунт, чтобы видеть статистику.";
                StatsHintText.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                using (var con = new SqlConnection(_cs))
                {
                    con.Open();

                    // Всего
                    using (var cmdTotal = new SqlCommand("SELECT COUNT(*) FROM Favorites WHERE UserId = @id", con))
                    {
                        cmdTotal.Parameters.AddWithValue("@id", _userId.Value);
                        TotalBooksText.Text = Convert.ToInt32(cmdTotal.ExecuteScalar() ?? 0).ToString();
                    }

                    // Есть ли поле Status
                    bool hasStatus;
                    using (var cmdHas = new SqlCommand(
                        "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Favorites' AND COLUMN_NAME='Status'", con))
                    {
                        hasStatus = Convert.ToInt32(cmdHas.ExecuteScalar() ?? 0) > 0;
                    }

                    if (!hasStatus)
                    {
                        StatsHintText.Text = "В таблице Favorites нет поля Status (Читаю/В планах/Прочитано).";
                        StatsHintText.Visibility = Visibility.Visible;
                        return;
                    }

                    // По статусам
                    using (var cmd = new SqlCommand(
                        "SELECT Status, COUNT(*) AS Cnt FROM Favorites WHERE UserId = @id GROUP BY Status", con))
                    {
                        cmd.Parameters.AddWithValue("@id", _userId.Value);
                        using (var reader = cmd.ExecuteReader())
                        {
                            int reading = 0, planned = 0, finished = 0;

                            while (reader.Read())
                            {
                                var status = reader["Status"]?.ToString() ?? "";
                                var cnt = reader["Cnt"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Cnt"]);

                                if (string.Equals(status, "Читаю", StringComparison.OrdinalIgnoreCase))
                                    reading = cnt;
                                else if (string.Equals(status, "В планах", StringComparison.OrdinalIgnoreCase))
                                    planned = cnt;
                                else if (string.Equals(status, "Прочитано", StringComparison.OrdinalIgnoreCase))
                                    finished = cnt;
                            }

                            ReadingBooksText.Text = reading.ToString();
                            PlannedBooksText.Text = planned.ToString();
                            FinishedBooksText.Text = finished.ToString();
                        }
                    }
                }
            }
            catch
            {
                StatsHintText.Text = "Не удалось загрузить статистику (нет подключения к БД).";
                StatsHintText.Visibility = Visibility.Visible;
            }
        }

        private string GetAvatarPath()
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OnlineLibrary1");

            Directory.CreateDirectory(dir);

            var key = _userId.HasValue ? _userId.Value.ToString() : "guest";
            return System.IO.Path.Combine(dir, $"avatar_{key}.png");
        }

        private void LoadAvatarFromDisk()
        {
            try
            {
                var path = GetAvatarPath();
                if (!File.Exists(path))
                {
                    SetAvatar(null);
                    return;
                }

                var bytes = File.ReadAllBytes(path);
                SetAvatar(bytes);
            }
            catch
            {
                SetAvatar(null);
            }
        }

        private void SetAvatar(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                AvatarImage.Source = null;
                AvatarPlaceholder.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(bytes))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                }

                AvatarImage.Source = bmp;
                AvatarPlaceholder.Visibility = Visibility.Collapsed;
            }
            catch
            {
                AvatarImage.Source = null;
                AvatarPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void UploadAvatar_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Изображения (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Title = "Выберите фото профиля"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                var bytes = File.ReadAllBytes(dlg.FileName);
                SetAvatar(bytes);

                // Сохраняем локально (без БД)
                File.WriteAllBytes(GetAvatarPath(), bytes);
            }
            catch
            {
                MessageBox.Show("Не удалось загрузить изображение.");
            }
        }

        private void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!AppSession.IsAuthenticated)
            {
                MessageBox.Show("Сначала выполните вход в аккаунт.");
                return;
            }

            NavigationService?.Navigate(new EditProfile());
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            if (!AppSession.IsAuthenticated)
            {
                MessageBox.Show("Сначала выполните вход в аккаунт.");
                return;
            }

            NavigationService?.Navigate(new ChangePassowdPage());
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ReloadAll();
        }
    }
}

