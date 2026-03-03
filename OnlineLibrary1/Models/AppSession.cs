using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OnlineLibrary1.Models
{
    public class AppSession
    {
        public static int? UserId { get; private set; }
        public static string Username { get; private set; }
        public static string Email { get; private set; }
        public static string Role { get; private set; }
        public static DateTime? CreatedAt { get; private set; }

        public static bool IsAuthenticated => UserId.HasValue;

        public static void SignIn(int userId, string username, string email, string role, DateTime? createdAt)
        {
            UserId = userId;
            Username = username ?? "";
            Email = email ?? "";
            Role = role ?? "";
            CreatedAt = createdAt;
        }

        public static void SignOut()
        {
            UserId = null;
            Username = "";
            Email = "";
            Role = "";
            CreatedAt = null;
        }
    }
}
