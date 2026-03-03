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
    /// Логика взаимодействия для BoolDetailsPage.xaml
    /// </summary>
    public partial class BoolDetailsPage : Page
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["bibleoteka"].ConnectionString;

        private readonly int bookId;
        private bool isInMyBooks = false;
        private string currentStatus = "";

        private byte[] coverBytes;
        private string genreForIcon = "";

        public BoolDetailsPage(int bookId)
        {
            InitializeComponent();
            this.bookId = bookId;

            LoadBookDetailsFromDb();
            CheckIfInMyBooks();
        }

        private void LoadBookDetailsFromDb()
        {
            try
            {
                const string sql = @"SELECT b.BookId, b.[Name] AS Title, LTRIM(RTRIM(CONCAT(a.LastName, ' ', a.FirstName, ' ', ISNULL(NULLIF(a.MidleName,''), '')))) AS Author,
                        ISNULL(b.PublicationYear, 0) AS [Year],
                        ISNULL(b.TotalPages, 0) AS Pages,
                        ISNULL(g.GenreName, N'') AS Genre,
                        ISNULL(ag.AgeName, N'') AS AgeRating,
                        ISNULL(b.ISBN, N'') AS ISBN,
                        ISNULL(b.DescriptionBook, N'') AS [Description],
                        lang.NameLang AS [Language],
                        pub.NamePublish AS Publisher,
                        c.CoverBytes,
                        (SELECT COUNT(*) FROM Favorites f WHERE f.BookId = b.BookId) AS FavoritesCount
                    FROM Book b
                    INNER JOIN Author a ON a.AuthorId = b.AuthorId
                    LEFT JOIN Age ag ON ag.AgeId = b.AgeId
                    LEFT JOIN GenreBook gb ON gb.BookId = b.BookId
                    LEFT JOIN Genre g ON g.GenreId = gb.GenreId
                    OUTER APPLY (
                        SELECT TOP 1 NameLang
                        FROM Languages
                        WHERE BookId = b.BookId
                        ORDER BY LanguagesId DESC
                    ) lang
                    OUTER APPLY (
                        SELECT TOP 1 NamePublish
                        FROM PublishingHouse
                        WHERE BookId = b.BookId
                        ORDER BY PublishingHouse DESC
                    ) pub
                    OUTER APPLY (
                        SELECT TOP 1 Cover AS CoverBytes
                        FROM Covers
                        WHERE BookId = b.BookId
                        ORDER BY CoversId DESC
                    ) c
                    WHERE b.BookId = @id;";

                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", bookId);
                    conn.Open();

                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                        {
                            MessageBox.Show("Книга не найдена в базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        var title = r["Title"]?.ToString() ?? "";
                        var author = r["Author"]?.ToString() ?? "";
                        var year = Convert.ToInt32(r["Year"]);
                        var pages = Convert.ToInt32(r["Pages"]);
                        var genre = r["Genre"]?.ToString() ?? "";
                        var age = r["AgeRating"]?.ToString() ?? "";
                        var isbn = r["ISBN"]?.ToString() ?? "";
                        var desc = r["Description"]?.ToString() ?? "";
                        var lang = r["Language"] == DBNull.Value ? "—" : r["Language"]?.ToString();
                        var publisher = r["Publisher"] == DBNull.Value ? "—" : r["Publisher"]?.ToString();
                        var favCount = Convert.ToInt32(r["FavoritesCount"]);
                        coverBytes = r["CoverBytes"] == DBNull.Value ? null : (byte[])r["CoverBytes"];

                        TitleText.Text = title;
                        AuthorText.Text = author;
                        YearText.Text = year.ToString();
                        PagesText.Text = pages.ToString();
                        GenreText.Text = string.IsNullOrWhiteSpace(genre) ? "—" : genre;
                        genreForIcon = genre;

                        // Рейтинг — в БД поля нет, поэтому показываем «—»
                        RatingText.Text = "—";

                        AgeRatingText.Text = string.IsNullOrWhiteSpace(age) ? "—" : age;
                        AgeRatingBorder.Background = GetAgeColor(age);

                        IsbnText.Text = string.IsNullOrWhiteSpace(isbn) ? "ISBN: —" : $"ISBN: {isbn}";
                        PublisherText.Text = $"Издательство: {publisher}";

                        DescriptionText.Text = string.IsNullOrWhiteSpace(desc) ? "Описание отсутствует." : desc;

                        LanguageText.Text = string.IsNullOrWhiteSpace(lang) ? "—" : lang;
                        SeriesText.Text = "—";
                        AddedDateText.Text = "—";
                        PopularityText.Text = FormatPopularity(favCount);

                        SetupBookCover();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных книги из БД.\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FormatPopularity(int favoritesCount)
        {
            if (favoritesCount >= 50) return "Очень высокая ★★★★★";
            if (favoritesCount >= 20) return "Высокая ★★★★☆";
            if (favoritesCount >= 5) return "Средняя ★★★☆☆";
            return "Низкая ★★☆☆☆";
        }

        private void SetupBookCover()
        {
            if (coverBytes != null && coverBytes.Length > 0)
            {
                var img = ByteArrayToImageSource(coverBytes);
                if (img != null)
                {
                    BookCoverBorder.Background = new ImageBrush(img) { Stretch = Stretch.UniformToFill };
                    BookIcon.Visibility = Visibility.Collapsed;
                    return;
                }
            }

            // Фоллбек: цвет + иконка по жанру
            var hash = (TitleText.Text ?? "").GetHashCode();
            var rnd = new Random(hash);
            var color = Color.FromRgb(
                (byte)rnd.Next(50, 200),
                (byte)rnd.Next(50, 200),
                (byte)rnd.Next(50, 200)
            );
            BookCoverBorder.Background = new SolidColorBrush(color);

            var genre = genreForIcon ?? "";
            if (genre.Contains("Поэз"))
                BookIcon.Text = "📜";
            else if (genre.Contains("Фантаст") || genre.Contains("Фэнт"))
                BookIcon.Text = "🚀";
            else if (genre.Contains("Детектив") || genre.Contains("Триллер"))
                BookIcon.Text = "🔍";
            else if (genre.Contains("Ужасы"))
                BookIcon.Text = "👻";
            else
                BookIcon.Text = "📖";

            BookIcon.Visibility = Visibility.Visible;
        }

        private Brush GetAgeColor(string ageRating)
        {
            switch (ageRating)
            {
                case "0+": return new SolidColorBrush(Color.FromRgb(39, 174, 96));
                case "6+": return new SolidColorBrush(Color.FromRgb(46, 204, 113));
                case "12+": return new SolidColorBrush(Color.FromRgb(241, 196, 15));
                case "16+": return new SolidColorBrush(Color.FromRgb(230, 126, 34));
                case "18+": return new SolidColorBrush(Color.FromRgb(231, 76, 60));
                default: return new SolidColorBrush(Color.FromRgb(149, 165, 166));
            }
        }

        private void CheckIfInMyBooks()
        {
            int currentUserId = 1; // TODO из авторизации

            const string sql = @"IF NOT EXISTS (SELECT 1 FROM Favorites WHERE UserId=@u AND BookId=@b)
            INSERT INTO Favorites(UserId, BookId) VALUES(@u, @b);";

            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@u", currentUserId);
                cmd.Parameters.AddWithValue("@b", bookId);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
            try
            {
                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Favorites WHERE BookId = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", bookId);
                    conn.Open();
                    var count = Convert.ToInt32(cmd.ExecuteScalar());

                    isInMyBooks = count > 0;
                    currentStatus = isInMyBooks ? "В избранном" : "";
                    ShowBookStatus();
                }
            }
            catch
            {
                // не критично
            }
        }

        private void ShowBookStatus()
        {
            if (!isInMyBooks)
            {
                BookStatusPanel.Visibility = Visibility.Collapsed;
                return;
            }

            BookStatusPanel.Visibility = Visibility.Visible;
            BookStatusText.Text = currentStatus;
            BookProgressText.Text = "";
        }

        // --- UI handlers ---

        private void ReadBook_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.MainFrame.Navigate(new ReadingPage(TitleText.Text));
            }
        }

        private void AddToMyBooks_Click(object sender, RoutedEventArgs e)
        {
            StatusSelectionPanel.Visibility = Visibility.Visible;
            AddToMyBooksButton.IsEnabled = false;
        }

        private void CancelStatusSelection_Click(object sender, RoutedEventArgs e)
        {
            StatusSelectionPanel.Visibility = Visibility.Collapsed;
            AddToMyBooksButton.IsEnabled = true;
        }

        private void ConfirmAddToMyBooks_Click(object sender, RoutedEventArgs e)
        {
            // Минимальная реализация: добавляем в Favorites
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (var check = new SqlCommand("SELECT COUNT(*) FROM Favorites WHERE BookId=@id", conn))
                    {
                        check.Parameters.AddWithValue("@id", bookId);
                        if (Convert.ToInt32(check.ExecuteScalar()) == 0)
                        {
                            using (var ins = new SqlCommand("INSERT INTO Favorites(BookId) VALUES(@id)", conn))
                            {
                                ins.Parameters.AddWithValue("@id", bookId);
                                ins.ExecuteNonQuery();
                            }
                        }
                    }
                }

                isInMyBooks = true;

                if (ReadingNowRadio.IsChecked == true) currentStatus = "Читаю сейчас";
                else if (PlannedRadio.IsChecked == true) currentStatus = "В планах";
                else if (FinishedRadio.IsChecked == true) currentStatus = "Прочитано";
                else currentStatus = "В избранном";

                StatusSelectionPanel.Visibility = Visibility.Collapsed;
                ShowBookStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось добавить в избранное.\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                AddToMyBooksButton.IsEnabled = true;
            }
        }

        private void ChangeStatus_Click(object sender, RoutedEventArgs e)
        {
            // Так как в БД пока нет таблицы статусов чтения, меняем только в UI
            StatusSelectionPanel.Visibility = Visibility.Visible;
            AddToMyBooksButton.IsEnabled = false;
        }

        public void UpdateBookStatus(string newStatus)
        {
            currentStatus = newStatus;
            ShowBookStatus();
        }

        private static ImageSource ByteArrayToImageSource(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;

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
    }
}
