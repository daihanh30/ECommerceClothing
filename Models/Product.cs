using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceClothing.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Please enter the product name.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Please enter the product price.")]
        [Range(0, double.MaxValue, ErrorMessage = "The product price must not be a negative number.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }
        public string Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Please select a Product Type")]
        public int? ProductTypeId { get; set; }

        [ForeignKey("ProductTypeId")]
        public ProductType? ProductTypeObj { get; set; }

        public List<ProductImage> Images { get; set; } = new();
        public ICollection<ProductSize> ProductSizes { get; set; } = new List<ProductSize>();
    }

    public class ProductImage
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ImageUrl { get; set; }

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }
    }
}