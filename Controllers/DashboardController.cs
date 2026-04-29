using System;
using System.Collections.Generic;
using System.Linq;
using ECommerceClothing.Data;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceClothing.Controllers
{ 
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        { 
            int delivered = _context.Orders.Count(o => o.Status.Contains("Delivered"));
            int cancelled = _context.Orders.Count(o => o.Status.Contains("Cancelled"));
             
            var totalRevenue = _context.Orders
                .Where(o => !o.Status.Contains("Cancelled"))
                .Sum(o => o.TotalAmount);
             
            var recentOrders = _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToList();
             
            var sevenDaysAgo = DateTime.Now.Date.AddDays(-6);
             
            var ordersLast7Days = _context.Orders
                .Where(o => !o.Status.Contains("Cancelled") && o.OrderDate >= sevenDaysAgo)
                .ToList();

            var labels = new List<string>();
            var revenueData = new List<decimal>();

            for (int i = 6; i >= 0; i--)
            {
                var targetDate = DateTime.Now.Date.AddDays(-i);
                labels.Add(targetDate.ToString("dd/MM")); 

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