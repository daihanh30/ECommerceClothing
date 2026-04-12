using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ECommerceClothing.Models
{
    // 👇 Kế thừa IdentityUser là có ngay: Id, UserName, Email, PasswordHash, PhoneNumber...
    public class AppUser : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; }

        public string? Address { get; set; } 

        public string? AvatarUrl { get; set; } 

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? ResetPasswordOtp { get; set; }

        public DateTime? OtpExpiryTime { get; set; }
    }
}