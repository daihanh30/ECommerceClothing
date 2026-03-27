using System.ComponentModel.DataAnnotations;

namespace ECommerceClothing.Models
{
    public class CartItem
    {
        [Key]
        public int Id { get; set; }
        public string? UserId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductImage { get; set; }
        public decimal Price { get; set; }
        public string? Size { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice => Price * Quantity;

        // THÊM DÒNG NÀY VÀO NÍ ƠI
       
    }
}
