using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OnlineLibrary1.Models
{
    public class AdminUserItem
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string RoleName { get; set; }
        public DateTime? Created { get; set; }
        public string CreatedText => Created.HasValue ? Created.Value.ToString("dd.MM.yyyy") : "—";
    }
}
