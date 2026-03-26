namespace ECommerceClothing.Models
{
    public class User
    {
        public int Id { get; set; }

        public string FullName { get; set; }

        public string Email { get; set; }

        public string Password { get; set; } // demo: plain text
        public string PasswordHash { get; set; }

        public string Role { get; set; } // "Admin" | "Customer"

        public DateTime CreatedAt { get; set; }
    }
}
