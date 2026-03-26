using OnlineLibrary1.Models;
using OnlineLibrary1.Pages;
using System;
using System.Collections.Generic;
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

namespace OnlineLibrary1
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindow _mainWindows;

        private void ApplyRoleVisibility()
        {
            bool auth = AppSession.IsAuthenticated;
            bool isAdmin = string.Equals(AppSession.Role, "Администратор", StringComparison.OrdinalIgnoreCase);

            btnMyBooks.Visibility = auth ? Visibility.Visible : Visibility.Collapsed;
            btnProfile.Visibility = auth ? Visibility.Visible : Visibility.Collapsed;
            btnAddBook.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            btnAdmin.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
        }


        public void SetAuthorized(bool isAuthorized)
        {
            btnLogin.Visibility = isAuthorized ? Visibility.Collapsed : Visibility.Visible;
            btnRegister.Visibility = isAuthorized ? Visibility.Collapsed : Visibility.Visible;
            btnLogout.Visibility = isAuthorized ? Visibility.Visible : Visibility.Collapsed;

            ApplyRoleVisibility();
        }
        public MainWindow()
        {
            InitializeComponent();
            ApplyRoleVisibility();
            MainFrame.Navigate(new CatalogPage());
            
        }
        private void NavigateToLogin(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new LoginPage(this));
        }

        private void NavigateToRegister(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new RegistrPage(_mainWindows));
        }

        private void NavigateToCatalog(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new CatalogPage());
        }

        private void NavigateToMyBooks(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new MyBooksPage());
        }

        private void NavigateToAddBook(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new AddBookPage());
        }

        private void NavigateToProfile(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new Profiel());
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Вы вышли из аккаунта");
            AppSession.SignOut();
            SetAuthorized(false);
        }
    }
}
