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
    public partial class AdminPage : Page
    {
        
        
            private readonly string _cs = ConfigurationManager.ConnectionStrings["bibleoteka"].ConnectionString;
            private List<AdminUserItem> _allUsers = new List<AdminUserItem>();
            private List<BookItem> _allBooks = new List<BookItem>();

            public AdminPage()
            {
                InitializeComponent();
                Loaded += AdminPage_Loaded;
            }

            private void AdminPage_Loaded(object sender, RoutedEventArgs e)
            {
                if (!string.Equals(AppSession.Role, "Администратор", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Доступ только для администратора.");
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    mainWindow?.MainFrame.Navigate(new CatalogPage());
                    return;
                }

                LoadUsers();
                LoadBooks();
            }

            private void LoadUsers()
            {
                const string sql = @"
                                    SELECT u.UsersId, u.Username, u.Email, u.Created, ISNULL(r.RoleName, N'—') AS RoleName
                                    FROM Users u
                                    LEFT JOIN Roles r ON r.RolesId = u.RolesId
                                    ORDER BY u.UsersId;";

                var list = new List<AdminUserItem>();

                using (var con = new SqlConnection(_cs))
                using (var cmd = new SqlCommand(sql, con))
                {
                    con.Open();
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new AdminUserItem
                            {
                                UserId = Convert.ToInt32(r["UsersId"]),
                                Username = r["Username"]?.ToString() ?? "",
                                Email = r["Email"]?.ToString() ?? "",
                                RoleName = r["RoleName"]?.ToString() ?? "",
                                Created = r["Created"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["Created"])
                            });
                        }
                    }
                }

                _allUsers = list;
                ApplyUserFilter();
            }

            private void LoadBooks()
            {
                const string sql = @"
                                    SELECT
                                        b.BookId,
                                        b.[Name] AS Title,
                                        LTRIM(RTRIM(CONCAT(a.LastName, ' ', a.FirstName, ' ', ISNULL(NULLIF(a.MidleName,''), '')))) AS Author,
                                        ISNULL(b.PublicationYear, 0) AS [Year],
                                        ISNULL(genres.GenreList, N'') AS Genre
                                    FROM Book b
                                    INNER JOIN Author a ON a.AuthorId = b.AuthorId
                                    OUTER APPLY (
                                        SELECT STUFF((
                                            SELECT N', ' + g2.GenreName
                                            FROM GenreBook gb2
                                            INNER JOIN Genre g2 ON g2.GenreId = gb2.GenreId
                                            WHERE gb2.BookId = b.BookId
                                            ORDER BY g2.GenreName
                                            FOR XML PATH(''), TYPE
                                        ).value('.', 'nvarchar(max)'), 1, 2, N'') AS GenreList
                                    ) genres
                                    ORDER BY b.BookId DESC;";

                var list = new List<BookItem>();

                using (var con = new SqlConnection(_cs))
                using (var cmd = new SqlCommand(sql, con))
                {
                    con.Open();
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new BookItem
                            {
                                BookId = Convert.ToInt32(r["BookId"]),
                                Title = r["Title"]?.ToString() ?? "",
                                Author = r["Author"]?.ToString() ?? "",
                                Year = Convert.ToInt32(r["Year"]),
                                Genre = r["Genre"]?.ToString() ?? ""
                            });
                        }
                    }
                }

                _allBooks = list;
                ApplyBookFilter();
            }

            private void ApplyUserFilter()
            {
                var q = (UserSearchTextBox.Text ?? "").Trim().ToLowerInvariant();
                var filtered = string.IsNullOrWhiteSpace(q)
                    ? _allUsers
                    : _allUsers.Where(u =>
                        (u.Username ?? "").ToLowerInvariant().Contains(q) ||
                        (u.Email ?? "").ToLowerInvariant().Contains(q) ||
                        (u.RoleName ?? "").ToLowerInvariant().Contains(q) ||
                        u.UserId.ToString().Contains(q)).ToList();

                UsersGrid.ItemsSource = filtered;
            }

            private void ApplyBookFilter()
            {
                var q = (BookSearchTextBox.Text ?? "").Trim().ToLowerInvariant();
                var filtered = string.IsNullOrWhiteSpace(q)
                    ? _allBooks
                    : _allBooks.Where(b =>
                        (b.Title ?? "").ToLowerInvariant().Contains(q) ||
                        (b.Author ?? "").ToLowerInvariant().Contains(q) ||
                        (b.Genre ?? "").ToLowerInvariant().Contains(q) ||
                        b.BookId.ToString().Contains(q)).ToList();

                BooksGrid.ItemsSource = filtered;
            }

            private AdminUserItem GetSelectedUser()
            {
                var user = UsersGrid.SelectedItem as AdminUserItem;
                if (user == null)
                    MessageBox.Show("Выберите пользователя.");
                return user;
            }

            private BookItem GetSelectedBook()
            {
                var book = BooksGrid.SelectedItem as BookItem;
                if (book == null)
                    MessageBox.Show("Выберите книгу.");
                return book;
            }

            private void PromoteUser_Click(object sender, RoutedEventArgs e)
            {
                ChangeUserRole(1, "выдать админку");
            }

            private void DemoteUser_Click(object sender, RoutedEventArgs e)
            {
                ChangeUserRole(2, "снять админку");
            }

            private void ChangeUserRole(int roleId, string actionText)
            {
                var user = GetSelectedUser();
                if (user == null) return;

                if (AppSession.UserId == user.UserId && roleId != 1)
                {
                    MessageBox.Show("Нельзя снять админку у самого себя, пока вы в этом аккаунте.");
                    return;
                }

                var result = MessageBox.Show($"Вы действительно хотите {actionText} пользователю \"{user.Username}\"?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                using (var con = new SqlConnection(_cs))
                using (var cmd = new SqlCommand("UPDATE Users SET RolesId = @roleId WHERE UsersId = @id", con))
                {
                    cmd.Parameters.AddWithValue("@roleId", roleId);
                    cmd.Parameters.AddWithValue("@id", user.UserId);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }

                LoadUsers();
            }

            private void DeleteUser_Click(object sender, RoutedEventArgs e)
            {
                var user = GetSelectedUser();
                if (user == null) return;

                if (AppSession.UserId == user.UserId)
                {
                    MessageBox.Show("Нельзя удалить самого себя из активной сессии.");
                    return;
                }

                var result = MessageBox.Show(
                    $"Удалить пользователя \"{user.Username}\" и все его данные?\n\nБудут удалены:\n- аккаунт\n- пароль/авторизация\n- избранное\n- аватарка",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;

                using (var con = new SqlConnection(_cs))
                {
                    con.Open();
                    using (var tx = con.BeginTransaction())
                    {
                        try
                        {
                            ExecuteNonQuery(con, tx, "DELETE FROM Favorites WHERE UserId = @id", user.UserId);
                            ExecuteNonQuery(con, tx, "UPDATE Users SET Avatar = NULL WHERE UsersId = @id", user.UserId);
                            ExecuteNonQuery(con, tx, "DELETE FROM AuthUsers WHERE AuthUsersId = @id", user.UserId);
                            ExecuteNonQuery(con, tx, "DELETE FROM Users WHERE UsersId = @id", user.UserId);

                            tx.Commit();
                        }
                        catch (Exception ex)
                        {
                            tx.Rollback();
                            MessageBox.Show("Не удалось удалить пользователя.\n" + ex.Message);
                            return;
                        }
                    }
                }

                LoadUsers();
            }

            private void DeleteBook_Click(object sender, RoutedEventArgs e)
            {
                var book = GetSelectedBook();
                if (book == null) return;

                var result = MessageBox.Show(
                    $"Удалить книгу \"{book.Title}\"?\n\nБудут удалены:\n- сама книга\n- файл книги\n- обложка\n- жанры\n- язык\n- издательство\n- все записи из избранного",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;

                using (var con = new SqlConnection(_cs))
                {
                    con.Open();
                    using (var tx = con.BeginTransaction())
                    {
                        try
                        {
                            ExecuteNonQuery(con, tx, "DELETE FROM Favorites WHERE BookId = @id", book.BookId);
                            ExecuteNonQuery(con, tx, "DELETE FROM Covers WHERE BookId = @id", book.BookId);
                            ExecuteNonQuery(con, tx, "DELETE FROM GenreBook WHERE BookId = @id", book.BookId);
                            ExecuteNonQuery(con, tx, "DELETE FROM Languages WHERE BookId = @id", book.BookId);
                            ExecuteNonQuery(con, tx, "DELETE FROM PublishingHouse WHERE BookId = @id", book.BookId);
                            ExecuteNonQuery(con, tx, "DELETE FROM Book WHERE BookId = @id", book.BookId);

                            tx.Commit();
                        }
                        catch (Exception ex)
                        {
                            tx.Rollback();
                            MessageBox.Show("Не удалось удалить книгу.\n" + ex.Message);
                            return;
                        }
                    }
                }

                LoadBooks();
            }

            private void ExecuteNonQuery(SqlConnection con, SqlTransaction tx, string sql, int id)
            {
                using (var cmd = new SqlCommand(sql, con, tx))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            private void RefreshUsers_Click(object sender, RoutedEventArgs e) => LoadUsers();
            private void RefreshBooks_Click(object sender, RoutedEventArgs e) => LoadBooks();
            private void UserSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyUserFilter();
            private void BookSearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyBookFilter();
        
    }
}
