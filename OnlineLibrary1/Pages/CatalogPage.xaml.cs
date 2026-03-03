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
using OnlineLibrary1.Models;
namespace OnlineLibrary1.Pages
{
    /// <summary>
    /// Логика взаимодействия для CatalogPage.xaml
    /// </summary>
    public partial class CatalogPage : Page
    {

        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["bibleoteka"].ConnectionString;

        private List<BookItem> allBooks = new List<BookItem>();
        private List<BookItem> filteredBooks = new List<BookItem>();

        public CatalogPage()
        {
            InitializeComponent();

            InitializeSearch();
                
            LoadFiltersFromDb();
            LoadBooksFromDb();
            UpdateBooksDisplay();
        }

        private void InitializeSearch()
        {
            if (SearchTextBox != null)
                SearchTextBox.TextChanged += SearchTextBox_TextChanged;

            if (SearchTypeComboBox != null)
            {
                SearchTypeComboBox.SelectionChanged += SearchTypeComboBox_SelectionChanged;
                if (SearchTypeComboBox.Items.Count > 0)
                    SearchTypeComboBox.SelectedIndex = 0;
            }
        }

        

        private void LoadBooksFromDb()
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
    ISNULL(ag.AgeName, N'') AS AgeRating,
    ISNULL(b.ISBN, N'') AS ISBN,
    ISNULL(b.TotalPages, 0) AS Pages,
    ISNULL(b.DescriptionBook, N'') AS [Description],
    c.CoverBytes
FROM Book b
INNER JOIN Author a ON a.AuthorId = b.AuthorId
LEFT JOIN Age ag ON ag.AgeId = b.AgeId
LEFT JOIN GenreBook gb ON gb.BookId = b.BookId
LEFT JOIN Genre g ON g.GenreId = gb.GenreId
OUTER APPLY (
    SELECT TOP 1 Cover AS CoverBytes
    FROM Covers
    WHERE BookId = b.BookId
    ORDER BY CoversId DESC
) c
ORDER BY b.[Name];";

                var list = new List<BookItem>();

