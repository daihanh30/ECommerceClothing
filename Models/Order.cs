using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; 

namespace ECommerceClothing.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }
        public string UserId { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;

        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DiscountAmount { get; set; }
        public int? VoucherId { get; set; }

        public string PaymentMethod { get; set; }
        public string Status { get; set; } = "Pending";
        public string? Note { get; set; }

        [ForeignKey("UserId")]
        public virtual AppUser User { get; set; }

        [ForeignKey("VoucherId")]
        public virtual Voucher Voucher { get; set; }
        public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    }
}