using System;
using System.Collections.Generic;
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
        private MainWindow _mainWindow;
        public RegistrPage(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = new MainWindow();
        }
        private void Register_Click(object sender, RoutedEventArgs e)
        {
            // Валидация
            if (string.IsNullOrWhiteSpace(FirstNameBox.Text) ||
                
                string.IsNullOrWhiteSpace(EmailBox.Text) ||
                string.IsNullOrWhiteSpace(PasswordBox.Text))
            {
                MessageBox.Show("Заполните все обязательные поля");
                return;
            }

            if (PasswordBox.Text != ConfirmPasswordBox.Text)
            {
                MessageBox.Show("Пароли не совпадают");
                return;
            }

            if (!IsValidEmail(EmailBox.Text))
            {
                MessageBox.Show("Введите корректный email");
                return;
            }

            if (!AgreementCheckBox.IsChecked ?? false)
            {
                MessageBox.Show("Примите условия использования");
                return;
            }

            // Сохранение в файл
            try
            {
                var userData = $"{FirstNameBox.Text} |{EmailBox.Text}|{PasswordBox.Text}|{System.DateTime.Now:dd.MM.yyyy}";

                if (File.Exists("users.txt"))
                {
                    File.AppendAllText("users.txt", "\n" + userData);
                }
                else
                {
                    File.WriteAllText("users.txt", userData);
                }

                // Создание профиля
                var profileData = $"{FirstNameBox.Text} |{EmailBox.Text}|{System.DateTime.Now:dd.MM.yyyy}";
                File.WriteAllText("profile.txt", profileData);

                MessageBox.Show("Регистрация успешна!");

                // Переход на страницу профиля
                NavigationService?.Navigate(new Profiel());

                // Показать кнопку выхода в главном окне
                var mainWindow = Window.GetWindow(this) as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.btnLogout.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                MessageBox.Show("Ошибка при регистрации");
            }
        }

        private bool IsValidEmail(string email)
        {
            var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(email, pattern);
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new LoginPage(_mainWindow));
        }
    }

}