                using (var conn = new SqlConnection(connectionString))
                using (var cmd = new SqlCommand(sql, conn))
                {
                    conn.Open();
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
                                Genre = r["Genre"]?.ToString() ?? "",
                                AgeRating = r["AgeRating"]?.ToString() ?? "",
                                ISBN = r["ISBN"]?.ToString() ?? "",
                                Pages = Convert.ToInt32(r["Pages"]),
                                Description = r["Description"]?.ToString() ?? "",
                                CoverBytes = r["CoverBytes"] == DBNull.Value ? null : (byte[])r["CoverBytes"],
                            });
                        }
                    }
                }

                allBooks = list;
                filteredBooks = new List<BookItem>(allBooks);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки каталога из БД.\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                allBooks = new List<BookItem>();
                filteredBooks = new List<BookItem>();
            }
        }

        private void LoadFiltersFromDb()
        {
            try
            {
                // Жанры
                if (GenreFilterComboBox != null)
                {
                    GenreFilterComboBox.Items.Clear();
                    GenreFilterComboBox.Items.Add(new ComboBoxItem { Content = "Все жанры" });

                    using (var conn = new SqlConnection(connectionString))
                    using (var cmd = new SqlCommand("SELECT GenreName FROM Genre ORDER BY GenreName", conn))
                    {
                        conn.Open();
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                                GenreFilterComboBox.Items.Add(new ComboBoxItem { Content = r.GetString(0) });
                        }
                    }

                    GenreFilterComboBox.SelectedIndex = 0;
                }

                // Годы (только те, что есть в Book)
                if (YearFilterComboBox != null)
                {
                    YearFilterComboBox.Items.Clear();
                    YearFilterComboBox.Items.Add(new ComboBoxItem { Content = "Все годы" });

                    using (var conn = new SqlConnection(connectionString))
                    using (var cmd = new SqlCommand(@"SELECT DISTINCT PublicationYear FROM Book WHERE PublicationYear IS NOT NULL ORDER BY PublicationYear DESC", conn))
                    {
                        conn.Open();
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                                YearFilterComboBox.Items.Add(new ComboBoxItem { Content = r.GetInt32(0).ToString() });
                        }
                    }

                    YearFilterComboBox.Items.Add(new ComboBoxItem { Content = "До 2020" });
                    YearFilterComboBox.Items.Add(new ComboBoxItem { Content = "До 2000" });
                    YearFilterComboBox.Items.Add(new ComboBoxItem { Content = "До 1900" });
                    YearFilterComboBox.SelectedIndex = 0;
                }

                // Возраст
                if (AgeFilterComboBox != null)
                {
                    AgeFilterComboBox.Items.Clear();
                    AgeFilterComboBox.Items.Add(new ComboBoxItem { Content = "Все возрасты" });

                    using (var conn = new SqlConnection(connectionString))
                    using (var cmd = new SqlCommand("SELECT AgeName FROM Age ORDER BY AgeId", conn))
                    {
                        conn.Open();
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                                AgeFilterComboBox.Items.Add(new ComboBoxItem { Content = r.GetString(0) });
                        }
                    }

                    AgeFilterComboBox.SelectedIndex = 0;
                }
            }
            catch
            {
                // фильтры не критичны
            }
        }

        private void UpdateBooksDisplay()
        {
            if (BooksContainer == null) return;

            BooksContainer.Children.Clear();

            if (filteredBooks.Count == 0)
            {
                if (NoResultsPanel != null)
                {
                    NoResultsPanel.Visibility = Visibility.Visible;
                    BooksContainer.Visibility = Visibility.Collapsed;
                }
                return;
            }

            if (NoResultsPanel != null)
            {
                NoResultsPanel.Visibility = Visibility.Collapsed;
                BooksContainer.Visibility = Visibility.Visible;
            }

            foreach (var book in filteredBooks)
            {
                var bookBlock = CreateBookBlock(book);
                if (bookBlock != null)
                    BooksContainer.Children.Add(bookBlock);
            }
        }

        private Border CreateBookBlock(BookItem book)
        {
            if (book == null) return null;

            var border = new Border
            {
                Style = (Style)FindResource("BookBlockStyle"),
                Margin = new Thickness(10),
                Background = Brushes.White,
                Cursor = Cursors.Hand,
                CornerRadius = new CornerRadius(8)
            };

            var stackPanel = new StackPanel { Margin = new Thickness(0) };

            // Обложка
            var coverBorder = new Border
            {
                Height = 180,
                Margin = new Thickness(0, 0, 0, 10),
                CornerRadius = new CornerRadius(5, 5, 0, 0),
                ClipToBounds = true
            };

            if (book.CoverBytes != null && book.CoverBytes.Length > 0)
            {
                var img = ByteArrayToImageSource(book.CoverBytes);
                if (img != null)
                    coverBorder.Background = new ImageBrush(img) { Stretch = Stretch.UniformToFill };
            }

            if (coverBorder.Background == null)
            {
                var random = new Random((book.Title ?? "").GetHashCode());
                var color = Color.FromRgb(
                    (byte)random.Next(50, 200),
                    (byte)random.Next(50, 200),
                    (byte)random.Next(50, 200)
                );
                coverBorder.Background = new SolidColorBrush(color);
            }

            var bookIcon = new TextBlock
            {
                Text = "📖",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.8
            };

            var coverGrid = new Grid();
            coverGrid.Children.Add(bookIcon);

            // Рейтинг (в БД пока нет — показываем «—»)
            var ratingBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 5, 5),
                Padding = new Thickness(3, 1, 3, 1)
            };

            var ratingStack = new StackPanel { Orientation = Orientation.Horizontal };
            ratingStack.Children.Add(new TextBlock
            {
                Text = "★",
                Foreground = Brushes.Gold,
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            });
            ratingStack.Children.Add(new TextBlock
            {
                Text = "—",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(2, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            ratingBorder.Child = ratingStack;
            coverGrid.Children.Add(ratingBorder);

            coverBorder.Child = coverGrid;
            stackPanel.Children.Add(coverBorder);

            // Название
            var titleText = new TextBlock
            {
                Text = book.Title,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Height = 40,
                Margin = new Thickness(10, 0, 10, 5),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            // Автор
            var authorText = new TextBlock
            {
                Text = book.Author,
                FontStyle = FontStyles.Italic,
                FontSize = 12,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(10, 0, 10, 5),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            // Инфо
            var infoGrid = new Grid { Margin = new Thickness(10, 0, 10, 5) };
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var yearGenreText = new TextBlock
            {
                Text = $"{book.Year} • {book.Genre}",
                FontSize = 11,
                Foreground = Brushes.Gray,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(yearGenreText, 0);

            var ageBorder = new Border
            {
                Background = GetAgeColor(book.AgeRating),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1)
            };

            var ageText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(book.AgeRating) ? "—" : book.AgeRating,
                FontSize = 10,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold
            };
            ageBorder.Child = ageText;
            Grid.SetColumn(ageBorder, 1);

            infoGrid.Children.Add(yearGenreText);
            infoGrid.Children.Add(ageBorder);

            var pagesText = new TextBlock
            {
                Text = $"{book.Pages} стр.",
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(10, 0, 10, 10)
            };

            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(authorText);
            stackPanel.Children.Add(infoGrid);
            stackPanel.Children.Add(pagesText);

            border.Child = stackPanel;

            border.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(30, 74, 111, 165));
                border.RenderTransform = new ScaleTransform(1.02, 1.02);
                border.RenderTransformOrigin = new Point(0.5, 0.5);
            };

            border.MouseLeave += (s, e) =>
            {
                border.Background = Brushes.White;
                border.RenderTransform = new ScaleTransform(1.0, 1.0);
            };

            border.MouseLeftButtonDown += (s, e) =>
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                mainWindow?.MainFrame?.Navigate(new BoolDetailsPage(book.BookId));
            };

            return border;
        }

        private Brush GetAgeColor(string ageRating)
        {
            if (string.IsNullOrEmpty(ageRating)) return Brushes.Gray;

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

        // Поиск
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => PerformSearch();
        private void SearchButton_Click(object sender, RoutedEventArgs e) => PerformSearch();

        private void SearchTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchTypeComboBox?.SelectedItem == null) return;

            string searchType = "По названию";
            if (SearchTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
                searchType = selectedItem.Content?.ToString() ?? "По названию";

            if (GenreFilterComboBox != null)
                GenreFilterComboBox.Visibility = searchType.Contains("жанру") ? Visibility.Visible : Visibility.Collapsed;

            if (YearFilterComboBox != null)
            {
                YearFilterComboBox.Visibility = searchType.Contains("году") ? Visibility.Visible : Visibility.Collapsed;
                AgeFilterComboBox.Visibility = searchType.Contains("году") ? Visibility.Visible : Visibility.Collapsed;
            }

            PerformSearch();
        }

        private void FilterChanged(object sender, SelectionChangedEventArgs e) => PerformSearch();

        private void PerformSearch()
        {
            if (SearchTypeComboBox == null) return;

            string searchType = "По названию";
            if (SearchTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
                searchType = selectedItem.Content?.ToString() ?? "По названию";

            string searchText = SearchTextBox?.Text?.ToLower() ?? "";

            var newFilteredBooks = new List<BookItem>();

            foreach (var book in allBooks)
            {
                if (book == null) continue;

                bool matchesSearch;
                bool matchesFilters = true;

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    matchesSearch = true;
                }
                else
                {
                    switch (searchType)
                    {
                        case "По автору":
                            matchesSearch = (book.Author ?? "").ToLower().Contains(searchText);
                            break;
                        case "По жанру":
                            matchesSearch = (book.Genre ?? "").ToLower().Contains(searchText);
                            break;
                        case "По году":
                            matchesSearch = book.Year.ToString().Contains(searchText);
                            break;
                        case "По ISBN":
                            matchesSearch = (book.ISBN ?? "").ToLower().Contains(searchText);
                            break;
                        default:
                            matchesSearch = (book.Title ?? "").ToLower().Contains(searchText);
                            break;
                    }
                }

                // Фильтры
                if (GenreFilterComboBox != null && GenreFilterComboBox.Visibility == Visibility.Visible)
                {
                    if (GenreFilterComboBox.SelectedItem is ComboBoxItem genreItem)
                    {
                        var selectedGenre = genreItem.Content?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(selectedGenre) && selectedGenre != "Все жанры")
                        {
                            if (!string.Equals(book.Genre, selectedGenre, StringComparison.OrdinalIgnoreCase))
                                matchesFilters = false;
                        }
                    }
                }

                if (YearFilterComboBox != null && YearFilterComboBox.Visibility == Visibility.Visible)
                {
                    if (YearFilterComboBox.SelectedItem is ComboBoxItem yearItem)
                    {
                        var selectedYear = yearItem.Content?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(selectedYear) && selectedYear != "Все годы")
                        {
                            if (selectedYear == "До 2020")
                            {
                                if (book.Year >= 2020) matchesFilters = false;
                            }
                            else if (selectedYear == "До 2000")
                            {
                                if (book.Year >= 2000) matchesFilters = false;
                            }
                            else if (selectedYear == "До 1900")
                            {
                                if (book.Year >= 1900) matchesFilters = false;
                            }
                            else if (int.TryParse(selectedYear, out int year))
                            {
                                if (book.Year != year) matchesFilters = false;
                            }
                        }
                    }
                }

                if (AgeFilterComboBox != null && AgeFilterComboBox.Visibility == Visibility.Visible)
                {
                    if (AgeFilterComboBox.SelectedItem is ComboBoxItem ageItem)
                    {
                        var selectedAge = ageItem.Content?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(selectedAge) && selectedAge != "Все возрасты")
                        {
                            if (!string.Equals(book.AgeRating, selectedAge, StringComparison.OrdinalIgnoreCase))
                                matchesFilters = false;
                        }
                    }
                }

                if (matchesSearch && matchesFilters)
                    newFilteredBooks.Add(book);
            }

            filteredBooks = newFilteredBooks;
            UpdateBooksDisplay();
        }

        public void UpdateColumns()
        {
            if (BooksContainer == null) return;

            var window = Application.Current.MainWindow;
            if (window == null) return;

            double width = window.ActualWidth;
            if (width > 1400)
                BooksContainer.Columns = 5;
            else if (width > 1100)
                BooksContainer.Columns = 4;
            else if (width > 800)
                BooksContainer.Columns = 3;
            else if (width > 500)
                BooksContainer.Columns = 2;
            else
                BooksContainer.Columns = 1;
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
