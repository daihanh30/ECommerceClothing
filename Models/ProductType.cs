using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceClothing.Models
{
    [Table("ProductTypes")] // Đảm bảo trỏ đúng bảng ProductTypes
    public class ProductType
    {
        [Key]
        public int ProductTypeId { get; set; } // Khớp với SQL: ProductTypeId

        // Đổi tên biến thành "Name" cho hết lỗi C#
        // Nhưng dùng [Column] để ép nó map vào cột "ProductType" trong SQL
        [Required]
        [Column("ProductType")]
        public string Name { get; set; }

        // Khóa ngoại liên kết với Category
        public int? CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }
    }
}

