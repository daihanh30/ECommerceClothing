using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ECommerceClothing.Models
{
    // 👇 Kế thừa IdentityUser là có ngay: Id, UserName, Email, PasswordHash, PhoneNumber...
    public class AppUser : IdentityUser
    {
        // Mình chỉ cần thêm những cái Identity chưa có:

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } // Tên hiển thị (Hi, Hanh)

        public string? Address { get; set; } // Địa chỉ chính (Optional)

        public string? AvatarUrl { get; set; } // Link ảnh đại diện (để sau này làm upload ảnh)

        public DateTime CreatedAt { get; set; } = DateTime.Now; // Ngày tạo tài khoản
    }
}