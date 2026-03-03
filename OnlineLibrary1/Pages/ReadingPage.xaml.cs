using System;
using System.Collections.Generic;
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
        private int currentPage = 1;
        private int totalPages = 100;

        public ReadingPage(string bookTitle)
        {
            InitializeComponent();
            BookTitleText.Text = bookTitle;
            LoadPageContent();
        }

        private void LoadPageContent()
        {
            // Загрузка содержимого страницы (в реальном приложении из БД)
            BookContentText.Text = $"Страница {currentPage}\n\n" +
                                  "Здесь будет текст книги. В реальном приложении " +
                                  "текст будет загружаться из базы данных постранично.\n\n" +
                                  "Lorem ipsum dolor sit amet, consectetur adipiscing elit. " +
                                  "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";

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
            LoadPageContent();
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage > 1)
            {
                currentPage--;
                LoadPageContent();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage < totalPages)
            {
                currentPage++;
                LoadPageContent();
            }
        }

        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            currentPage = totalPages;
            LoadPageContent();
        }
    }
}
