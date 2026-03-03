using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OnlineLibrary1.Models
{
    public class BookItem
    {
        public int BookId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public int Year { get; set; }
        public string Genre { get; set; }
        public string AgeRating { get; set; }
        public string ISBN { get; set; }
        public int Pages { get; set; }
        public string Description { get; set; }
        public byte[] CoverBytes { get; set; }
    }
}
