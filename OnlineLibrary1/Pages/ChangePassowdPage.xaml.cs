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
    /// Логика взаимодействия для ChangePassowdPage.xaml
    /// </summary>
    public partial class ChangePassowdPage : Page
    {
        private readonly string _cs = ConfigurationManager.ConnectionStrings["bibleoteka"].ConnectionString;

        public ChangePassowdPage()
        {
            InitializeComponent();
            Loaded += ChangePassowdPage_Loaded;
        }

        private void ChangePassowdPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!AppSession.IsAuthenticated)
            {
                NotAuthPanel.Visibility = Visibility.Visible;
                SubtitleText.Text = "Смена пароля недоступна";
                SetFormEnabled(false);
                return;
            }

            NotAuthPanel.Visibility = Visibility.Collapsed;
            SetFormEnabled(true);
        }

        private void SetFormEnabled(bool enabled)
        {
            OldPassBox.IsEnabled = enabled;
            NewPassBox.IsEnabled = enabled;
            ConfirmPassBox.IsEnabled = enabled;
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

            string oldPass = OldPassBox.Password ?? "";
            string newPass = NewPassBox.Password ?? "";
            string confirm = ConfirmPassBox.Password ?? "";

            if (string.IsNullOrWhiteSpace(oldPass))
            {
                ShowError("Введите текущий пароль.");
                return;
            }

            if (newPass.Length < 6)
            {
                ShowError("Новый пароль должен быть минимум 6 символов.");
                return;
            }

            if (newPass != confirm)
            {
                ShowError("Новый пароль и подтверждение не совпадают.");
                return;
            }

            try
            {
                int affected;
                using (var con = new SqlConnection(_cs))
                using (var cmd = new SqlCommand(
                    "UPDATE AuthUsers " +
                    "SET Password = HASHBYTES('SHA2_256', @new) " +
                    "WHERE AuthUsersId = @id AND Password = HASHBYTES('SHA2_256', @old)", con))
                {
                    cmd.Parameters.AddWithValue("@new", newPass);
                    cmd.Parameters.AddWithValue("@old", oldPass);
                    cmd.Parameters.AddWithValue("@id", AppSession.UserId.Value);

                    con.Open();
                    affected = cmd.ExecuteNonQuery();
                }

                if (affected == 0)
                {
                    ShowError("Текущий пароль неверный.");
                    return;
                }

                MessageBox.Show("Пароль успешно изменён!");
                OldPassBox.Clear();
                NewPassBox.Clear();
                ConfirmPassBox.Clear();
                NavigateBack();
            }
            catch (Exception ex)
            {
                ShowError("Не удалось изменить пароль. " + ex.Message);
            }
        }

        private void ShowError(string text)
        {
            ErrorText.Text = text;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
