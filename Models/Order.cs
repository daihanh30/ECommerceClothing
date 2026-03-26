using System.ComponentModel.DataAnnotations;

namespace ECommerceClothing.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }
        public string UserId { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;

        public string FullName { get; set; } // Thống nhất tên này nhé
        public string PhoneNumber { get; set; }
        public string Address { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal DiscountAmount { get; set; } // Nên có để lưu số tiền đã giảm
        public string? VoucherCode { get; set; }

        public string PaymentMethod { get; set; }
        public string Status { get; set; } = "Pending";
        public string? Note { get; set; }

        // Quan hệ 1-N: Một đơn hàng có nhiều dòng chi tiết
        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
    }
}