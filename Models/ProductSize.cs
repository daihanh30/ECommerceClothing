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
        public string SizeName { get; set; }  

        [Required] 
        [Range(0, int.MaxValue, ErrorMessage = "The quantity of each size must not be a negative number.")]
        public int Quantity { get; set; }
    }
}