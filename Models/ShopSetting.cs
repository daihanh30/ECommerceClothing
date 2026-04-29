using System.ComponentModel.DataAnnotations;

namespace ECommerceClothing.Models
{
    public class ShopSetting
    {
        public int Id { get; set; }

        public string ShopName { get; set; } = "NIXONE Shop";
        public string Address { get; set; } = "Ho Chi Minh City, Vietnam";

        [Required(ErrorMessage = "Please enter the latitude.")]
        [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
        public double Latitude { get; set; }

        [Required(ErrorMessage = "Please enter the longitude.")]
        [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
        public double Longitude { get; set; }

        public string? SenderEmail { get; set; }
        public string? SenderPassword { get; set; }
        public string? SenderName { get; set; }
        public string? MapboxToken { get; set; }
    }
}