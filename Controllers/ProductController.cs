using ECommerceClothing.Data;
using ECommerceClothing.Models;
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

        //shop page
        public async Task<IActionResult> Index(string query, int? categoryId, int? typeId, int page = 1)
        {
            var products = _context.Products
                .Include(p => p.Images)
                .Include(p => p.ProductSizes)
                .Include(p => p.ProductTypeObj)
                    .ThenInclude(pt => pt.Category)
                .AsQueryable();

            //Lọc theo Category 
            if (categoryId.HasValue)
            {
                products = products.Where(p => p.ProductTypeObj.CategoryId == categoryId);

                var categoryInfo = await _context.Categories.FindAsync(categoryId);
                ViewBag.CategoryName = categoryInfo != null ? categoryInfo.Name.ToUpper() : "COLLECTION";
            }

            if (typeId.HasValue)
            {
                products = products.Where(p => p.ProductTypeId == typeId);

                var typeInfo = await _context.ProductTypes.FindAsync(typeId);
                if (typeInfo != null)
                {
                    ViewBag.CategoryName = typeInfo.Name.ToUpper();
                }
            }

            //Lọc theo search
            if (!string.IsNullOrEmpty(query))
            {
                products = products.Where(p => p.Name.Contains(query));
                ViewBag.SearchQuery = query;
                ViewBag.CategoryName = "SEARCH RESULTS";
            }
             
            if (!categoryId.HasValue && !typeId.HasValue && string.IsNullOrEmpty(query))
            {
                ViewBag.CategoryName = "SHOP ALL";
            }

            return View(await products.OrderByDescending(p => p.CreatedAt).ToListAsync());
        }

        //search redirect
        public IActionResult Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return RedirectToAction("Index", new { query = "empty_search" });
            }

            return RedirectToAction("Index", new { query = query.Trim() });
        }

        //product detail
        public async Task<IActionResult> Detail(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.ProductTypeObj)
                    .ThenInclude(pt => pt.Category)
                .Include(p => p.ProductSizes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            // lay 3 danh gia moi nhat
            var reviews = await _context.Reviews
                .Where(r => r.ProductId == id)
                .OrderByDescending(r => r.CreatedAt)
                .Take(3)
                .ToListAsync();

            ViewBag.Reviews = reviews;
            ViewBag.TotalReviews = await _context.Reviews.CountAsync(r => r.ProductId == id);

            string cookieName = "RecentlyViewed";
            string viewedIdsStr = Request.Cookies[cookieName] ?? "";

            List<int> historyIds = viewedIdsStr
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList();

            if (!historyIds.Contains(id))
            {
                historyIds.Insert(0, id);
                if (historyIds.Count > 5) historyIds = historyIds.Take(5).ToList();

                CookieOptions options = new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(7),
                    IsEssential = true,
                    Path = "/"
                };
                Response.Cookies.Append(cookieName, string.Join(",", historyIds), options);
            }

            var recommendedProducts = new List<Product>();

            //Lấy 4 sản phẩm cùng Category với sản phẩm hiện tại
            recommendedProducts = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.ProductTypeObj)
                .Where(p => p.ProductTypeObj.CategoryId == product.ProductTypeObj.CategoryId && p.Id != id)
                .OrderByDescending(p => p.CreatedAt)
                .Take(4)
                .ToListAsync();

            if (!recommendedProducts.Any() && historyIds.Any())
            {
                // Lấy danh sách ProductTypeId của những sản phẩm đã xem
                var recentTypes = await _context.Products
                    .Where(p => historyIds.Contains(p.Id))
                    .Select(p => p.ProductTypeId)
                    .Distinct()
                    .ToListAsync();

                recommendedProducts = await _context.Products
                    .Include(p => p.Images)
                    .Where(p => recentTypes.Contains(p.ProductTypeId) && !historyIds.Contains(p.Id) && p.Id != id)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(4)
                    .ToListAsync();
            }

            ViewBag.RecommendedProducts = recommendedProducts;

            return View("ProductDetail", product);
        }

        //quick view api
        [HttpGet]
        public async Task<IActionResult> GetProductJson(int id)
        {
            var p = await _context.Products
                .Include(x => x.Images)
                .Include(x => x.ProductSizes)
                .Include(x => x.ProductTypeObj)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (p == null) return NotFound();

            return Json(new
            {
                id = p.Id,
                name = p.Name,
                price = p.Price, 
                stock = p.ProductSizes?.Sum(s => s.Quantity) ?? 0,
                image = p.Images?.FirstOrDefault()?.ImageUrl ?? "/images/no-image.png",
                 
                categoryId = p.ProductTypeObj?.CategoryId ?? 0,

                sizes = p.ProductSizes?.Select(s => new {
                    name = s.SizeName,
                    qty = s.Quantity
                }).ToList()
            });
        }

        //all reviews
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