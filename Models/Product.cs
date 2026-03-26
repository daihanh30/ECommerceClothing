using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceClothing.Models
{
    public class Product
    {
        public int Id { get; set; }

        public string Name { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public int CategoryId { get; set; }
        public Category Category { get; set; }

        public string? Size { get; set; } // S,M,L,XL
        public int Stock { get; set; }

        public string Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<ProductImage> Images { get; set; }
        public int? ProductTypeId { get; set; }
        [ForeignKey("ProductTypeId")]
        public ProductType? ProductTypeObj { get; set; }
        public ICollection<ProductSize> ProductSizes { get; set; } = new List<ProductSize>();
    }

    public class ProductImage
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ImageUrl { get; set; }
    }
}
