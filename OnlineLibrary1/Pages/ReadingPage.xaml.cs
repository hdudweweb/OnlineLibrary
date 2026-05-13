using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;
using System.Xml.Linq;

namespace OnlineLibrary1.Pages
{
    public partial class ReadingPage : Page
    {
        private enum BookContentKind
        {
            PlainText,
            Fb2,
            Pdf
        }

        private const int TargetPageSize = 2200;

        private readonly string connectionString =
            ConfigurationManager.ConnectionStrings["bibleoteka"].ConnectionString;

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
                SELECT [Name], BookFile
                FROM Book
                WHERE BookId = @id";

            using (var conn = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", bookId);
                conn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        MessageBox.Show("Книга не найдена.");
                        ShowSinglePageMessage("Книга не найдена.");
                        return;
                    }

                    var title = reader["Name"]?.ToString() ?? "Книга";
                    var fileBytes = reader["BookFile"] == DBNull.Value ? null : (byte[])reader["BookFile"];

                    BookTitleText.Text = title;

                    if (fileBytes == null || fileBytes.Length == 0)
                    {
                        ShowSinglePageMessage("Файл книги отсутствует.");
                        return;
                    }

                    try
                    {
                        ReadBookContent(title, fileBytes);
                    }
                    catch (Exception ex)
                    {
                        ShowSinglePageMessage("Не удалось открыть книгу.");
                        MessageBox.Show(
                            "Не удалось открыть книгу.\n" + ex.Message,
                            "Ошибка",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ReadBookContent(string title, byte[] fileBytes)
        {
            switch (DetermineBookContentKind(fileBytes))
            {
                case BookContentKind.Pdf:
                    OpenPdfInExternalViewer(title, fileBytes);
                    ShowSinglePageMessage("PDF открыт во внешнем приложении.");
                    return;

                case BookContentKind.Fb2:
                    BuildPages(ExtractFb2Text(fileBytes));
                    ShowCurrentPage();
                    return;

                default:
                    BuildPages(DecodePlainText(fileBytes));
                    ShowCurrentPage();
                    return;
            }
        }

        private BookContentKind DetermineBookContentKind(byte[] fileBytes)
        {
            if (HasPdfSignature(fileBytes))
                return BookContentKind.Pdf;

            if (LooksLikeFb2(fileBytes))
                return BookContentKind.Fb2;

            return BookContentKind.PlainText;
        }

        private static bool HasPdfSignature(byte[] fileBytes)
        {
            if (fileBytes == null || fileBytes.Length < 5)
                return false;

            return fileBytes[0] == (byte)'%' &&
                   fileBytes[1] == (byte)'P' &&
                   fileBytes[2] == (byte)'D' &&
                   fileBytes[3] == (byte)'F' &&
                   fileBytes[4] == (byte)'-';
        }

        private bool LooksLikeFb2(byte[] fileBytes)
        {
            if (fileBytes == null || fileBytes.Length == 0)
                return false;

            try
            {
                var document = LoadXmlDocument(fileBytes);
                return string.Equals(
                    document.Root?.Name.LocalName,
                    "FictionBook",
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                foreach (var preview in GetDecodedPreviews(fileBytes))
                {
                    if (preview.IndexOf("<fictionbook", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }

                return false;
            }
        }

        private string ExtractFb2Text(byte[] fileBytes)
        {
            var document = LoadXmlDocument(fileBytes);
            if (document.Root == null)
                return "Текст книги пуст.";

            var builder = new StringBuilder();

            foreach (var body in document.Root.Elements().Where(x => x.Name.LocalName == "body"))
                AppendFb2Node(body, builder);

            return NormalizeText(builder.ToString());
        }

        private static XDocument LoadXmlDocument(byte[] fileBytes)
        {
            using (var stream = new MemoryStream(fileBytes))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore
            }))
            {
                return XDocument.Load(reader, LoadOptions.None);
            }
        }

        private void AppendFb2Node(XElement element, StringBuilder builder)
        {
            if (element == null)
                return;

            switch (element.Name.LocalName)
            {
                case "body":
                case "section":
                case "annotation":
                case "epigraph":
                case "cite":
                case "poem":
                case "stanza":
                    foreach (var child in element.Elements())
                        AppendFb2Node(child, builder);

                    AppendBlankLine(builder);
                    return;

                case "title":
                    AppendHeading(builder, CollectBlockText(element));
                    return;

                case "subtitle":
                    AppendParagraph(builder, element.Value);
                    return;

                case "p":
                    AppendParagraph(builder, element.Value);
                    return;

                case "empty-line":
                    AppendBlankLine(builder);
                    return;

                case "image":
                case "binary":
                    return;

                default:
                    if (element.Elements().Any())
                    {
                        foreach (var child in element.Elements())
                            AppendFb2Node(child, builder);

                        return;
                    }

                    AppendParagraph(builder, element.Value);
                    return;
            }
        }

        private static string CollectBlockText(XElement element)
        {
            if (element == null)
                return string.Empty;

            var lines = element
                .DescendantsAndSelf()
                .Where(x => x.Name.LocalName == "p")
                .Select(x => NormalizeInlineText(x.Value))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (lines.Count > 0)
                return string.Join(Environment.NewLine, lines);

            return NormalizeInlineText(element.Value);
        }

        private static void AppendHeading(StringBuilder builder, string text)
        {
            var normalized = NormalizeInlineText(text);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            if (builder.Length > 0 && !EndsWithBlankLine(builder))
                builder.AppendLine();

            builder.AppendLine(normalized);
            builder.AppendLine();
        }

        private static void AppendParagraph(StringBuilder builder, string text)
        {
            var normalized = NormalizeInlineText(text);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            builder.AppendLine(normalized);
            builder.AppendLine();
        }

        private static void AppendBlankLine(StringBuilder builder)
        {
            if (builder.Length == 0)
                return;

            if (!EndsWithBlankLine(builder))
                builder.AppendLine();
        }

        private static bool EndsWithBlankLine(StringBuilder builder)
        {
            var text = builder.ToString();
            return text.EndsWith(Environment.NewLine + Environment.NewLine);
        }

        private string DecodePlainText(byte[] fileBytes)
        {
            var bestText = GetDecodedCandidates(fileBytes)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderByDescending(ScoreTextQuality)
                .FirstOrDefault();

            return NormalizeText(bestText);
        }

        private IEnumerable<string> GetDecodedCandidates(byte[] fileBytes)
        {
            yield return ReadWithBomAwareness(fileBytes);

            foreach (var preview in GetDecodedPreviews(fileBytes))
                yield return preview;
        }

        private IEnumerable<string> GetDecodedPreviews(byte[] fileBytes)
        {
            yield return TryDecode(fileBytes, Encoding.UTF8);
            yield return TryDecode(fileBytes, Encoding.GetEncoding(1251));
            yield return TryDecode(fileBytes, Encoding.Unicode);
            yield return TryDecode(fileBytes, Encoding.BigEndianUnicode);
        }

        private static string ReadWithBomAwareness(byte[] fileBytes)
        {
            if (fileBytes == null || fileBytes.Length == 0)
                return string.Empty;

            using (var stream = new MemoryStream(fileBytes))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                return reader.ReadToEnd();
            }
        }

        private static string TryDecode(byte[] fileBytes, Encoding encoding)
        {
            if (fileBytes == null || fileBytes.Length == 0)
                return string.Empty;

            try
            {
                return encoding.GetString(fileBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int ScoreTextQuality(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return int.MinValue;

            var score = 0;
            foreach (var ch in text.Take(4000))
            {
                if (ch == '\0')
                    score -= 50;
                else if (ch == '\uFFFD')
                    score -= 25;
                else if (char.IsControl(ch) && ch != '\r' && ch != '\n' && ch != '\t')
                    score -= 8;
                else if (char.IsLetterOrDigit(ch))
                    score += 4;
                else if (char.IsWhiteSpace(ch))
                    score += 2;
                else
                    score += 1;
            }

            return score;
        }

        private static string NormalizeInlineText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var parts = text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\t", " ")
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x));

            return string.Join(" ", parts);
        }

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "Текст книги пуст.";

            var normalizedLines = text
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\t", "    ")
                .Split('\n');

            var builder = new StringBuilder();
            var previousLineWasBlank = false;

            foreach (var rawLine in normalizedLines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (!previousLineWasBlank)
                    {
                        builder.AppendLine();
                        previousLineWasBlank = true;
                    }

                    continue;
                }

                builder.AppendLine(line);
                previousLineWasBlank = false;
            }

