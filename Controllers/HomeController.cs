using ECommerceClothing.Data;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ECommerceClothing.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(int page = 1)
        {
            int pageSize = 8; 

            var query = _context.Products
                .Include(p => p.Images)
                .Include(p => p.ProductSizes) 
                .OrderByDescending(p => p.CreatedAt);

            // 2. Đếm tổng số lượng sản phẩm và tính tổng số trang
            int totalProducts = query.Count();
            int totalPages = (int)Math.Ceiling((double)totalProducts / pageSize);

            // 3. Phân trang: Skip (Bỏ qua) sản phẩm trang trước, Take (Lấy) 8 cái của trang này
            var products = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 4. Quăng số liệu phân trang ra View (HTML) để vẽ nút bấm
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(products);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}