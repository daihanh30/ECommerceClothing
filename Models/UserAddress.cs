using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceClothing.Models
{
    public class UserAddress
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } // ID của người dùng (từ hệ thống đăng nhập)

        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; } // Địa chỉ cụ thể

        public bool IsDefault { get; set; } // Đánh dấu là địa chỉ mặc định
    }
}