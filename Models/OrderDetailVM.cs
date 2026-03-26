namespace ECommerceClothing.Models
{
    public class OrderDetailVM
    {
        public Order OrderInfo { get; set; } // Chứa FullName, Address, Status...
        public List<OrderItemInfo> Items { get; set; } // Danh sách món hàng có ảnh

        // Tiền hàng (chưa phí ship)
        public decimal Subtotal => Items.Sum(x => x.Price * x.Quantity);
    }

    public class OrderItemInfo
    {
        public string ProductName { get; set; }
        public string ProductImage { get; set; }
        public string Size { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}