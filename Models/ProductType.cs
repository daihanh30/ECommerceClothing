using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceClothing.Models
{
    [Table("ProductTypes")]
    public class ProductType
    {
        [Key]
        public int ProductTypeId { get; set; }

        [Required]
        [Column("ProductType")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Please select a Category")]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        public List<Product> Products { get; set; } = new();
    }
}