using OnlineLibrary1.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
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
    /// Логика взаимодействия для LoginPage.xaml
    /// </summary>
    public partial class LoginPage : Page
    {
        private MainWindow _menupage;
        private MainWindow _mainpage;
        public LoginPage(MainWindow menupage)
        {
            InitializeComponent();
            _menupage = menupage;

        }
        private void Login_Click(object sender, RoutedEventArgs e)
        {
           
            string login = EmailBox.Text;
            string password = PasswordBox.Text;
            string connectionString = ConfigurationManager.ConnectionStrings["bibleoteka"].ConnectionString;
            
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT TOP (1) u.UsersId, u.Username, u.Email,  r.RoleName,  u.Created FROM dbo.Users AS u Join dbo.AuthUsers as au on u.UsersId=au.AuthUsersId join dbo.Roles as r  on u.UsersId=r.RolesId WHERE au.Email = @login  AND [Password] = HASHBYTES('SHA2_256', @password);";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@login", login);
                    command.Parameters.AddWithValue("@password", password);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int userId = Convert.ToInt32(reader["UsersId"]);
                            string username = reader["Username"]?.ToString() ?? "";
                            string email = reader["Email"]?.ToString() ?? "";
                            string role = reader["RoleName"]?.ToString() ?? "";
                            DateTime? created = reader["Created"] == DBNull.Value
                                ? (DateTime?)null
                                : Convert.ToDateTime(reader["Created"]);

                            AppSession.SignIn(userId, username, email, role, created);

                            _menupage.SetAuthorized(true);
                            MessageBox.Show("Успешный вход!");
                            NavigationService.Navigate(new CatalogPage());
                        }
                        else
                        {
                            MessageBox.Show("Неверный пароль или почта!");
                        }
                    }
                }
            }

        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new RegistrPage(_mainpage));
        }
    }

}
