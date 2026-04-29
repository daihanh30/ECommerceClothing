using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using ECommerceClothing.Models;
using ECommerceClothing.Data;
using System.Linq;
using System.Collections.Generic;

namespace ECommerceClothing.Controllers
{
    [Authorize] 
    public class CartController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
         
        public CartController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Hàm hỗ trợ lấy UserId
        private string GetUserId() => _userManager.GetUserId(User);

        //Trang danh sách giỏ hàng
        public IActionResult Index()
        {
            var userId = GetUserId();

            // Lấy giỏ hàng từ db, Include Product để View có thể hiển thị  
            var cart = _context.CartItems
                               .Include(c => c.Product)
                               .ThenInclude(p => p.Images)
                               .Where(c => c.UserId == userId)
                               .ToList();

            // Nếu có dữ liệu tick từ Buy Again truyền qua, quăng vào ViewBag
            if (TempData["PreTickedItems"] != null)
            {
                ViewBag.PreTickedItems = TempData["PreTickedItems"].ToString().Split(',').ToList();
            }

            return View(cart);
        }

        // Thêm vào giỏ 
        [HttpPost]
        public IActionResult AddToCart(int Id, string Size, int Quantity)
        {
            var userId = GetUserId();
            var product = _context.Products.Find(Id);
            if (product == null) return NotFound();

            // Kiểm tra tồn kho dựa trên bảng ProductSizes (mỗi Size có số lượng riêng)
            var stockInfo = _context.ProductSizes.FirstOrDefault(ps => ps.ProductId == Id && ps.SizeName == Size);
            if (stockInfo == null || stockInfo.Quantity <= 0)
            {
                return Json(new { success = false, msg = "Out of stock for this size" });
            }

            // Kiểm tra sản phẩm đã có trong giỏ hàng của user này chưa
            var existingItem = _context.CartItems.FirstOrDefault(c => c.ProductId == Id && c.Size == Size && c.UserId == userId);

            if (existingItem != null)
            {
                if (existingItem.Quantity + Quantity > stockInfo.Quantity)
                {
                    return Json(new { success = false, msg = "Not enough stock" });
                }
                existingItem.Quantity += Quantity;
            }
            else
            {
                if (Quantity > stockInfo.Quantity)
                {
                    return Json(new { success = false, msg = "Not enough stock" });
                }

                // Lưu vào DB 
                var newCartItem = new CartItem
                {
                    UserId = userId,
                    ProductId = Id,
                    Size = Size,
                    Quantity = Quantity
                };
                _context.CartItems.Add(newCartItem);
            }

            _context.SaveChanges(); 
            return Redirect(Request.Headers["Referer"].ToString());
        }

        //Cập nhật số lượng (Cho AJAX)
        [HttpPost]
        public IActionResult UpdateQuantity(int id, string size, int change)
        {
            var userId = GetUserId();
            var item = _context.CartItems.FirstOrDefault(c => c.ProductId == id && c.Size == size && c.UserId == userId);

            if (item != null)
            {
                item.Quantity += change;

                if (item.Quantity <= 0)
                {
                    _context.CartItems.Remove(item);
                }
                else
                {
                }

                _context.SaveChanges(); 
                return Json(new { success = true });
            }

            return Json(new { success = false, msg = "Item not found in cart" });
        }

        //Thêm vào giỏ bằng AJAX 
        [HttpPost]
        public IActionResult AddToCartAjax(int Id, string Size, int Quantity)
        {
            var userId = GetUserId();

            var product = _context.Products.Find(Id);
            if (product == null) return Json(new { success = false, msg = "Product not found" });

            // Kiểm tra tồn kho dựa trên Size
            var stockInfo = _context.ProductSizes.FirstOrDefault(ps => ps.ProductId == Id && ps.SizeName == Size);
            if (stockInfo == null || stockInfo.Quantity <= 0)
            {
                return Json(new { success = false, msg = "Out of stock for this size" });
            }

            var existingItem = _context.CartItems.FirstOrDefault(c => c.ProductId == Id && c.Size == Size && c.UserId == userId);

            int newQuantity = Quantity;
            if (existingItem != null)
            {
                newQuantity = existingItem.Quantity + Quantity;
            }

            if (newQuantity > stockInfo.Quantity)
            {
                return Json(new { success = false, msg = $"Only {stockInfo.Quantity} items left for this size" });
            }

            if (existingItem != null)
            {
                existingItem.Quantity = newQuantity;
            }
            else
            {
                _context.CartItems.Add(new CartItem
                {
                    UserId = userId,
                    ProductId = Id,
                    Size = Size,
                    Quantity = Quantity
                });
            }

            _context.SaveChanges();
            return Json(new { success = true });
        }

        //Lấy dữ liệu giỏ hàng để vẽ lên Sidebar
        [HttpGet]
        public IActionResult GetCartJson()
        {
            var userId = GetUserId();

            // Include Product để lấy tên, hình, giá
            var cartItems = _context.CartItems
                                    .Include(c => c.Product)
                                        .ThenInclude(p => p.Images)
                                    .Where(c => c.UserId == userId)
                                    .ToList();

            var total = cartItems.Sum(x => x.Quantity * x.Product.Price);

            var jsonItems = cartItems.Select(c => new {
                productId = c.ProductId,
                productName = c.Product.Name,
                productImage = c.Product.Images.FirstOrDefault()?.ImageUrl ?? "/images/no-image.png",
                price = c.Product.Price,
                size = c.Size,
                quantity = c.Quantity
            });

            return Json(new { items = jsonItems, total = total });
        }

        //Xóa sản phẩm (AJAX)
        [HttpPost]
        public IActionResult Remove(int id, string size)
        {
            var userId = GetUserId();
            var item = _context.CartItems.FirstOrDefault(c => c.ProductId == id && c.Size == size && c.UserId == userId);

            if (item != null)
            {
                _context.CartItems.Remove(item);
                _context.SaveChanges();
            }

            return Json(new { success = true });
        }
    }
}