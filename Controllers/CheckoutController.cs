using ECommerceClothing.Data;
using ECommerceClothing.Helpers;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ECommerceClothing.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly AppDbContext _context;

        // Cấu hình MoMo (Giữ nguyên của ní)
        string endpoint = "https://test-payment.momo.vn/v2/gateway/api/create";
        string partnerCode = "MOMOBKUN20180529";
        string accessKey = "klm05TvNBzhg7h7j";
        string secretKey = "at67qH6mk8w5Y1nAyMoYKMWACiEi2bsa";
        string returnUrl = "https://localhost:7098/Checkout/PaymentCallback";
        string notifyUrl = "https://momo.vn";

        public CheckoutController(AppDbContext context) { _context = context; }

        // Thêm tham số bool checkoutAll = false
        [Route("Checkout")]
        public async Task<IActionResult> Index(List<string>? selectedItems, int? buyNowId, string size, int qty = 1, bool checkoutAll = false)
        {
            var checkoutCart = new List<CartItem>();
            var fullCart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();

            // TRƯỜNG HỢP 1: MUA NGAY (Bypass giỏ hàng)
            if (buyNowId.HasValue && !string.IsNullOrEmpty(size))
            {
                var product = await _context.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == buyNowId);
                if (product != null)
                {
                    checkoutCart.Add(new CartItem { ProductId = product.Id, ProductName = product.Name, Price = product.Price, Size = size, Quantity = qty, ProductImage = product.Images?.FirstOrDefault()?.ImageUrl ?? "/images/no-image.png" });
                }
            }
            // TRƯỜNG HỢP 2: TỪ TRANG CART CHÍNH CÓ TICK CHỌN
            else if (selectedItems != null && selectedItems.Any())
            {
                foreach (var itemStr in selectedItems)
                {
                    var parts = itemStr.Split('|');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int pId))
                    {
                        var s = parts[1];
                        var item = fullCart.FirstOrDefault(c => c.ProductId == pId && c.Size == s);
                        if (item != null) checkoutCart.Add(item);
                    }
                }
            }
            // 👉 TRƯỜNG HỢP 3: BẤM TỪ MINI-CART (Lấy tất cả sản phẩm)
            else if (checkoutAll)
            {
                if (fullCart.Count == 0) return RedirectToAction("Index", "Home");
                checkoutCart = fullCart; // Bốc hết 4 món vào
            }
            // TRƯỜNG HỢP 4: MUA LẠI HOẶC LOAD LẠI TRANG
            else
            {
                var savedCheckout = HttpContext.Session.Get<List<CartItem>>("CheckoutItems");
                if (savedCheckout != null && savedCheckout.Any())
                {
                    checkoutCart = savedCheckout;
                }
                else
                {
                    if (fullCart.Count == 0) return RedirectToAction("Index", "Home");
                    checkoutCart = fullCart;
                }
            }

            if (checkoutCart.Count == 0) return RedirectToAction("Index", "Cart");

            // Lưu lại để lúc bấm PlaceOrder nó bốc đúng đồ đi lưu DB
            HttpContext.Session.Set("CheckoutItems", checkoutCart);

            var checkoutModel = new CheckoutViewModel { CartItems = new List<CartItemViewModel>() };
            foreach (var item in checkoutCart)
            {
                checkoutModel.CartItems.Add(new CartItemViewModel { ProductName = item.ProductName, Price = item.Price, Quantity = item.Quantity, Size = item.Size, ProductImage = item.ProductImage });
            }
            checkoutModel.TotalAmount = checkoutModel.CartItems.Sum(x => x.Price * x.Quantity);
            return View(checkoutModel);
        }

        [HttpPost]
        public async Task<IActionResult> PlaceOrder(CheckoutViewModel model)
        {
            //ModelState.Clear();
            // Lấy danh sách thực tế đã chốt ở bước Index
            var checkoutCart = HttpContext.Session.Get<List<CartItem>>("CheckoutItems") ?? new List<CartItem>();
            if (checkoutCart.Count == 0) return RedirectToAction("Index", "Home");

            // Tính toán lại tổng tiền dựa trên danh sách chốt
            decimal realTotalAmount = 0;
            foreach (var item in checkoutCart)
            {
                realTotalAmount += (item.Price * 1000) * item.Quantity;
            }

            decimal finalTotal = realTotalAmount + model.ShippingFee - model.DiscountAmount;
            if (finalTotal < 0) finalTotal = 0;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var order = new Order
            {
                UserId = userId,
                ShippingFee = model.ShippingFee,
                TotalAmount = finalTotal,
                DiscountAmount = model.DiscountAmount,
                VoucherCode = model.VoucherCode,
                Address = model.Address,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                PaymentMethod = model.PaymentMethod,
                OrderDate = DateTime.Now,

                // 👉 ĐÃ SỬA: Nếu là MoMo thì để Unpaid, nếu là COD thì để Pending
                Status = model.PaymentMethod == "MoMo" ? "Unpaid" : "Pending",

                Note = model.Note ?? ""
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // 2. LƯU CHI TIẾT ĐƠN HÀNG & TRỪ KHO
            foreach (var item in checkoutCart)
            {
                var detail = new OrderDetail
                {
                    OrderId = order.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Size = item.Size,
                    Price = item.Price
                };
                _context.OrderDetail.Add(detail);

                // Trừ kho theo Size
                var pSize = await _context.ProductSizes
                    .FirstOrDefaultAsync(ps => ps.ProductId == item.ProductId && ps.SizeName == item.Size);
                if (pSize != null)
                {
                    pSize.Quantity -= item.Quantity;
                    if (pSize.Quantity < 0) pSize.Quantity = 0;
                }

                // Cập nhật tổng kho
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null) product.Stock -= item.Quantity;
            }

            // 3. Cập nhật Voucher
            if (!string.IsNullOrEmpty(model.VoucherCode))
            {
                var appliedVoucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == model.VoucherCode.ToUpper());
                if (appliedVoucher != null)
                {
                    appliedVoucher.UsedCount += 1;
                    _context.Vouchers.Update(appliedVoucher);
                }
            }

            await _context.SaveChangesAsync();

            // 4. DỌN DẸP GIỎ HÀNG
            var mainCart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            // Chỉ xóa những món vừa thanh toán xong ra khỏi giỏ hàng chính
            foreach (var item in checkoutCart)
            {
                var itemInMain = mainCart.FirstOrDefault(x => x.ProductId == item.ProductId && x.Size == item.Size);
                if (itemInMain != null) mainCart.Remove(itemInMain);
            }
            HttpContext.Session.Set("Cart", mainCart);
            HttpContext.Session.Remove("CheckoutItems"); // Xóa danh sách tạm

            if (model.PaymentMethod == "MoMo")
            {
                return RedirectToAction("PaymentMock", new { orderId = order.Id, amount = finalTotal });
            }

            return RedirectToAction("Success", new { id = order.Id });
        }

        // --- CÁC HÀM CÒN LẠI (PaymentMock, ConfirmPayment, Success, Vouchers...) GIỮ NGUYÊN ---
        public IActionResult PaymentMock(int orderId, decimal amount)
        {
            ViewBag.OrderId = orderId;
            ViewBag.Amount = amount;
            return View();
        }

        public async Task<IActionResult> ConfirmPayment(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.Status = "Paid";
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Success", new { id = orderId });
        }

        public IActionResult Success(int id)
        {
            ViewBag.OrderId = id;
            return View();
        }

        private string ComputeHmacSha256(string message, string secretKey)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var messageBytes = Encoding.UTF8.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyBytes))
            {
                var hashmessage = hmacsha256.ComputeHash(messageBytes);
                return BitConverter.ToString(hashmessage).Replace("-", "").ToLower();
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableVouchers()
        {
            var now = DateTime.Now;
            var vouchers = await _context.Vouchers
                .Where(v => v.IsActive && v.IsPublic && v.StartDate <= now && v.EndDate >= now && v.UsedCount < v.Quantity)
                .Select(v => new {
                    id = v.Id.ToString(),
                    code = v.Code,
                    type = v.Type.ToLower(),
                    value = v.Value,
                    max_reduce = v.MaxReduce,
                    min_order = v.MinOrder,
                    title = v.Title,
                    desc = v.Description,
                    date = v.EndDate.ToString("dd/MM/yyyy")
                })
                .ToListAsync();

            return Json(new { success = true, data = vouchers });
        }

        [HttpGet]
        public async Task<IActionResult> CheckManualVoucher(string code)
        {
            if (string.IsNullOrEmpty(code))
                return Json(new { success = false, msg = "Please enter a promo code!" });

            var now = DateTime.Now;
            var v = await _context.Vouchers.FirstOrDefaultAsync(x => x.Code == code.ToUpper() && x.IsActive);

            if (v == null) return Json(new { success = false, msg = "Invalid or inactive promo code." });
            if (v.StartDate > now || v.EndDate < now) return Json(new { success = false, msg = "This code is expired." });
            if (v.UsedCount >= v.Quantity) return Json(new { success = false, msg = "This code is fully redeemed." });

            return Json(new
            {
                success = true,
                data = new
                {
                    id = v.Id.ToString(),
                    code = v.Code,
                    type = v.Type.ToLower(),
                    value = v.Value,
                    max_reduce = v.MaxReduce,
                    min_order = v.MinOrder,
                    title = v.Title,
                    desc = v.Description,
                    date = v.EndDate.ToString("dd/MM/yyyy")
                }
            });
        }
    }
}