using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http; // Bắt buộc thêm dòng này cho IFormFile

namespace ECommerceClothing.Models
{
    public class Reviews
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public Product Product { get; set; }

        public int OrderId { get; set; }
        [ForeignKey("OrderId")]
        public Order Order { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(500)]
        public string Comment { get; set; }

        // 👇 THÊM 2 DÒNG NÀY VÀO 👇
        public string? ImageUrl { get; set; } // Đường dẫn ảnh lưu vào DB (Cho phép null)

        [NotMapped]
        public IFormFile? ImageFile { get; set; } // File ảnh up lên từ Form (Không lưu cột này vào DB)

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}