            var result = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? "Текст книги пуст." : result;
        }

        private void OpenPdfInExternalViewer(string title, byte[] fileBytes)
        {
            var tempPath = BuildTemporaryPdfPath(title);
            File.WriteAllBytes(tempPath, fileBytes);

            Process.Start(new ProcessStartInfo(tempPath)
            {
                UseShellExecute = true
            });
        }

        private string BuildTemporaryPdfPath(string title)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var safeName = new string((title ?? string.Empty)
                .Where(ch => !invalidChars.Contains(ch))
                .ToArray())
                .Trim();

            if (string.IsNullOrWhiteSpace(safeName))
                safeName = "book";

            return Path.Combine(Path.GetTempPath(), safeName + "_" + bookId + ".pdf");
        }

        private void BuildPages(string text)
        {
            pages.Clear();

            if (string.IsNullOrWhiteSpace(text))
            {
                ShowSinglePageMessage("Текст книги пуст.");
                return;
            }

            var currentPageBuilder = new StringBuilder();
            var paragraphs = text.Split(new[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawParagraph in paragraphs)
            {
                var paragraph = rawParagraph.Trim();
                if (string.IsNullOrWhiteSpace(paragraph))
                    continue;

                foreach (var piece in SplitLongParagraph(paragraph))
                {
                    var separatorLength = currentPageBuilder.Length == 0 ? 0 : 2;
                    if (currentPageBuilder.Length > 0 &&
                        currentPageBuilder.Length + separatorLength + piece.Length > TargetPageSize)
                    {
                        AddPage(currentPageBuilder.ToString());
                        currentPageBuilder.Clear();
                    }

                    if (currentPageBuilder.Length > 0)
                        currentPageBuilder.AppendLine().AppendLine();

                    currentPageBuilder.Append(piece);
                }
            }

            if (currentPageBuilder.Length > 0)
                AddPage(currentPageBuilder.ToString());

            if (pages.Count == 0)
                pages.Add("Текст книги пуст.");

            currentPage = Math.Max(1, Math.Min(currentPage, pages.Count));
        }

        private IEnumerable<string> SplitLongParagraph(string paragraph)
        {
            if (paragraph.Length <= TargetPageSize)
            {
                yield return paragraph;
                yield break;
            }

            var index = 0;
            while (index < paragraph.Length)
            {
                var length = Math.Min(TargetPageSize, paragraph.Length - index);
                var end = FindParagraphSplit(paragraph, index, length);
                var piece = paragraph.Substring(index, end - index).Trim();

                if (!string.IsNullOrWhiteSpace(piece))
                    yield return piece;

                index = end;
            }
        }

        private static int FindParagraphSplit(string paragraph, int startIndex, int maxLength)
        {
            var hardEnd = Math.Min(paragraph.Length, startIndex + maxLength);
            if (hardEnd >= paragraph.Length)
                return paragraph.Length;

            var searchStart = Math.Max(startIndex + 200, hardEnd - 250);
            for (var i = hardEnd - 1; i >= searchStart; i--)
            {
                var ch = paragraph[i];
                if (ch == '.' || ch == '!' || ch == '?' || ch == ';' || ch == ':' || ch == ' ')
                    return i + 1;
            }

            return hardEnd;
        }

        private void AddPage(string text)
        {
            var pageText = string.IsNullOrWhiteSpace(text) ? " " : text.Trim();
            pages.Add(pageText);
        }

        private void ShowSinglePageMessage(string message)
        {
            pages.Clear();
            pages.Add(message);
            currentPage = 1;
            ShowCurrentPage();
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
            if (pages.Count == 0)
                return;

            currentPage = 1;
            ShowCurrentPage();
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (pages.Count == 0)
                return;

            if (currentPage > 1)
            {
                currentPage--;
                ShowCurrentPage();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (pages.Count == 0)
                return;

            if (currentPage < pages.Count)
            {
                currentPage++;
                ShowCurrentPage();
            }
        }

        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            if (pages.Count == 0)
                return;

            currentPage = pages.Count;
            ShowCurrentPage();
        }

        private void PageNumberText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || pages.Count == 0)
                return;

            int pageNumber;
            if (!int.TryParse(PageNumberText.Text, out pageNumber))
            {
                PageNumberText.Text = currentPage.ToString();
                return;
            }

            pageNumber = Math.Max(1, Math.Min(pageNumber, pages.Count));
            currentPage = pageNumber;
            ShowCurrentPage();
        }
    }
}
