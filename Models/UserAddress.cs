using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceClothing.Models
{
    public class UserAddress
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } 

        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; }

        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; } 

        public bool IsDefault { get; set; } 
    }
}