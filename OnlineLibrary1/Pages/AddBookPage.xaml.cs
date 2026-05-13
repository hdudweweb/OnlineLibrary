using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace OnlineLibrary1.Pages
{
    public partial class AddBookPage : Page
    {
        private sealed class GenreSelectionItem
        {
            public int GenreId { get; set; }
            public string GenreName { get; set; }

            public override string ToString()
            {
                return GenreName ?? string.Empty;
            }
        }

        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["bibleoteka"].ConnectionString;

        private readonly List<GenreSelectionItem> selectedGenres = new List<GenreSelectionItem>();
        private readonly List<string> selectedLanguages = new List<string>();

        private byte[] _coverBytes;
        private byte[] _bookFileBytes;
        private string _bookFilePath;

        public AddBookPage()
        {
            InitializeComponent();
            LoadComboBoxes();
            RefreshSelectedGenres();
            RefreshSelectedLanguages();
        }

        private void LoadComboBoxes()
        {
            try
            {
                LoadAuthors();
                LoadAges();
                LoadGenres();
                LoadLanguages();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Не удалось загрузить данные из БД.\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void LoadAuthors()
        {
            const string sql = @"
                SELECT
                    AuthorId,
                    LTRIM(RTRIM(
                        CONCAT(LastName, ' ', FirstName, ' ', ISNULL(NULLIF(MidleName, ''), ''))
                    )) AS FullName
                FROM Author
                ORDER BY LastName, FirstName, MidleName";

            var dt = LoadDataTable(sql);
            AuthorComboBox.ItemsSource = dt.DefaultView;
            AuthorComboBox.DisplayMemberPath = "FullName";
            AuthorComboBox.SelectedValuePath = "AuthorId";
        }

        private void LoadAges()
        {
            const string sql = "SELECT AgeId, AgeName FROM Age ORDER BY AgeId";
            var dt = LoadDataTable(sql);

            AgeComboBox.ItemsSource = dt.DefaultView;
            AgeComboBox.DisplayMemberPath = "AgeName";
            AgeComboBox.SelectedValuePath = "AgeId";
        }

        private void LoadGenres()
        {
            const string sql = "SELECT GenreId, GenreName FROM Genre ORDER BY GenreName";
            var dt = LoadDataTable(sql);

            GenreComboBox.ItemsSource = dt.DefaultView;
            GenreComboBox.DisplayMemberPath = "GenreName";
            GenreComboBox.SelectedValuePath = "GenreId";
        }

        private void LoadLanguages()
        {
            const string sql = @"
                SELECT DISTINCT NameLang
                FROM Languages
                WHERE NameLang IS NOT NULL AND LTRIM(RTRIM(NameLang)) <> ''
                ORDER BY NameLang";

            var dt = LoadDataTable(sql);

            LanguageComboBox.ItemsSource = dt.DefaultView;
            LanguageComboBox.DisplayMemberPath = "NameLang";
            LanguageComboBox.SelectedValuePath = "NameLang";
        }

        private DataTable LoadDataTable(string sql)
        {
            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            using (var da = new SqlDataAdapter(cmd))
            {
                var dt = new DataTable();
                da.Fill(dt);
                return dt;
            }
        }

        private void UploadCover_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                Title = "Выберите обложку книги"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var bytes = File.ReadAllBytes(openFileDialog.FileName);
                if (bytes.Length > 5 * 1024 * 1024)
                {
                    MessageBox.Show("Файл обложки слишком большой (макс 5MB).");
                    return;
                }

                _coverBytes = bytes;
                ShowCoverPreview(bytes);
                MessageBox.Show("Обложка выбрана: " + Path.GetFileName(openFileDialog.FileName));
            }
        }

        private void ShowCoverPreview(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return;

            using (var ms = new MemoryStream(bytes))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();

                CoverPreviewImage.Source = image;
                CoverPreviewImage.Visibility = Visibility.Visible;
                CoverPlaceholderPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void UploadBookFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Book files (*.txt;*.pdf;*.fb2)|*.txt;*.pdf;*.fb2|All files (*.*)|*.*",
                Title = "Выберите файл с текстом книги"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _bookFilePath = openFileDialog.FileName;
                _bookFileBytes = File.ReadAllBytes(openFileDialog.FileName);

                BookContentTextBox.Visibility = Visibility.Collapsed;
                MessageBox.Show("Файл книги выбран: " + Path.GetFileName(openFileDialog.FileName));
            }
        }

        private void ChangeCover_Click(object sender, RoutedEventArgs e)
        {
            UploadCover_Click(sender, e);
        }

        private void RemoveCover_Click(object sender, RoutedEventArgs e)
        {
            _coverBytes = null;
            CoverPreviewImage.Source = null;
            CoverPreviewImage.Visibility = Visibility.Collapsed;
            CoverPlaceholderPanel.Visibility = Visibility.Visible;

            MessageBox.Show("Обложка удалена.");
        }

        private void ManualInput_Click(object sender, RoutedEventArgs e)
        {
            BookContentTextBox.Visibility = Visibility.Visible;
            _bookFilePath = null;
            _bookFileBytes = null;
        }

        private void AddAuthor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int authorId = GetOrCreateAuthorId();
                MessageBox.Show("Автор добавлен или выбран.");
                LoadAuthors();
                AuthorComboBox.SelectedValue = authorId;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка автора: " + ex.Message);
            }
        }

        private void AddGenre_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var genre = ResolveGenreSelection(allowEmpty: false);
                AddGenreToSelection(genre, true);
                LoadGenres();
                ClearGenreInput();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка жанра: " + ex.Message);
            }
        }

        private void AddLanguage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var language = ResolveLanguageName(allowEmpty: false);
                AddLanguageToSelection(language, true);
                LoadLanguages();
                ClearLanguageInput();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка языка: " + ex.Message);
            }
        }

        private void RemoveGenre_Click(object sender, RoutedEventArgs e)
        {
            var selected = SelectedGenresListBox.SelectedItem as GenreSelectionItem;
            if (selected == null)
            {
                MessageBox.Show("Выберите жанр для удаления из списка.");
                return;
            }

            selectedGenres.RemoveAll(x => x.GenreId == selected.GenreId);
            RefreshSelectedGenres();
        }

        private void RemoveLanguage_Click(object sender, RoutedEventArgs e)
        {
            var selected = SelectedLanguagesListBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(selected))
            {
                MessageBox.Show("Выберите язык для удаления из списка.");
                return;
            }

            selectedLanguages.RemoveAll(x => string.Equals(x, selected, StringComparison.OrdinalIgnoreCase));
            RefreshSelectedLanguages();
        }

        private void RefreshSelectedGenres()
        {
            SelectedGenresListBox.ItemsSource = null;
            SelectedGenresListBox.ItemsSource = selectedGenres
                .OrderBy(x => x.GenreName)
                .ToList();
        }

        private void RefreshSelectedLanguages()
        {
            SelectedLanguagesListBox.ItemsSource = null;
            SelectedLanguagesListBox.ItemsSource = selectedLanguages
                .OrderBy(x => x)
                .ToList();
        }

        private void SaveBook_Click(object sender, RoutedEventArgs e)
        {
            var title = (TitleTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Введите название книги!");
                return;
            }

            int pages;
            if (!int.TryParse(PagesTextBox.Text, out pages) || pages <= 0)
            {
                MessageBox.Show("Введите корректное количество страниц!");
                return;
            }

            int year;
            if (!int.TryParse(YearTextBox.Text, out year) || year <= 0)
            {
                MessageBox.Show("Введите корректный год издания!");
                return;
            }

            var isbn = (IsbnTextBox.Text ?? string.Empty).Trim();
            var description = (DescriptionTextBox.Text ?? string.Empty).Trim();
            var publisher = (PublisherTextBox.Text ?? string.Empty).Trim();

            int? ageId = AgeComboBox.SelectedValue == null
                ? (int?)null
                : Convert.ToInt32(AgeComboBox.SelectedValue);

            int authorId;
            try
            {
                authorId = GetOrCreateAuthorId();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка автора: " + ex.Message);
                return;
            }

            try
            {
                CollectPendingSelectionsForSave();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }

            if (selectedGenres.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы один жанр.");
                return;
            }

            byte[] bookBytes = _bookFileBytes;
            if (bookBytes == null && BookContentTextBox.Visibility == Visibility.Visible)
            {
                var manualText = (BookContentTextBox.Text ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(manualText))
                    bookBytes = Encoding.UTF8.GetBytes(manualText);
            }

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        int bookId = InsertBook(
                            conn,
                            tx,
                            title,
                            pages,
                            year,
                            isbn,
                            description,
                            bookBytes,
                            ageId,
                            null,
                            authorId);

                        foreach (var genre in selectedGenres)
                            InsertGenreBook(conn, tx, bookId, genre.GenreId);

                        foreach (var language in selectedLanguages)
                            InsertLanguageForBook(conn, tx, bookId, language);

                        if (_coverBytes != null && _coverBytes.Length > 0)
                            InsertCover(conn, tx, bookId, _coverBytes);

                        if (!string.IsNullOrWhiteSpace(publisher))
                            InsertPublishingHouseForBook(conn, tx, bookId, publisher);

                        tx.Commit();

                        MessageBox.Show(
                            "Книга успешно добавлена!",
                            "Успех",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        mainWindow?.MainFrame.Navigate(new CatalogPage());
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        MessageBox.Show(
                            $"Ошибка сохранения:\n{ex.Message}",
                            "Ошибка",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
        }

        private void CollectPendingSelectionsForSave()
        {
            TryAddPendingGenreSelection();
            TryAddPendingLanguageSelection();
        }

        private void TryAddPendingGenreSelection()
        {
            bool hasPendingInput = GenreComboBox.SelectedValue != null ||
                                   !string.IsNullOrWhiteSpace(GenreComboBox.Text);

            if (!hasPendingInput)
                return;

            var genre = ResolveGenreSelection(allowEmpty: selectedGenres.Count > 0);
            AddGenreToSelection(genre, false);
            ClearGenreInput();
        }

        private void TryAddPendingLanguageSelection()
        {
            bool hasPendingInput = LanguageComboBox.SelectedValue != null ||
                                   !string.IsNullOrWhiteSpace(LanguageComboBox.Text);

            if (!hasPendingInput)
                return;

            var language = ResolveLanguageName(allowEmpty: true);
            AddLanguageToSelection(language, false);
            ClearLanguageInput();
        }

        private void AddGenreToSelection(GenreSelectionItem genre, bool showMessage)
        {
            if (genre == null)
                return;

            if (selectedGenres.Any(x => x.GenreId == genre.GenreId))
            {
                if (showMessage)
                    MessageBox.Show("Этот жанр уже добавлен в список.");

                return;
            }

            selectedGenres.Add(genre);
            RefreshSelectedGenres();

            if (showMessage)
                MessageBox.Show("Жанр добавлен в список.");
        }

        private void AddLanguageToSelection(string language, bool showMessage)
        {
            if (string.IsNullOrWhiteSpace(language))
                return;

            if (selectedLanguages.Any(x => string.Equals(x, language, StringComparison.OrdinalIgnoreCase)))
            {
                if (showMessage)
                    MessageBox.Show("Этот язык уже добавлен в список.");

                return;
            }

            selectedLanguages.Add(language);
            RefreshSelectedLanguages();

            if (showMessage)
                MessageBox.Show("Язык добавлен в список.");
        }

        private void ClearGenreInput()
        {
            GenreComboBox.SelectedItem = null;
            GenreComboBox.SelectedValue = null;
            GenreComboBox.Text = string.Empty;
        }

        private void ClearLanguageInput()
        {
            LanguageComboBox.SelectedItem = null;
            LanguageComboBox.SelectedValue = null;
            LanguageComboBox.Text = string.Empty;
        }

        private int GetOrCreateAuthorId()
        {
            if (AuthorComboBox.SelectedValue != null)
                return Convert.ToInt32(AuthorComboBox.SelectedValue);

            var raw = (AuthorComboBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
                throw new InvalidOperationException("Укажите автора.");

            var parts = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new InvalidOperationException("Введите автора в формате: Фамилия Имя Отчество.");

            var lastName = parts[0];
            var firstName = parts[1];
            string middleName = parts.Length >= 3
                ? string.Join(" ", parts, 2, parts.Length - 2)
                : null;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (var find = new SqlCommand(@"
                    SELECT TOP 1 AuthorId
                    FROM Author
                    WHERE LastName = @ln
                      AND FirstName = @fn
                      AND ISNULL(MidleName,'') = ISNULL(@mn,'')", conn))
                {
                    find.Parameters.AddWithValue("@ln", lastName);
                    find.Parameters.AddWithValue("@fn", firstName);
                    find.Parameters.AddWithValue("@mn", (object)middleName ?? DBNull.Value);

                    var found = find.ExecuteScalar();
                    if (found != null && found != DBNull.Value)
                        return Convert.ToInt32(found);
                }

                using (var ins = new SqlCommand(@"
                    INSERT INTO Author(LastName, FirstName, MidleName)
                    OUTPUT INSERTED.AuthorId
                    VALUES (@ln, @fn, @mn)", conn))
                {
                    ins.Parameters.AddWithValue("@ln", lastName);
                    ins.Parameters.AddWithValue("@fn", firstName);
                    ins.Parameters.AddWithValue("@mn", (object)middleName ?? DBNull.Value);

                    var newId = ins.ExecuteScalar();
                    if (newId == null)
                        throw new Exception("Не удалось добавить автора.");

                    return Convert.ToInt32(newId);
                }
            }
        }

        private GenreSelectionItem ResolveGenreSelection(bool allowEmpty)
        {
            if (GenreComboBox.SelectedValue != null)
            {
                var selectedRow = GenreComboBox.SelectedItem as DataRowView;
                var genreName = selectedRow == null
                    ? (GenreComboBox.Text ?? string.Empty).Trim()
                    : Convert.ToString(selectedRow["GenreName"]);

                return new GenreSelectionItem
                {
                    GenreId = Convert.ToInt32(GenreComboBox.SelectedValue),
                    GenreName = genreName
                };
            }

            var genreNameInput = (GenreComboBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(genreNameInput))
            {
                if (allowEmpty)
                    return null;

                throw new InvalidOperationException("Укажите жанр.");
            }

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (var find = new SqlCommand(@"
                    SELECT TOP 1 GenreId, GenreName
                    FROM Genre
                    WHERE LTRIM(RTRIM(GenreName)) = LTRIM(RTRIM(@name))", conn))
                {
                    find.Parameters.AddWithValue("@name", genreNameInput);

                    using (var reader = find.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new GenreSelectionItem
                            {
                                GenreId = Convert.ToInt32(reader["GenreId"]),
                                GenreName = reader["GenreName"]?.ToString() ?? genreNameInput
                            };
                        }
                    }
                }

                using (var ins = new SqlCommand(@"
                    INSERT INTO Genre(GenreName)
                    OUTPUT INSERTED.GenreId
                    VALUES (@name)", conn))
                {
                    ins.Parameters.AddWithValue("@name", genreNameInput);

                    var newId = ins.ExecuteScalar();
                    if (newId == null)
                        throw new Exception("Не удалось добавить жанр.");

                    return new GenreSelectionItem
                    {
                        GenreId = Convert.ToInt32(newId),
                        GenreName = genreNameInput
                    };
                }
            }
        }

        private string ResolveLanguageName(bool allowEmpty)
        {
            var languageInput = (LanguageComboBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(languageInput))
            {
                if (LanguageComboBox.SelectedValue != null)
                    languageInput = Convert.ToString(LanguageComboBox.SelectedValue);
            }

            if (string.IsNullOrWhiteSpace(languageInput))
            {
                if (allowEmpty)
                    return null;

                throw new InvalidOperationException("Укажите язык.");
            }

            using (var conn = new SqlConnection(connectionString))
            using (var find = new SqlCommand(@"
                SELECT TOP 1 NameLang
                FROM Languages
                WHERE LTRIM(RTRIM(NameLang)) = LTRIM(RTRIM(@name))
                ORDER BY NameLang", conn))
            {
                find.Parameters.AddWithValue("@name", languageInput);
                conn.Open();

                var found = find.ExecuteScalar();
                if (found != null && found != DBNull.Value)
                    return Convert.ToString(found);
            }

            return languageInput;
        }

        private int InsertBook(
            SqlConnection conn,
            SqlTransaction tx,
            string title,
            int totalPages,
            int publicationYear,
            string isbn,
            string descriptionBook,
            byte[] fb2File,
            int? ageId,
            int? chapterId,
            int authorId)
        {
            using (var cmd = new SqlCommand(@"
                INSERT INTO Book
                    ([Name], TotalPages, PublicationYear, ISBN, DescriptionBook, BookFile, AgeId, ChapterId, AuthorId)
                OUTPUT INSERTED.BookId
                VALUES
                    (@name, @pages, @year, @isbn, @desc, @file, @ageId, @chapterId, @authorId)
            ", conn, tx))
            {
                cmd.Parameters.AddWithValue("@name", title);
                cmd.Parameters.AddWithValue("@pages", totalPages);
                cmd.Parameters.AddWithValue("@year", publicationYear);
                cmd.Parameters.AddWithValue("@isbn", string.IsNullOrWhiteSpace(isbn) ? (object)DBNull.Value : isbn);
                cmd.Parameters.AddWithValue("@desc", string.IsNullOrWhiteSpace(descriptionBook) ? (object)DBNull.Value : descriptionBook);
                cmd.Parameters.AddWithValue("@file", (object)fb2File ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ageId", (object)ageId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@chapterId", (object)chapterId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@authorId", authorId);

                var bookIdObj = cmd.ExecuteScalar();
                if (bookIdObj == null)
                    throw new Exception("Не удалось сохранить книгу.");

                return Convert.ToInt32(bookIdObj);
            }
        }

        private void InsertGenreBook(SqlConnection conn, SqlTransaction tx, int bookId, int genreId)
        {
            using (var cmd = new SqlCommand(@"
                INSERT INTO GenreBook(BookId, GenreId)
                VALUES (@bookId, @genreId)
            ", conn, tx))
            {
                cmd.Parameters.AddWithValue("@bookId", bookId);
                cmd.Parameters.AddWithValue("@genreId", genreId);
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertCover(SqlConnection conn, SqlTransaction tx, int bookId, byte[] coverBytes)
        {
            using (var cmd = new SqlCommand(@"
                INSERT INTO Covers(Cover, BookId)
                VALUES (@cover, @bookId)
            ", conn, tx))
            {
                cmd.Parameters.AddWithValue("@cover", coverBytes);
                cmd.Parameters.AddWithValue("@bookId", bookId);
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertLanguageForBook(SqlConnection conn, SqlTransaction tx, int bookId, string language)
        {
            using (var cmd = new SqlCommand(@"
                INSERT INTO Languages(NameLang, BookId)
                VALUES (@lang, @bookId)
            ", conn, tx))
            {
                cmd.Parameters.AddWithValue("@lang", language);
                cmd.Parameters.AddWithValue("@bookId", bookId);
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertPublishingHouseForBook(SqlConnection conn, SqlTransaction tx, int bookId, string publisher)
        {
            using (var cmd = new SqlCommand(@"
                INSERT INTO PublishingHouse(NamePublish, BookId)
                VALUES (@name, @bookId)
            ", conn, tx))
            {
                cmd.Parameters.AddWithValue("@name", publisher);
                cmd.Parameters.AddWithValue("@bookId", bookId);
                cmd.ExecuteNonQuery();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.MainFrame.Navigate(new CatalogPage());
        }
    }
}
