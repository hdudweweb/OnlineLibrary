using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OnlineLibrary1.Pages
{
    public partial class ReadingPage : Page
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["bibleoteka"].ConnectionString;

        private readonly int bookId;
        private readonly List<string> pages = new List<string>();
        private int currentPage = 1;

        public ReadingPage(int bookId)
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
                        pages.Clear();
                        pages.Add("Файл книги отсутствует.");
                        currentPage = 1;
                        ShowCurrentPage();
                        return;
                    }

                    if (contentType.IndexOf("pdf", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        string tempPath = Path.Combine(
                            Path.GetTempPath(),
                            string.IsNullOrWhiteSpace(fileName) ? $"book_{bookId}.pdf" : fileName);

                        File.WriteAllBytes(tempPath, fileBytes);
                        Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });

                        pages.Clear();
                        pages.Add("PDF открыт во внешнем приложении.");
                        currentPage = 1;
                        ShowCurrentPage();
                        return;
                    }

                    string text = DecodeBookText(fileBytes);
                    BuildPages(text);
                    ShowCurrentPage();
                }
            }
        }

        private string DecodeBookText(byte[] fileBytes)
        {
            if (fileBytes == null || fileBytes.Length == 0)
                return "";

            string utf8 = Encoding.UTF8.GetString(fileBytes);
            if (!LooksBroken(utf8))
                return NormalizeText(utf8);

            string win1251 = Encoding.GetEncoding(1251).GetString(fileBytes);
            if (!LooksBroken(win1251))
                return NormalizeText(win1251);

            string unicode = Encoding.Unicode.GetString(fileBytes);
            if (!LooksBroken(unicode))
                return NormalizeText(unicode);

            return NormalizeText(utf8);
        }

        private bool LooksBroken(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            int badCount = 0;
            foreach (char ch in text)
            {
                if (ch == '�')
                    badCount++;
            }

            return badCount > text.Length / 40;
        }

        private string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "Текст книги пуст.";

            text = text.Replace("\r\n", "\n");
            text = text.Replace("\r", "\n");
            text = text.Replace("\t", "    ");

            while (text.Contains("\n\n\n"))
                text = text.Replace("\n\n\n", "\n\n");

            return text.Trim();
        }

        private void BuildPages(string text)
        {
            pages.Clear();

            if (string.IsNullOrWhiteSpace(text))
            {
                pages.Add("Текст книги пуст.");
                currentPage = 1;
                return;
            }

            const int pageSize = 2200;
            int index = 0;

            while (index < text.Length)
            {
                int length = Math.Min(pageSize, text.Length - index);
                int end = index + length;

                if (end < text.Length)
                {
                    int lastBreak = text.LastIndexOfAny(new[] { ' ', '\n' }, end - 1, length);
                    if (lastBreak > index + 500)
                        end = lastBreak + 1;
                }

                string pageText = text.Substring(index, end - index).Trim();
                pages.Add(string.IsNullOrWhiteSpace(pageText) ? " " : pageText);
                index = end;
            }

            if (pages.Count == 0)
                pages.Add("Текст книги пуст.");

            if (currentPage < 1)
                currentPage = 1;
            if (currentPage > pages.Count)
                currentPage = pages.Count;
        }

        private void ShowCurrentPage()
        {
            if (pages.Count == 0)
            {
                BookContentText.Text = "Нет данных.";
                PageNumberText.Text = "0";
                TotalPagesText.Text = "/ 0";
                return;
            }

            BookContentText.Text = pages[currentPage - 1];
            PageNumberText.Text = currentPage.ToString();
            TotalPagesText.Text = "/ " + pages.Count;
            BookScrollViewer.ScrollToTop();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null && mainWindow.MainFrame.CanGoBack)
                mainWindow.MainFrame.GoBack();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
                mainWindow.MainFrame.Navigate(new CatalogPage());
        }

        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            if (pages.Count == 0) return;
            currentPage = 1;
            ShowCurrentPage();
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (pages.Count == 0) return;
            if (currentPage > 1)
            {
                currentPage--;
                ShowCurrentPage();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (pages.Count == 0) return;
            if (currentPage < pages.Count)
            {
                currentPage++;
                ShowCurrentPage();
            }
        }

        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            if (pages.Count == 0) return;
            currentPage = pages.Count;
            ShowCurrentPage();
        }

        private void PageNumberText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (pages.Count == 0)
                return;

            int pageNumber;
            if (int.TryParse(PageNumberText.Text, out pageNumber))
            {
                if (pageNumber < 1)
                    pageNumber = 1;
                if (pageNumber > pages.Count)
                    pageNumber = pages.Count;

                currentPage = pageNumber;
                ShowCurrentPage();
            }
            else
            {
                PageNumberText.Text = currentPage.ToString();
            }
        }
    }
}