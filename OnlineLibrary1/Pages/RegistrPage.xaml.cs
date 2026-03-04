using OnlineLibrary1.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    /// Логика взаимодействия для RegistrPage.xaml
    /// </summary>

    public partial class RegistrPage : Page
    {
        private readonly string _cs = ConfigurationManager.ConnectionStrings["bibleoteka"].ConnectionString;
        private readonly MainWindow _mainWindow;

        public RegistrPage(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
        }

        private void Back_Click(object sender, RoutedEventArgs e) => NavigateBack();
        private void Cancel_Click(object sender, RoutedEventArgs e) => NavigateBack();

        private void NavigateBack()
        {
            if (NavigationService != null && NavigationService.CanGoBack)
                NavigationService.GoBack();
            else
                NavigationService?.Navigate(new LoginPage(_mainWindow ?? (Window.GetWindow(this) as MainWindow)));
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new LoginPage(_mainWindow ?? (Window.GetWindow(this) as MainWindow)));
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            ErrorText.Text = "";

            string username = (UserNameBox.Text ?? "").Trim();
            string email = (EmailBox.Text ?? "").Trim();
            string pass = PasswordBox.Password ?? "";
            string confirm = ConfirmPasswordBox.Password ?? "";

            // Валидация
            if (username.Length < 2)
            {
                ShowError("Имя пользователя должно быть минимум 2 символа.");
                return;
            }

            if (!IsValidEmail(email))
            {
                ShowError("Введите корректный Email.");
                return;
            }

            if (pass.Length < 6)
            {
                ShowError("Пароль должен быть минимум 6 символов.");
                return;
            }

            if (pass != confirm)
            {
                ShowError("Пароли не совпадают.");
                return;
            }

            if (!(AgreementCheckBox.IsChecked ?? false))
            {
                ShowError("Нужно принять условия использования.");
                return;
            }



            // Регистрация в БД
            {
                int newUserId;

                using (var con = new SqlConnection(_cs))
                {
                    con.Open();

                    // Проверка на существующий email
                    using (var cmdCheck = new SqlCommand(
                        "SELECT COUNT(*) FROM AuthUsers WHERE Email = @e", con))
                    {
                        cmdCheck.Parameters.AddWithValue("@e", email);
                        int exists = Convert.ToInt32(cmdCheck.ExecuteScalar() ?? 0);
                        if (exists > 0)
                        {
                            ShowError("Пользователь с таким Email уже существует.");
                            return;
                        }
                    }

                    // Создаём пользователя (RolesId = 2 -> Читатель)
                    using (var cmdUser = new SqlCommand(
                        "INSERT INTO Users (Username, Created, Email, RolesId) " +
                        "VALUES (@u, GETDATE(), @e, 2); " +
                        "SELECT CAST(SCOPE_IDENTITY() AS INT);", con))
                    {
                        cmdUser.Parameters.AddWithValue("@u", username);
                        cmdUser.Parameters.AddWithValue("@e", email);
                        newUserId = Convert.ToInt32(cmdUser.ExecuteScalar());
                    }

                    // Создаём запись для входа (пароль хешируем как в твоём LoginPage)
                    using (var cmdAuth = new SqlCommand(
                        "INSERT INTO AuthUsers (AuthUsersId, [Password], Email) " +
                        "VALUES (@id, HASHBYTES('SHA2_256', @p), @e);", con))
                    {
                        cmdAuth.Parameters.AddWithValue("@id", newUserId);
                        cmdAuth.Parameters.AddWithValue("@p", pass);
                        cmdAuth.Parameters.AddWithValue("@e", email);
                        cmdAuth.ExecuteNonQuery();
                    }
                }

                // Сессия + UI главного окна
                AppSession.SignIn(newUserId, username, email, "Читатель", DateTime.Now);
                (_mainWindow ?? (Window.GetWindow(this) as MainWindow))?.SetAuthorized(true);

                MessageBox.Show("Регистрация успешна!");
                NavigationService?.Navigate(new Profiel());
            }
           
           
        }

        private void ShowError(string text)
        {
            ErrorText.Text = text;
            ErrorText.Visibility = Visibility.Visible;
        }

        private bool IsValidEmail(string email)
        {
            var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(email ?? "", pattern);
        }
    }

}
