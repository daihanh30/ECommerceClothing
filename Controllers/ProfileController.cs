using ECommerceClothing.Data;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; // Bắt buộc để lấy UserId
using ECommerceClothing.Helpers;

namespace ECommerceClothing.Controllers
{
    public class ProfileController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly AppDbContext _context;

        // ✅ CẬP NHẬT: Gộp Constructor lại cho chuẩn, không bị lỗi nhân đôi
        public ProfileController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            AppDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateInfo(string fullName, string phoneNumber, string address)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            user.FullName = fullName;
            user.PhoneNumber = phoneNumber;
            user.Address = address;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "Profile updated successfully!";
            }
            else
            {
                TempData["Error"] = result.Errors.FirstOrDefault()?.Description;
            }

            return RedirectToAction("Index");
        }

        // ================= ORDER DETAIL =================
        public async Task<IActionResult> OrderDetails(int id)
        {
            var userId = _userManager.GetUserId(User);

            // 1. Lấy dữ liệu thô từ DB
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                        .ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null) return NotFound();

            // 2. Đổ dữ liệu vào OrderDetailVM (ViewModel mới tạo)
            var viewModel = new OrderDetailVM
            {
                OrderInfo = order,
                Items = order.OrderDetails.Select(od => new OrderItemInfo
                {
                    ProductName = od.Product.Name,
                    ProductImage = od.Product.Images.FirstOrDefault()?.ImageUrl ?? "/images/no-image.png",
                    Size = od.Size,
                    Quantity = od.Quantity,
                    Price = od.Price
                }).ToList()
            };

            return View("OrderDetails", viewModel);
        }

        // ================= MY ORDERS =================
        public async Task<IActionResult> MyOrders(string status = "")
        {
            var userId = _userManager.GetUserId(User);

            var query = _context.Orders
                .Where(o => o.UserId == userId)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                        .ThenInclude(p => p.Images)
                .OrderByDescending(o => o.OrderDate)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.Status == status);
            }

            var orders = await query.ToListAsync();

            // Lấy danh sách ID đơn đã được người dùng này đánh giá
            var reviewedOrders = await _context.Reviews
                .Where(r => r.UserId == userId)
                .Select(r => r.OrderId)
                .ToListAsync();

            ViewBag.ReviewedOrders = reviewedOrders;
            ViewBag.CurrentStatus = status;

            return View(orders);
        }

        [HttpPost]
        public async Task<IActionResult> CancelOrder(int orderId, string cancelReason)
        {
            try
            {
                // Lấy ID người dùng đang đăng nhập (giống bên Checkout)
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Tìm đơn hàng kèm theo chi tiết đơn hàng
                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

                if (order == null)
                {
                    return Json(new { success = false, msg = "Order not found!" });
                }

                // Chỉ cho phép hủy nếu đơn đang Pending hoặc Unpaid
                if (order.Status != "Pending" && order.Status != "Unpaid")
                {
                    return Json(new { success = false, msg = "You can only cancel Pending or Unpaid orders." });
                }

                // 1. Đổi trạng thái
                order.Status = "Cancelled";

                // 2. Lưu lý do hủy (Nhét tạm vào cột Note để khỏi phải sửa Database)
                string reasonText = $"[Cancelled Reason: {cancelReason}]";
                order.Note = string.IsNullOrEmpty(order.Note) ? reasonText : $"{order.Note} \n {reasonText}";

                // 3. CỘNG TRẢ LẠI KHO (Cực kỳ quan trọng)
                foreach (var detail in order.OrderDetails)
                {
                    // Trả lại kho size
                    var pSize = await _context.ProductSizes
                        .FirstOrDefaultAsync(ps => ps.ProductId == detail.ProductId && ps.SizeName == detail.Size);
                    if (pSize != null)
                    {
                        pSize.Quantity += detail.Quantity;
                    }

                    // Trả lại kho tổng
                    var product = await _context.Products.FindAsync(detail.ProductId);
                    if (product != null)
                    {
                        product.Stock += detail.Quantity;
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Bắt lỗi và quăng ra Front-end để biết C# đang bị gì
                return Json(new { success = false, msg = "Server error: " + ex.Message });
            }
        }

        // ================= MUA LẠI ĐƠN HÀNG (BUY AGAIN) =================
        [HttpPost]
        public async Task<IActionResult> BuyAgain(int orderId)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                            .ThenInclude(p => p.Images)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

                if (order == null) return Json(new { success = false, msg = "Order not found!" });

                var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
                var preTickedItems = new List<string>(); // Danh sách các món cần tick sẵn

                foreach (var detail in order.OrderDetails)
                {
                    var existingItem = cart.FirstOrDefault(x => x.ProductId == detail.ProductId && x.Size == detail.Size);

                    if (existingItem != null)
                    {
                        existingItem.Quantity += detail.Quantity; // Đã có trong giỏ thì cộng dồn số lượng
                    }
                    else
                    {
                        var img = detail.Product.Images?.FirstOrDefault()?.ImageUrl ?? "/images/no-image.png";
                        cart.Add(new CartItem
                        {
                            ProductId = detail.ProductId,
                            ProductName = detail.Product.Name,
                            Price = detail.Price,
                            Size = detail.Size,
                            Quantity = detail.Quantity,
                            ProductImage = img
                        });
                    }
                    // Ghi nhớ định danh của món này để lát nữa tick
                    preTickedItems.Add($"{detail.ProductId}|{detail.Size}");
                }

                HttpContext.Session.Set("Cart", cart);

                // 👉 Lưu mật thư vào TempData (Nó sẽ sống sót qua 1 lần chuyển trang)
                TempData["PreTickedItems"] = string.Join(",", preTickedItems);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message });
            }
        }
        [HttpPost]
        public async Task<IActionResult> ChangePasswordJson(string currentPassword, string newPassword, string confirmNewPassword)
        {
            // Kiểm tra xem user có đang đăng nhập không
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, msg = "Your login session has expired!" });
            }

            // Xài hàm chuẩn của Identity để đổi mật khẩu (tự động check pass cũ và mã hóa pass mới)
            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (result.Succeeded)
            {
                // F5 lại phiên đăng nhập để hệ thống nhận pass mới ngay
                await _signInManager.RefreshSignInAsync(user);
                return Json(new { success = true, msg = "Password changed successfully!" });
            }

            // Nếu pass cũ gõ sai hoặc pass mới không đủ độ khó, nó sẽ báo lỗi tại đây
            string errorMsg = string.Join(", ", result.Errors.Select(e => e.Description));
            return Json(new { success = false, msg = errorMsg });
        }
    }
}