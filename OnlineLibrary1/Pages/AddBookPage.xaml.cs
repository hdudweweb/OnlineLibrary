using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
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
    /// Логика взаимодействия для AddBookPage.xaml
    /// </summary>
    public partial class AddBookPage : Page
    {
        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["bibleoteka"].ConnectionString;

        private byte[] _coverBytes;          
        private byte[] _bookFileBytes;       
        private string _bookFilePath;        

        public AddBookPage()
        {
            InitializeComponent();
            LoadComboBoxes();
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
                MessageBox.Show($"Не удалось загрузить данные из БД.\n{ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                ORDER BY LastName, FirstName";

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
                MessageBox.Show($"Обложка выбрана: {System.IO.Path.GetFileName(openFileDialog.FileName)}");
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
                MessageBox.Show($"Файл книги выбран: {System.IO.Path.GetFileName(openFileDialog.FileName)}");
            }
        }

        private void ManualInput_Click(object sender, RoutedEventArgs e)
        {
            BookContentTextBox.Visibility = Visibility.Visible;

            
            _bookFilePath = null;
            _bookFileBytes = null;
        }

        private void SaveBook_Click(object sender, RoutedEventArgs e)
        {
            
            var title = (TitleTextBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Введите название книги!");
                return;
            }

            if (!int.TryParse(PagesTextBox.Text, out var pages) || pages <= 0)
            {
                MessageBox.Show("Введите корректное количество страниц!");
                return;
            }

            if (!int.TryParse(YearTextBox.Text, out var year) || year <= 0)
            {
                MessageBox.Show("Введите корректный год издания!");
                return;
            }

            if (GenreComboBox.SelectedValue == null)
            {
                MessageBox.Show("Выберите жанр!");
                return;
            }

            var isbn = (IsbnTextBox.Text ?? "").Trim();
            var description = (DescriptionTextBox.Text ?? "").Trim();

            int? ageId = AgeComboBox.SelectedValue as int?;
            int genreId = Convert.ToInt32(GenreComboBox.SelectedValue);

            int authorId;
            try
            {
                authorId = GetOrCreateAuthorId();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка автора: {ex.Message}");
                return;
            }

            byte[] bookBytes = _bookFileBytes;
            if (bookBytes == null && BookContentTextBox.Visibility == Visibility.Visible)
            {
                var manualText = (BookContentTextBox.Text ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(manualText))
                    bookBytes = Encoding.UTF8.GetBytes(manualText);
            }

            var publisher = (PublisherTextBox.Text ?? "").Trim();

            string language = null;
            if (LanguageComboBox.SelectedValue != null)
            {
                if (LanguageComboBox.SelectedValue is string s)
                    language = s;
                else
                    language = Convert.ToString(LanguageComboBox.SelectedValue);
            }

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        int bookId = InsertBook(conn, tx,
                            title: title,
                            totalPages: pages,
                            publicationYear: year,
                            isbn: isbn,
                            descriptionBook: description,
                            fb2File: bookBytes,
                            ageId: ageId,
                            chapterId: null,   
                            authorId: authorId
                        );

                        InsertGenreBook(conn, tx, bookId, genreId);

                        if (_coverBytes != null && _coverBytes.Length > 0)
                        {
                            InsertCover(conn, tx, bookId, _coverBytes);
                        }

                        if (!string.IsNullOrWhiteSpace(language))
                        {
                            InsertLanguageForBook(conn, tx, bookId, language);
                        }

                        
                        if (!string.IsNullOrWhiteSpace(publisher))
                        {
                            InsertPublishingHouseForBook(conn, tx, bookId, publisher);
                        }

                        tx.Commit();

                        MessageBox.Show("Книга успешно добавлена!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        mainWindow?.MainFrame.Navigate(new CatalogPage());
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        MessageBox.Show($"Ошибка сохранения:\n{ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private int GetOrCreateAuthorId()
        {
            if (AuthorComboBox.SelectedValue != null)
                return Convert.ToInt32(AuthorComboBox.SelectedValue);

            var raw = (AuthorComboBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(raw))
                throw new InvalidOperationException("Укажите автора (выберите из списка или введите вручную).");

            var parts = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new InvalidOperationException("Введите автора в формате: Фамилия Имя (Отчество).");

            var lastName = parts[0];
            var firstName = parts[1];
            string middleName = parts.Length >= 3 ? string.Join(" ", parts, 2, parts.Length - 2) : null;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                using (var find = new SqlCommand(@"
            SELECT TOP 1 AuthorId
            FROM Author
            WHERE LastName = @ln AND FirstName = @fn AND ISNULL(MidleName,'') = ISNULL(@mn,'')", conn))
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

                    LoadAuthors();
                    AuthorComboBox.SelectedValue = Convert.ToInt32(newId);

                    return Convert.ToInt32(newId);
                }
            }
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
                cmd.Parameters.AddWithValue("@isbn", (object)isbn ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", (object)descriptionBook ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@file", (object)fb2File ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ageId", (object)ageId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@chapterId", (object)chapterId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@authorId", authorId);

                var bookIdObj = cmd.ExecuteScalar();
                if (bookIdObj == null)
                    throw new Exception("Не удалось сохранить книгу (Book).");

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

        private void InsertLanguageForBook(SqlConnection conn, SqlTransaction tx, int bookId, string lang)
        {
            using (var cmd = new SqlCommand(@"
                INSERT INTO Languages(NameLang, BookId)
                VALUES (@lang, @bookId)
            ", conn, tx))
            {
                cmd.Parameters.AddWithValue("@lang", lang);
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
