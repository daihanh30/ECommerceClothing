using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; 

namespace ECommerceClothing.Models
{
    public class CartItem
    {
        [Key]
        public int Id { get; set; }

        public string? UserId { get; set; }

        public int ProductId { get; set; }

        public string? Size { get; set; }

        public int Quantity { get; set; }

        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

    }
}