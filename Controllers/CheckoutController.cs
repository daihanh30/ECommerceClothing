using ECommerceClothing.Data;
using ECommerceClothing.Helpers;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;


namespace ECommerceClothing.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly AppDbContext _context;

        string endpoint = "https://test-payment.momo.vn/v2/gateway/api/create";
        string partnerCode = "MOMOBKUN20180529";
        string accessKey = "klm05TvNBzhg7h7j";
        string secretKey = "at67qH6mk8w5Y1nAyMoYKMWACiEi2bsa";
        string returnUrl = "https://localhost:7098/Checkout/PaymentCallback";
        string notifyUrl = "https://momo.vn";

        public CheckoutController(AppDbContext context) { _context = context; }

        // Checout 
        [Route("Checkout")]
        [HttpGet]
        public async Task<IActionResult> Index(int? buyNowId, string? size, int qty = 1, bool checkoutAll = false)
        {
            //Lấy dữ liệu an toàn từ URL
            var rawItems = HttpContext.Request.Query["selectedItems"].ToList();
            var selectedItems = new List<string>();
            foreach (var raw in rawItems)
            {
                if (!string.IsNullOrEmpty(raw)) selectedItems.AddRange(raw.Split(',').Select(x => x.Trim()));
            }

            var checkoutCart = new List<CartItem>();

            //Buy Now 
            if (buyNowId.HasValue && !string.IsNullOrEmpty(size))
            {
                checkoutCart.Add(new CartItem { ProductId = buyNowId.Value, Size = size, Quantity = qty });
            }
            // tick sản phẩm từ giỏ
            else if (selectedItems != null && selectedItems.Any())
            {
                foreach (var itemStr in selectedItems)
                {
                    var parts = itemStr.Split('|');

                    if (parts.Length >= 2 && int.TryParse(parts[0], out int pId))
                    {
                        var s = parts[1].Trim().ToUpper();
                        int q = 1;

                        // Lấy Quantity trực tiếp từ form gửi lên 
                        if (parts.Length >= 3) int.TryParse(parts[2], out q);

                        checkoutCart.Add(new CartItem { ProductId = pId, Size = s, Quantity = q });
                    }
                }
            }
            else if (checkoutAll)
            {
                // 1. Lấy UserId của người dùng đang đăng nhập
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (!string.IsNullOrEmpty(currentUserId))
                {
                    // 2. Truy xuất giỏ hàng từ Database
                    var dbCartItems = await _context.CartItems
                                                    .Where(c => c.UserId == currentUserId)
                                                    .ToListAsync();

                    if (dbCartItems != null && dbCartItems.Any())
                    {
                        foreach (var item in dbCartItems)
                        {
                            checkoutCart.Add(new CartItem
                            {
                                ProductId = item.ProductId,
                                Size = item.Size,
                                Quantity = item.Quantity
                            });
                        }
                    }
                }
            }
            else
            {
                var savedCheckout = HttpContext.Session.Get<List<CartItem>>("CheckoutItems");
                if (savedCheckout != null && savedCheckout.Any()) checkoutCart = savedCheckout;
            }

            if (checkoutCart.Count == 0)
            {
                TempData["ErrorMessage"] = "Please select at least one product to proceed with checkout.";
                return RedirectToAction("Index", "Cart");
            }

            var cartItemViewModels = new List<CartItemViewModel>();
            decimal totalAmount = 0;

            //Truy xuất DB để lấy giá và kiểm tra tồn kho 
            foreach (var item in checkoutCart)
            {
                var product = await _context.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == item.ProductId);
                if (product == null) continue;

                var pSize = await _context.ProductSizes.FirstOrDefaultAsync(ps => ps.ProductId == item.ProductId && ps.SizeName == item.Size);

                if (pSize == null && !string.IsNullOrEmpty(item.Size)) return BadRequest("Size không tồn tại");

                if (pSize != null)
                {
                    if (pSize.Quantity <= 0) return BadRequest($"{product.Name} (Size {item.Size}) is out of stock.");
                    if (pSize.Quantity < item.Quantity) return BadRequest($"Only {pSize.Quantity} products remain for {product.Name} (Size: {item.Size})");
                }

                cartItemViewModels.Add(new CartItemViewModel
                {
                    ProductName = product.Name,
                    Price = product.Price,
                    Quantity = item.Quantity,
                    Size = item.Size,
                    ProductImage = product.Images?.FirstOrDefault()?.ImageUrl ?? "/images/no-image.png"
                });

                totalAmount += product.Price * item.Quantity;
            }

            HttpContext.Session.Set("CheckoutItems", checkoutCart);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Truy vấn thông tin User từ DB
            var currentUser = await _context.Users.FindAsync(userId);
            var checkoutModel = new CheckoutViewModel
            {
                CartItems = cartItemViewModels,
                FullName = currentUser?.FullName,
                PhoneNumber = currentUser?.PhoneNumber,
                TotalAmount = totalAmount
            };
            //load lại tọa độ map 
            ViewBag.ShopLocation = await _context.ShopSettings.FirstOrDefaultAsync();

            return View(checkoutModel);
        }

        // XỬ LÝ ĐẶT HÀNG use Transaction
        [HttpPost]
        public async Task<IActionResult> PlaceOrder(CheckoutViewModel model)
        {
            var checkoutCart = HttpContext.Session.Get<List<CartItem>>("CheckoutItems") ?? new List<CartItem>();
            if (checkoutCart.Count == 0) return RedirectToAction("Index", "Home");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Tính toán tiền và kiểm tra Voucher (Đoạn này chỉ là tính toán trên bộ nhớ tạm)
            decimal realTotalAmount = 0;
            foreach (var item in checkoutCart)
            {
                var p = await _context.Products.FindAsync(item.ProductId);
                if (p != null) realTotalAmount += (p.Price * 1000) * item.Quantity;
            }

            decimal serverCalculatedDiscount = 0;
            int? appliedVoucherId = null;

            if (!string.IsNullOrEmpty(model.VoucherCode))
            {
                var v = await _context.Vouchers.FirstOrDefaultAsync(x => x.Code == model.VoucherCode.ToUpper());
                var now = DateTime.Now;

                if (v != null && v.IsActive && now >= v.StartDate && now <= v.EndDate && v.UsedCount < v.Quantity)
                {
                    decimal minOrderReal = v.MinOrder;
                    if (realTotalAmount >= minOrderReal)
                    {
                        var userUsedCount = await _context.Orders.CountAsync(o => o.UserId == userId && o.VoucherId == v.Id && !o.Status.Contains("Cancelled"));
                        if (userUsedCount < v.UsageLimitPerUser)
                        {
                            if (v.Type == "Percent")
                            {
                                serverCalculatedDiscount = realTotalAmount * v.Value;
                                decimal maxReduceReal = v.MaxReduce;
                                if (v.MaxReduce > 0 && serverCalculatedDiscount > maxReduceReal) serverCalculatedDiscount = maxReduceReal;
                            }
                            else
                            {
                                serverCalculatedDiscount = v.Value;
                            }
                            v.UsedCount += 1;
                            appliedVoucherId = v.Id;
                        }
                        else { return BadRequest("You have reached the usage limit for this discount code."); }
                    }
                    else { return BadRequest("Order minimum total not met."); }
                }
                else { return BadRequest("Discount code is invalid or expired."); }
            }

            if (serverCalculatedDiscount > realTotalAmount) serverCalculatedDiscount = realTotalAmount;
            decimal finalTotal = realTotalAmount + model.ShippingFee - serverCalculatedDiscount;
            if (finalTotal < 0) finalTotal = 0;

            //TRANSACTION
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                //Lưu đơn hàng chính trc ở lấy Id để lưu chi tiết sau
                var order = new Order
                {
                    UserId = userId,
                    ShippingFee = model.ShippingFee,
                    TotalAmount = finalTotal,
                    DiscountAmount = serverCalculatedDiscount,
                    VoucherId = appliedVoucherId,
                    Address = model.Address,
                    FullName = model.FullName,
                    PhoneNumber = model.PhoneNumber,
                    PaymentMethod = model.PaymentMethod,
                    OrderDate = DateTime.Now,
                    Status = model.PaymentMethod == "MoMo" ? "Unpaid" : "Pending",
                    Note = model.Note ?? ""
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // lưu chi tiết đơn hàng và trừ kho, nếu có lỗi sẽ dễ dàng rollback hơn
                foreach (var item in checkoutCart)
                {
                    var p = await _context.Products.FindAsync(item.ProductId);

                    _context.OrderDetail.Add(new OrderDetail
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Size = item.Size,
                        Price = p?.Price ?? 0
                    });

                    // Trừ kho theo Size
                    var pSize = await _context.ProductSizes
                        .FirstOrDefaultAsync(ps => ps.ProductId == item.ProductId && ps.SizeName == item.Size);

                    if (pSize != null)
                    {
                        // Kiểm tra kho lần cuối ngay trước khi lưu, đề phòng có người vừa mua chớp nhoáng
                        if (pSize.Quantity < item.Quantity)
                        {
                            throw new Exception($"Product {p?.Name} (Size: {item.Size}) is out of stock.!");
                        }
                        pSize.Quantity = Math.Max(0, pSize.Quantity - item.Quantity);
                    }
                }
                // Lưu chi tiết và số lượng kho mới
                await _context.SaveChangesAsync();

                // Xóa sp đã đặt ở giỏ hàng sau khi đặt thành công 
                foreach (var item in checkoutCart)
                {
                    var cartItemDb = await _context.CartItems
                        .FirstOrDefaultAsync(c => c.UserId == userId
                                               && c.ProductId == item.ProductId
                                               && c.Size == item.Size);

                    if (cartItemDb != null)
                    {
                        _context.CartItems.Remove(cartItemDb);
                    }
                }
                await _context.SaveChangesAsync();
                HttpContext.Session.Remove("CheckoutItems");

                // nếu mọi thứ chạy được thì đến đây mới lưu vào db
                await transaction.CommitAsync();
                if (model.PaymentMethod == "MoMo")
                {
                    return await ProcessMoMoPayment(order, finalTotal);
                }

                return RedirectToAction("Success", new { id = order.Id });
            }
            catch (Exception ex)
            {
                // nếu có lỗi sẽ k lưu vào db
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] = "An error occurred during the ordering process: " + ex.Message;
                return RedirectToAction("Index", "Cart");
            }
        }

        //api kiểm tra voucher
        [HttpGet]
        // [Authorize] // Bạn có thể thêm attribute này nếu trên đầu Controller chưa có
        public async Task<IActionResult> GetAvailableVouchers()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Bắt lỗi ngay từ đầu nếu mất session/chưa đăng nhập
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại." });
            }

            var now = DateTime.Now;

            // 1. Lấy danh sách voucher hợp lệ
            var vouchers = await _context.Vouchers
                .Where(v => v.IsPublic == true
                         && v.IsActive == true
                         && v.StartDate <= now
                         && v.EndDate >= now
                         && v.UsedCount < v.Quantity)
                .ToListAsync();

            // Tối ưu: Nếu không có voucher nào khả dụng thì trả về luôn, đỡ phải query bảng Orders
            if (!vouchers.Any())
            {
                return Json(new { success = true, data = new List<object>() });
            }

            var voucherIds = vouchers.Select(v => v.Id).ToList();

            // 2. Query thẳng số lần dùng của User hiện tại (không cần if bọc ngoài nữa)
            var userUsages = await _context.Orders
                .Where(o => o.UserId == userId
                         && o.VoucherId != null
                         && voucherIds.Contains(o.VoucherId.Value)
                         && !o.Status.Contains("Cancelled"))
                .GroupBy(o => o.VoucherId.Value)
                .Select(g => new { VoucherId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.VoucherId, x => x.Count);

            // 3. Chuẩn bị dữ liệu trả về cho Frontend
            var resultData = vouchers.Select(v => {
                int currentUserUsedCount = userUsages.ContainsKey(v.Id) ? userUsages[v.Id] : 0;
                bool isLimitReached = currentUserUsedCount >= v.UsageLimitPerUser;

                return new
                {
                    id = v.Id,
                    code = v.Code,
                    title = v.Title ?? "Voucher",
                    desc = v.Description ?? "No description",
                    date = v.EndDate.ToString("dd/MM/yyyy HH:mm"),
                    min_order = v.MinOrder,
                    max_reduce = v.MaxReduce,
                    type = v.Type == "Percent" ? "percent" : "fixed",
                    value = v.Value,
                    isLimitReached = isLimitReached
                };
            }).ToList();

            return Json(new { success = true, data = resultData });
        }
        

        [HttpPost]
        public async Task<IActionResult> ApplyVoucher(string code, decimal orderTotal)
        {
            if (string.IsNullOrEmpty(code))
                return Json(new { success = false, message = "Please enter the discount code." });

            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == code.ToUpper());
            if (voucher == null || !voucher.IsActive)
                return Json(new { success = false, message = "The discount code does not exist or has been locked." });

            var now = DateTime.Now;
            if (now < voucher.StartDate)
                return Json(new { success = false, message = $"This code is only valid from {voucher.StartDate:dd/MM/yyyy HH:mm}." });
            if (now > voucher.EndDate)
                return Json(new { success = false, message = "This discount code has expired." });
            if (voucher.UsedCount >= voucher.Quantity)
                return Json(new { success = false, message = "The code usage limit has been reached." });

            if (orderTotal < voucher.MinOrder)
                return Json(new { success = false, message = $"Minimum order to apply this code is {voucher.MinOrder:#,##0}." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userUsedCount = await _context.Orders.CountAsync(o => o.UserId == userId && o.VoucherId == voucher.Id && !o.Status.Contains("Cancelled"));
            if (userUsedCount >= voucher.UsageLimitPerUser)
                return Json(new { success = false, message = "You have used up all your uses of this discount code." });

            decimal discount = voucher.Type == "Percent" ? (orderTotal * voucher.Value) : voucher.Value;
            if (voucher.Type == "Percent" && voucher.MaxReduce > 0 && discount > voucher.MaxReduce) discount = voucher.MaxReduce;
            if (discount > orderTotal) discount = orderTotal;

            return Json(new { success = true, discount = discount, message = "Code applied successfully!" });
        }

        //Momo API

        //Hàm tạo Request gửi sang MoMo để lấy link QR Code
        private async Task<IActionResult> ProcessMoMoPayment(Order order, decimal finalTotal)
        {
            string orderInfo = "Pay for your NIXONE order #" + order.Id;
            string amount = Math.Round(finalTotal).ToString();

            string orderId = order.Id.ToString() + "_" + DateTime.Now.Ticks.ToString();
            string requestId = Guid.NewGuid().ToString();
            string extraData = "";

            // Xây dựng chuỗi dữ liệu gốc để băm bảo mật
            string rawHash = "accessKey=" + accessKey +
                             "&amount=" + amount +
                             "&extraData=" + extraData +
                             "&ipnUrl=" + notifyUrl +
                             "&orderId=" + orderId +
                             "&orderInfo=" + orderInfo +
                             "&partnerCode=" + partnerCode +
                             "&redirectUrl=" + returnUrl +
                             "&requestId=" + requestId +
                             "&requestType=captureWallet";

            string signature = ComputeHmacSha256(rawHash, secretKey);

            var requestData = new
            {
                partnerCode = partnerCode,
                partnerName = "NIXONE",
                storeId = "MomoTestStore",
                requestId = requestId,
                amount = amount,
                orderId = orderId,
                orderInfo = orderInfo,
                redirectUrl = returnUrl,
                ipnUrl = notifyUrl,
                lang = "en",
                extraData = extraData,
                requestType = "captureWallet",
                signature = signature
            };

            // Bắn HTTP Request sang MoMo
            using HttpClient client = new HttpClient();
            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(endpoint, content);
            var responseString = await response.Content.ReadAsStringAsync();

            // Đọc JSON MoMo trả về để lấy link payUrl
            var jsonResponse = JsonDocument.Parse(responseString);
            if (jsonResponse.RootElement.TryGetProperty("payUrl", out var payUrlElement))
            {
                string payUrl = payUrlElement.GetString();
                return Redirect(payUrl); 
            }

            TempData["ErrorMessage"] = "MoMo payment gateway connection error. Please try again.";
            return RedirectToAction("Index", "Cart");
        }

        // quay lại sau khi thanh toán xong, momo trả về đây
        [HttpGet]
        public async Task<IActionResult> PaymentCallback()
        {
            // MoMo sẽ nhét các kết quả thanh toán vào URL (QueryString)
            var collection = HttpContext.Request.Query;

            string orderIdStr = collection["orderId"]; 
            string resultCode = collection["resultCode"];

            if (string.IsNullOrEmpty(orderIdStr)) return RedirectToAction("Index", "Home");

            // Cắt chuỗi để lấy lại cái ID đơn hàng thực tế  
            int realOrderId = int.Parse(orderIdStr.Split('_')[0]);
            var order = await _context.Orders.FindAsync(realOrderId);

            if (order == null) return NotFound();

            if (resultCode == "0")
            {
                order.Status = "Paid";
                await _context.SaveChangesAsync();

                return RedirectToAction("Success", new { id = realOrderId });
            }
            else
            {
                order.Status = "Cancelled";

                var orderDetails = await _context.OrderDetail.Where(od => od.OrderId == realOrderId).ToListAsync();
                foreach (var item in orderDetails)
                {
                    var pSize = await _context.ProductSizes.FirstOrDefaultAsync(ps => ps.ProductId == item.ProductId && ps.SizeName == item.Size);
                    if (pSize != null) pSize.Quantity += item.Quantity; 
                }

                await _context.SaveChangesAsync();

                TempData["ErrorMessage"] = "MoMo payment failed or the transaction was canceled by you.";
                return RedirectToAction("Index", "Cart");
            }
        }

        //PAY NOW
        [HttpGet]
        public async Task<IActionResult> RetryPayment(int orderId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);

                if (order == null)
                {
                    TempData["ErrorMessage"] = "Order not found or access denied!";
                    return RedirectToAction("Index", "Home");
                }

                if (order.Status.ToUpper() != "UNPAID")
                {
                    TempData["ErrorMessage"] = "This order has already been paid or cancelled.";
                    return RedirectToAction("Index", "Profile");
                }

                return await ProcessMoMoPayment(order, order.TotalAmount);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred: " + ex.Message;
                return RedirectToAction("Index", "Profile"); 
            }
        }

        // 3. Hàm thuật toán mã hóa chữ ký điện tử 
        private string ComputeHmacSha256(string message, string secretKey)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var messageBytes = Encoding.UTF8.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyBytes))
            {
                var hashMessage = hmacsha256.ComputeHash(messageBytes);
                return BitConverter.ToString(hashMessage).Replace("-", "").ToLower();
            }
        }
            
        public IActionResult Success(int id)
        {
            ViewBag.OrderId = id;
            return View();
        }
    }
}