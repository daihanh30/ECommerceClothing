using System;
using System.Collections.Generic;
using ECommerceClothing.Models;

namespace ECommerceClothing.ViewModels 
{
    public class CustomerDetail
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }

        public int TotalOrders { get; set; }
        public decimal TotalSpent { get; set; }
        public double CancelRate { get; set; }

        public List<Order> OrderHistory { get; set; }

    }


}