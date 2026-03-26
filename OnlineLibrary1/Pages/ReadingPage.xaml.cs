using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
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
    /// Логика взаимодействия для ReadingPage.xaml
    /// </summary>
    public partial class ReadingPage : Page
    {
        private readonly string connectionString =ConfigurationManager.ConnectionStrings["bibleoteka"].ConnectionString;

        private readonly int bookId;
        private List<string> pages = new List<string>();
        private int currentPage = 1;

        public ReadingPage(string bookTitle)
        {
            InitializeComponent();
            this.bookId = bookId;
            LoadBook();
        }

        private void LoadBook()
        {
            const string sql = @"
        SELECT [Name], BookFile, FileName, ContentType
        FROM Book
        WHERE BookId = @id";

            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", bookId);
                conn.Open();

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                    {
                        MessageBox.Show("Книга не найдена.");
                        return;
                    }

                    string title = r["Name"]?.ToString() ?? "Книга";
                    byte[] fileBytes = r["BookFile"] == DBNull.Value ? null : (byte[])r["BookFile"];
                    string fileName = r["FileName"]?.ToString() ?? "";
                    string contentType = r["ContentType"]?.ToString() ?? "";

                    BookTitleText.Text = title;

                    if (fileBytes == null || fileBytes.Length == 0)
                    {
                        BookContentText.Text = "Файл книги отсутствует.";
                        return;
                    }

                    if (contentType.Contains("pdf") || fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                            string.IsNullOrWhiteSpace(fileName) ? $"book_{bookId}.pdf" : fileName);

                        File.WriteAllBytes(tempPath, fileBytes);
                        Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });

                        BookContentText.Text = "PDF открыт во внешнем приложении.";
                        return;
                    }

                    string text = Encoding.UTF8.GetString(fileBytes);
                    BuildPages(text);
                    ShowCurrentPage();
                }
            }
        }
        private void BuildPages(string text)
        {
            pages.Clear();

            if (string.IsNullOrWhiteSpace(text))
            {
                pages.Add("Текст книги пуст.");
                return;
            }

            const int pageSize = 2500;
            for (int i = 0; i < text.Length; i += pageSize)
            {
                int len = Math.Min(pageSize, text.Length - i);
                pages.Add(text.Substring(i, len));
            }

            currentPage = 1;
        }
        private void ShowCurrentPage()
        {
            if (pages.Count == 0)
            {
                BookContentText.Text = "Нет данных.";
                PageNumberText.Text = "0";
                return;
            }

            BookContentText.Text = pages[currentPage - 1];
            PageNumberText.Text = currentPage.ToString();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow.MainFrame.GoBack();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow.MainFrame.Navigate(new CatalogPage());
        }

        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            currentPage = 1;
            LoadBook();
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage > 1)
            {
                currentPage--;
                LoadBook();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage < pages.Count)
            {
                currentPage++;
                LoadBook();
            }
        }

        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            currentPage = pages.Count;
            LoadBook();
        }
    }
}
