using ECommerceClothing.Data;
using ECommerceClothing.Models; // Đảm bảo có dòng này để nhận diện Model Review
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerceClothing.Controllers
{
    public class ProductController : Controller
    {
        private readonly AppDbContext _context;

        public ProductController(AppDbContext context)
        {
            _context = context;
        }

        // ================== 1. SHOP PAGE ==================
        public async Task<IActionResult> Index(string query, int? categoryId)
        {
            var products = _context.Products
                .Include(p => p.Images)
                .Include(p => p.ProductSizes)
                .Include(p => p.Category)
                .AsQueryable();

            if (categoryId.HasValue)
            {
                products = products.Where(p => p.CategoryId == categoryId);

                //switch (categoryId)
                //{
                //    case 1: ViewBag.CategoryName = "TOPS"; break;
                //    case 2: ViewBag.CategoryName = "BOTTOMS"; break;
                //    case 5: ViewBag.CategoryName = "ACCESSORIES"; break;
                //    default: ViewBag.CategoryName = "COLLECTION"; break;
                //}
                var categoryInfo = await _context.Categories.FindAsync(categoryId);
                ViewBag.CategoryName = categoryInfo != null ? categoryInfo.Name.ToUpper() : "COLLECTION";
            }

            if (!string.IsNullOrEmpty(query))
            {
                products = products.Where(p => p.Name.Contains(query));
                ViewBag.SearchQuery = query;
                ViewBag.CategoryName = "SEARCH RESULTS";
            }

            if (!categoryId.HasValue && string.IsNullOrEmpty(query))
            {
                ViewBag.CategoryName = "SHOP ALL";
            }

            return View(await products.OrderByDescending(p => p.CreatedAt).ToListAsync());
        }

        // ================== 2. SEARCH REDIRECT ==================
        public IActionResult Search(string query)
        {
            return RedirectToAction("Index", new { query = query });
        }

        // ================== 3. PRODUCT DETAIL (UPDATED WITH FEEDBACK) ==================
        public async Task<IActionResult> Detail(int id)
        {
            // Lấy thông tin sản phẩm và các bảng liên quan
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.ProductSizes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            // 👇 LẤY DỮ LIỆU FEEDBACK 👇
            // Lấy 3 đánh giá mới nhất
            var reviews = await _context.Reviews
                .Where(r => r.ProductId == id)
                .OrderByDescending(r => r.CreatedAt)
                .Take(3)
                .ToListAsync();

            // Đếm tổng số lượt đánh giá để hiện lên giao diện
            ViewBag.Reviews = reviews;
            ViewBag.TotalReviews = await _context.Reviews.CountAsync(r => r.ProductId == id);

            // Related Products (Sản phẩm liên quan)
            var relatedProducts = await _context.Products
                .Include(p => p.Images)
                .Where(p => p.CategoryId == product.CategoryId && p.Id != id)
                .OrderByDescending(p => p.CreatedAt)
                .Take(4)
                .ToListAsync();

            ViewBag.RelatedProducts = relatedProducts;

            return View("ProductDetail", product);
        }

        // ================== 4. QUICK VIEW API (FOR POPUP) ==================
        [HttpGet]
        public async Task<IActionResult> GetProductJson(int id)
        {
            var p = await _context.Products
                .Include(x => x.Images)
                .Include(x => x.ProductSizes)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (p == null) return NotFound();

            return Json(new
            {
                id = p.Id,
                name = p.Name,
                price = p.Price,
                stock = p.Stock,
                image = p.Images?.FirstOrDefault()?.ImageUrl ?? "/images/no-image.png",

                categoryId = p.CategoryId,

                sizes = p.ProductSizes?.Select(s => new {
                    name = s.SizeName,
                    qty = s.Quantity
                }).ToList()
            });
        }

        // Action xem tất cả đánh giá
        public async Task<IActionResult> AllReviews(int productId)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null) return NotFound();

            var allReviews = await _context.Reviews
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.ProductName = product.Name;
            ViewBag.ProductId = product.Id;

            return View(allReviews);
        }
    }
}