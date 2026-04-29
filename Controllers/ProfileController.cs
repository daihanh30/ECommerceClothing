using ECommerceClothing.Data;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;  
using ECommerceClothing.Helpers;

namespace ECommerceClothing.Controllers
{
    public class ProfileController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly AppDbContext _context;
         
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

        // ORDER DETAIL  
        public async Task<IActionResult> OrderDetails(int id)
        {
            var userId = _userManager.GetUserId(User);

            //Lấy dữ liệu thô từ DB
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                        .ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null) return NotFound();

            //Đổ dữ liệu vào OrderDetailVM để View dễ xài hơn
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

        // MY ORDERS 
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
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                 
                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

                if (order == null)
                {
                    return Json(new { success = false, msg = "Order not found!" });
                }
                 
                if (order.Status != "Pending" && order.Status != "Unpaid")
                {
                    return Json(new { success = false, msg = "You can only cancel Pending or Unpaid orders." });
                }
                 
                order.Status = "Cancelled";
                 
                string reasonText = $"[Cancelled Reason: {cancelReason}]";
                order.Note = string.IsNullOrEmpty(order.Note) ? reasonText : $"{order.Note} \n {reasonText}";

                // cancel thì mới trả lại kho
                foreach (var detail in order.OrderDetails)
                { 
                    var pSize = await _context.ProductSizes
                        .FirstOrDefaultAsync(ps => ps.ProductId == detail.ProductId && ps.SizeName == detail.Size);
                    if (pSize != null)
                    {
                        pSize.Quantity += detail.Quantity;
                    }  
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            { 
                return Json(new { success = false, msg = "Server error: " + ex.Message });
            }
        }

        //Buy agian
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
                var preTickedItems = new List<string>(); 

                foreach (var detail in order.OrderDetails)
                {
                    var existingItem = cart.FirstOrDefault(x => x.ProductId == detail.ProductId && x.Size == detail.Size);

                    if (existingItem != null)
                    { 
                        existingItem.Quantity += detail.Quantity;
                    }
                    else
                    {  
                        cart.Add(new CartItem
                        {
                            ProductId = detail.ProductId,
                            Size = detail.Size,
                            Quantity = detail.Quantity
                        });
                    }
                    preTickedItems.Add($"{detail.ProductId}|{detail.Size}");
                }

                HttpContext.Session.Set("Cart", cart);

                TempData["PreTickedItems"] = string.Join(",", preTickedItems);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = ex.Message });
            }
        }
         
        //Change password
        [HttpPost]
        public async Task<IActionResult> ChangePasswordJson(string currentPassword, string newPassword, string confirmNewPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, msg = "Your login session has expired!" });
            }

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                return Json(new { success = true, msg = "Password changed successfully!" });
            }

            string errorMsg = string.Join(", ", result.Errors.Select(e => e.Description));
            return Json(new { success = false, msg = errorMsg });
        }
    }
}