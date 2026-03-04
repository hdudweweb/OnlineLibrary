using OnlineLibrary1.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
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
    /// Логика взаимодействия для EditProfile.xaml
    /// </summary>
    public partial class EditProfile : Page
    {
        private readonly string _cs = ConfigurationManager.ConnectionStrings["bibleoteka"].ConnectionString;

        public EditProfile()
        {
            InitializeComponent();
            Loaded += EditProfile_Loaded;
        }

        private void EditProfile_Loaded(object sender, RoutedEventArgs e)
        {
            if (!AppSession.IsAuthenticated)
            {
                NotAuthPanel.Visibility = Visibility.Visible;
                SubtitleText.Text = "Редактирование недоступно";
                SetFormEnabled(false);
                return;
            }

            NotAuthPanel.Visibility = Visibility.Collapsed;
            SetFormEnabled(true);

            UsernameBox.Text = AppSession.Username ?? "";
            EmailBox.Text = AppSession.Email ?? "";
        }

        private void SetFormEnabled(bool enabled)
        {
            UsernameBox.IsEnabled = enabled;
            EmailBox.IsEnabled = enabled;
        }
        private void Cancel_Click(object sender, RoutedEventArgs e) => NavigateBack();

        private void NavigateBack()
        {
            if (NavigationService != null && NavigationService.CanGoBack)
                NavigationService.GoBack();
            else
                NavigationService?.Navigate(new Profiel());
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            ErrorText.Text = "";

            if (!AppSession.IsAuthenticated || !AppSession.UserId.HasValue)
            {
                MessageBox.Show("Сначала выполните вход.");
                NavigateBack();
                return;
            }

            string newUsername = (UsernameBox.Text ?? "").Trim();
            string newEmail = (EmailBox.Text ?? "").Trim();

            if (newUsername.Length < 2)
            {
                ShowError("Имя пользователя слишком короткое (минимум 2 символа).");
                return;
            }

            if (string.IsNullOrWhiteSpace(newEmail) || !newEmail.Contains("@") || !newEmail.Contains("."))
            {
                ShowError("Введите корректный Email.");
                return;
            }

            try
            {
                using (var con = new SqlConnection(_cs))
                {
                    con.Open();

                    // Users
                    using (var cmd = new SqlCommand(
                        "UPDATE Users SET Username = @u, Email = @e WHERE UsersId = @id", con))
                    {
                        cmd.Parameters.AddWithValue("@u", newUsername);
                        cmd.Parameters.AddWithValue("@e", newEmail);
                        cmd.Parameters.AddWithValue("@id", AppSession.UserId.Value);
                        cmd.ExecuteNonQuery();
                    }

                    // AuthUsers (Email для входа)
                    using (var cmd2 = new SqlCommand(
                        "UPDATE AuthUsers SET Email = @e WHERE AuthUsersId = @id", con))
                    {
                        cmd2.Parameters.AddWithValue("@e", newEmail);
                        cmd2.Parameters.AddWithValue("@id", AppSession.UserId.Value);
                        cmd2.ExecuteNonQuery();
                    }
                }

                // Обновим сессию
                AppSession.SignIn(
                    AppSession.UserId.Value,
                    newUsername,
                    newEmail,
                    AppSession.Role,
                    AppSession.CreatedAt
                );

                MessageBox.Show("Профиль обновлён!");
                NavigateBack();
            }
            catch (Exception ex)
            {
                ShowError("Не удалось сохранить изменения. " + ex.Message);
            }
        }

        private void ShowError(string text)
        {
            ErrorText.Text = text;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
