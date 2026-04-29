namespace ECommerceClothing.Models
{
    public class CheckoutViewModel
    { 
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string? Note { get; set; }

        public decimal ShippingFee { get; set; } 
        public string? VoucherCode { get; set; }
        public decimal DiscountAmount { get; set; }
        public List<CartItemViewModel> CartItems { get; set; } = new List<CartItemViewModel>();
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = "COD"; 
    }

    public class CartItemViewModel
    {
        public string ProductName { get; set; }
        public string ProductImage { get; set; }
        public string? Size { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
