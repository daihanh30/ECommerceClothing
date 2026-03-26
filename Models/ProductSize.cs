using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceClothing.Models
{
    public class ProductSize
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product Product { get; set; }

        [Required]
        public string SizeName { get; set; } // VD: S, M, L, XL

        [Required]
        public int Quantity { get; set; } // Tồn kho của cái size này
    }
}