using System.ComponentModel.DataAnnotations;

namespace ECommerceClothing.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Category name is required")]
        public string Name { get; set; } = string.Empty;

        public List<ProductType> ProductTypes { get; set; } = new();
    }
}