using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace OnlineLibrary1.Models
{
    public class MyBookItem
    {
        public int BookId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public int Year { get; set; }
        public string Genre { get; set; }
        public int Pages { get; set; }
        public string Description { get; set; }
        public byte[] CoverBytes { get; set; }
        public string Status { get; set; }
        public BitmapImage CoverImage { get; set; }
        public string Meta => $"{Year} • {Genre} • {Pages} стр.";
        public string DescriptionShort =>
            string.IsNullOrWhiteSpace(Description)
                ? "Описание отсутствует."
                : (Description.Length > 140 ? Description.Substring(0, 140) + "…" : Description);
    }
}
