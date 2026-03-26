using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace ECommerceClothing.Models
{
    public class Voucher
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Code { get; set; } // Mã nhập (VD: FREESHIP, HELLO)

        public string Title { get; set; } // Tên hiển thị (VD: Giảm 10%)
        public string Description { get; set; } // Mô tả chi tiết

        public string Type { get; set; } // "Percent" (Phần trăm) hoặc "Fixed" (Tiền mặt)

        [Column(TypeName = "decimal(18,2)")]
        public decimal Value { get; set; } // Giá trị giảm (VD: 0.1 cho 10%, hoặc 20000 cho 20k)
        public decimal MaxReduce { get; set; } // Số tiền giảm tối đa
        public decimal MinOrder { get; set; } // Giá trị đơn hàng tối thiểu

        public int Quantity { get; set; } // Tổng số lượng mã phát ra phát ra
        public int UsedCount { get; set; } = 0; // Đã dùng bao nhiêu lượt (Thêm mới)
        public int UsageLimitPerUser { get; set; } = 1; // Số lần 1 user được dùng (thường là 1)

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public bool IsPublic { get; set; } = true; // True = Public (hiện ở giỏ hàng), False = Hidden (phải nhập tay)
        public bool IsActive { get; set; } = true;
    }
}