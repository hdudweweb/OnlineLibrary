using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Contexts;
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
using OnlineLibrary1.Models;
namespace OnlineLibrary1.Pages
{
    /// <summary>
    /// Логика взаимодействия для MyBooksPage.xaml
    /// </summary>
    public partial class MyBooksPage : Page
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["bibleoteka"].ConnectionString;

        // TODO: заменить на текущего пользователя после логина
        private const int CurrentUserId = 1;

        private List<MyBookItem> all = new List<MyBookItem>();
        private List<MyBookItem> filtered = new List<MyBookItem>();

        public MyBooksPage()
        {
            InitializeComponent();
            StatusFilterComboBox.SelectedIndex = 0;
            LoadMyBooks();
            ApplyFilter();
        }

        private void LoadMyBooks()
        {
            try
            {
                const string sql = @"
                    SELECT
                        b.BookId,
                        b.[Name] AS Title,
                        LTRIM(RTRIM(CONCAT(a.LastName, ' ', a.FirstName, ' ', ISNULL(NULLIF(a.MidleName,''), '')))) AS Author,
                        ISNULL(b.PublicationYear, 0) AS [Year],
                        ISNULL(g.GenreName, N'') AS Genre,
                        ISNULL(b.TotalPages, 0) AS Pages,
                        ISNULL(b.DescriptionBook, N'') AS [Description],
                        f.Status,
                        c.CoverBytes
                    FROM Favorites f
                    INNER JOIN Book b ON b.BookId = f.BookId
                    INNER JOIN Author a ON a.AuthorId = b.AuthorId
                    LEFT JOIN GenreBook gb ON gb.BookId = b.BookId
                    LEFT JOIN Genre g ON g.GenreId = gb.GenreId
                    OUTER APPLY (
                        SELECT TOP 1 Cover AS CoverBytes
                        FROM Covers
                        WHERE BookId = b.BookId
                        ORDER BY CoversId DESC
                    ) c
                    WHERE f.UserId = @userId
                    ORDER BY f.AddedAt DESC;";

                var list = new List<MyBookItem>();

                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", CurrentUserId);

                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var item = new MyBookItem
                            {
                                BookId = Convert.ToInt32(r["BookId"]),
                                Title = r["Title"]?.ToString() ?? "",
                                Author = r["Author"]?.ToString() ?? "",
                                Year = Convert.ToInt32(r["Year"]),
                                Genre = r["Genre"]?.ToString() ?? "",
                                Pages = Convert.ToInt32(r["Pages"]),
                                Description = r["Description"]?.ToString() ?? "",
                                CoverBytes = r["CoverBytes"] == DBNull.Value ? null : (byte[])r["CoverBytes"],
                                Status = r["Status"]?.ToString() ?? "В планах",
                            };

                            item.CoverImage = item.CoverBytes != null && item.CoverBytes.Length > 0
                                ? ByteArrayToBitmapImage(item.CoverBytes)
                                : null;

                            list.Add(item);
                        }
                    }
                }

                all = list;
                filtered = new List<MyBookItem>(all);

                BooksList.ItemsSource = filtered;
                CountText.Text = $"{filtered.Count} книг";
                NoResultsPanel.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки 'Мои книги' из БД.\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            var q = (SearchTextBox.Text ?? "").Trim().ToLower();

            if (string.IsNullOrWhiteSpace(q))
                filtered = new List<MyBookItem>(all);
            else
                filtered = all.Where(x =>
                        (x.Title ?? "").ToLower().Contains(q) ||
                        (x.Author ?? "").ToLower().Contains(q))
                    .ToList();

            BooksList.ItemsSource = filtered;
            CountText.Text = $"{filtered.Count} книг";
            NoResultsPanel.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // --- UI handlers ---

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
            ApplyFilter();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadMyBooks();
            ApplyFilter();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.MainFrame?.Navigate(new BoolDetailsPage(id));
            }
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is int id)) return;

            var res = MessageBox.Show("Удалить книгу из моих книг (из избранного)?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand(
                           "DELETE FROM Favorites WHERE UserId = @userId AND BookId = @bookId", conn))
                {
                    cmd.Parameters.AddWithValue("@userId", CurrentUserId);
                    cmd.Parameters.AddWithValue("@bookId", id);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                LoadMyBooks();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось удалить.\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearFavorites_Click(object sender, RoutedEventArgs e)
        {
            if (all.Count == 0) return;

            var res = MessageBox.Show("Очистить мои книги (удалить все избранные у текущего пользователя)?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (res != MessageBoxResult.Yes) return;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand("DELETE FROM Favorites WHERE UserId = @userId", conn))
                {
                    cmd.Parameters.AddWithValue("@userId", CurrentUserId);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                LoadMyBooks();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось очистить.\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- helpers ---

        private static BitmapImage ByteArrayToBitmapImage(byte[] bytes)
        {
            try
            {
                using (var ms = new MemoryStream(bytes))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms;
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
            }
            catch
            {
                return null;
            }
        }

        private void StatusFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatusFilterComboBox.SelectedItem == null)
                return;

            string selectedStatus =
                (StatusFilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все";

            if (selectedStatus == "Все")
            {
                BooksList.ItemsSource = all;
                CountText.Text = $"{all.Count} книг";
                return;
            }

            var filtered = all
                .Where(x => string.Equals(x.Status, selectedStatus, StringComparison.OrdinalIgnoreCase))
                .ToList();

            BooksList.ItemsSource = filtered;
            CountText.Text = $"{filtered.Count} книг";
        }
    }
}

