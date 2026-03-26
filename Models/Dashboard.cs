using System.Collections.Generic;

namespace ECommerceClothing.Models
{
    public class Dashboard
    {
        public List<Product> Products { get; set; } = new List<Product>();
        public int TotalProducts { get; set; }
        public int TotalOrders { get; set; }
        public int TotalCustomers { get; set; } 
        public decimal TotalRevenue { get; set; }

        public List<Order> RecentOrders { get; set; } = new List<Order>();
        public List<int> OrderStatusCounts { get; set; } = new List<int>();
        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<decimal> ChartData { get; set; } = new List<decimal>();
    }
}