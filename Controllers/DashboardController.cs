using System;
using System.Collections.Generic;
using System.Linq;
using ECommerceClothing.Data;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceClothing.Controllers
{
    // [Authorize(Roles = "Admin")] // Bật lại sau khi xong Login
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // 1. ĐẾM SỐ LƯỢNG CHO BIỂU ĐỒ TRÒN (Chấp luôn khoảng trắng bị dư trong Database)
            int delivered = _context.Orders.Count(o => o.Status.Contains("Delivered"));
            int cancelled = _context.Orders.Count(o => o.Status.Contains("Cancelled"));

            // 2. Tính Tổng doanh thu (👉 ĐÃ FIX: Chặn triệt để đơn Hủy dù có khoảng trắng)
            var totalRevenue = _context.Orders
                .Where(o => !o.Status.Contains("Cancelled"))
                .Sum(o => o.TotalAmount);

            // 3. Lấy 5 đơn hàng mới nhất
            var recentOrders = _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToList();

            // 4. LẤY DATA THẬT CHO BIỂU ĐỒ ĐƯỜNG (7 NGÀY QUA)
            var sevenDaysAgo = DateTime.Now.Date.AddDays(-6);

            // 👉 ĐÃ FIX: Chặn triệt để đơn Hủy cho biểu đồ đường
            var ordersLast7Days = _context.Orders
                .Where(o => !o.Status.Contains("Cancelled") && o.OrderDate >= sevenDaysAgo)
                .ToList();

            var labels = new List<string>();
            var revenueData = new List<decimal>();

            for (int i = 6; i >= 0; i--)
            {
                var targetDate = DateTime.Now.Date.AddDays(-i);
                labels.Add(targetDate.ToString("dd/MM")); // Trục ngang (Ngày)

                var dailyTotal = ordersLast7Days
                    .Where(o => o.OrderDate.Date == targetDate.Date)
                    .Sum(o => o.TotalAmount);

                revenueData.Add(dailyTotal);
            }

            var dashboard = new Dashboard
            {
                Products = _context.Products.OrderByDescending(p => p.CreatedAt).ToList(),
                TotalProducts = _context.Products.Count(),
                TotalCustomers = _context.Users.Count(),
                TotalOrders = _context.Orders.Count(),
                TotalRevenue = totalRevenue,
                RecentOrders = recentOrders,

                OrderStatusCounts = new List<int> { delivered, cancelled },
                ChartLabels = labels,
                ChartData = revenueData
            };

            return View(dashboard);
        }
    }
}