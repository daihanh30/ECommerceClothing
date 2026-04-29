using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceClothing.Models
{
    public class Voucher
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Please enter Voucher code")]
        [MaxLength(50)]
        public string Code { get; set; }

        [Required(ErrorMessage = "Please enter a display name")]
        public string Title { get; set; }
        public string Description { get; set; }

        public string Type { get; set; } 

        [Range(0, double.MaxValue, ErrorMessage = "Decreased value cannot be negative")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Value { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Maximum discount amount cannot be negative")]
        public decimal MaxReduce { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Minimum order value cannot be negative")]
        public decimal MinOrder { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity issued must be 1 or more")]
        public int Quantity { get; set; }

        public int UsedCount { get; set; } = 0;

        [Range(1, int.MaxValue, ErrorMessage = "Limit per user must be 1 or more")]
        public int UsageLimitPerUser { get; set; } = 1;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public bool IsPublic { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}