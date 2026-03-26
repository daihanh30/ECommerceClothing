//using Microsoft.AspNetCore.Mvc;
//using ECommerceClothing.Models;
//using ECommerceClothing.Helpers; // [QUAN TRỌNG] Fix lỗi Session
//using ECommerceClothing.Data;    // [QUAN TRỌNG] Fix lỗi ApplicationDbContext

//namespace ECommerceClothing.Controllers
//{
//    public class CartController : Controller
//    {
//        private readonly AppDbContext _context;

//        public CartController(AppDbContext context)
//        {
//            _context = context;
//        }

//        // 1. Trang danh sách giỏ hàng
//        public IActionResult Index()
//        {
//            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
//            return View(cart);
//        }

//        // 2. Thêm vào giỏ
//        [HttpPost]
//        public IActionResult AddToCart(int Id, string Size, int Quantity)
//        {
//            var product = _context.Products.Find(Id);
//            if (product == null) return NotFound();

//            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();

//            var existingItem = cart.FirstOrDefault(x => x.ProductId == Id && x.Size == Size);

//            if (existingItem != null)
//            {
//                existingItem.Quantity += Quantity;
//            }
//            else
//            {
//                // Lấy ảnh đầu tiên
//                var img = _context.ProductImages.FirstOrDefault(x => x.ProductId == Id)?.ImageUrl ?? "/images/no-image.png";

//                cart.Add(new CartItem
//                {
//                    ProductId = product.Id,
//                    ProductName = product.Name,
//                    Price = product.Price,
//                    Size = Size,
//                    Quantity = Quantity,
//                    ProductImage = img
//                });
//            }

//            HttpContext.Session.Set("Cart", cart);

//            // Quay lại trang cũ
//            return Redirect(Request.Headers["Referer"].ToString());
//        }

//        // 3. Cập nhật số lượng (Cho AJAX)
//        [HttpPost]
//        public IActionResult UpdateQuantity(int id, string size, int change)
//        {
//            var cart = HttpContext.Session.Get<List<CartItem>>("Cart");
//            if (cart != null)
//            {
//                var item = cart.FirstOrDefault(x => x.ProductId == id && x.Size == size);
//                if (item != null)
//                {
//                    item.Quantity += change;
//                    if (item.Quantity <= 0)
//                    {
//                        cart.Remove(item);
//                    }
//                }
//                HttpContext.Session.Set("Cart", cart);
//            }
//            return Ok();
//        }

//        // 4. Thêm vào giỏ bằng AJAX (Dùng cho Popup)
//        [HttpPost]
//        public IActionResult AddToCartAjax(int Id, string Size, int Quantity)
//        {
//            var product = _context.Products.Find(Id);
//            if (product == null) return Json(new { success = false, msg = "Sản phẩm không tồn tại" });

//            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
//            var existingItem = cart.FirstOrDefault(x => x.ProductId == Id && x.Size == Size);

//            if (existingItem != null)
//            {
//                existingItem.Quantity += Quantity;
//            }
//            else
//            {
//                var img = _context.ProductImages.FirstOrDefault(x => x.ProductId == Id)?.ImageUrl ?? "/images/no-image.png";
//                cart.Add(new CartItem
//                {
//                    ProductId = product.Id,
//                    ProductName = product.Name,
//                    Price = product.Price,
//                    Size = Size,
//                    Quantity = Quantity,
//                    ProductImage = img
//                });
//            }

//            HttpContext.Session.Set("Cart", cart);
//            return Json(new { success = true });
//        }

//        // 5. Lấy dữ liệu giỏ hàng để vẽ lên Sidebar
//        [HttpGet]
//        public IActionResult GetCartJson()
//        {
//            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
//            var total = cart.Sum(x => x.TotalPrice);
//            return Json(new { items = cart, total = total });
//        }

//        // 6. Xóa sản phẩm khỏi Sidebar (AJAX)
//        [HttpPost]
//        public IActionResult RemoveCartItemAjax(int id, string size)
//        {
//            var cart = HttpContext.Session.Get<List<CartItem>>("Cart");
//            if (cart != null)
//            {
//                var item = cart.FirstOrDefault(x => x.ProductId == id && x.Size == size);
//                if (item != null) cart.Remove(item);
//                HttpContext.Session.Set("Cart", cart);
//            }
//            return Json(new { success = true });
//        }


//    }
//}

using Microsoft.AspNetCore.Mvc;
using ECommerceClothing.Models;
using ECommerceClothing.Helpers;
using ECommerceClothing.Data;

namespace ECommerceClothing.Controllers
{
    public class CartController : Controller
    {
        private readonly AppDbContext _context;

        public CartController(AppDbContext context)
        {
            _context = context;
        }

        // 1. Trang danh sách giỏ hàng
        public IActionResult Index()
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();

            // 👉 ĐỌC MẬT THƯ: Nếu có dữ liệu tick từ Buy Again truyền qua, quăng nó vào ViewBag
            if (TempData["PreTickedItems"] != null)
            {
                ViewBag.PreTickedItems = TempData["PreTickedItems"].ToString().Split(',').ToList();
            }

            return View(cart);
        }

        // 2. Thêm vào giỏ (Form Submit thường)
        [HttpPost]
        public IActionResult AddToCart(int Id, string Size, int Quantity)
        {
            var product = _context.Products.Find(Id);
            if (product == null) return NotFound();

            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();

            var existingItem = cart.FirstOrDefault(x => x.ProductId == Id && x.Size == Size);

            if (existingItem != null)
            {
                existingItem.Quantity += Quantity;
            }
            else
            {
                var img = _context.ProductImages.FirstOrDefault(x => x.ProductId == Id)?.ImageUrl ?? "/images/no-image.png";

                cart.Add(new CartItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Price = product.Price,
                    Size = Size,
                    Quantity = Quantity,
                    ProductImage = img
                });
            }

            HttpContext.Session.Set("Cart", cart);
            return Redirect(Request.Headers["Referer"].ToString());
        }

        // 3. Cập nhật số lượng (Cho AJAX) --> ĐÃ SỬA RETURN JSON
        [HttpPost]
        public IActionResult UpdateQuantity(int id, string size, int change)
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart");
            if (cart != null)
            {
                var item = cart.FirstOrDefault(x => x.ProductId == id && x.Size == size);
                if (item != null)
                {
                    item.Quantity += change;

                    // Nếu giảm <= 0 thì xóa luôn
                    if (item.Quantity <= 0)
                    {
                        cart.Remove(item);
                    }
                }
                HttpContext.Session.Set("Cart", cart);

                // [QUAN TRỌNG] Phải trả về JSON success để JS không bị lỗi
                return Json(new { success = true });
            }
            return Json(new { success = false, msg = "Giỏ hàng trống" });
        }

        // 4. Thêm vào giỏ bằng AJAX (Dùng cho Popup Quick View)
        [HttpPost]
        public IActionResult AddToCartAjax(int Id, string Size, int Quantity)
        {
            if (!User.Identity!.IsAuthenticated)
            {
                return Json(new { success = false, notLoggedIn = true });
            }
            var product = _context.Products.Find(Id);
            if (product == null) return Json(new { success = false, msg = "Sản phẩm không tồn tại" });

            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            var existingItem = cart.FirstOrDefault(x => x.ProductId == Id && x.Size == Size);

            if (existingItem != null)
            {
                existingItem.Quantity += Quantity;
            }
            else
            {
                var img = _context.ProductImages.FirstOrDefault(x => x.ProductId == Id)?.ImageUrl ?? "/images/no-image.png";
                cart.Add(new CartItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Price = product.Price,
                    Size = Size,
                    Quantity = Quantity,
                    ProductImage = img
                });
            }

            HttpContext.Session.Set("Cart", cart);
            return Json(new { success = true });
        }

        // 5. Lấy dữ liệu giỏ hàng để vẽ lên Sidebar
        [HttpGet]
        public IActionResult GetCartJson()
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            // Tính tổng tiền
            var total = cart.Sum(x => x.Quantity * x.Price);
            return Json(new { items = cart, total = total });
        }

        // 6. Xóa sản phẩm (AJAX) --> ĐÃ ĐỔI TÊN HÀM CHO KHỚP JS
        [HttpPost]
        public IActionResult Remove(int id, string size) // Tên cũ: RemoveCartItemAjax -> Đổi thành Remove
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart");
            if (cart != null)
            {
                var item = cart.FirstOrDefault(x => x.ProductId == id && x.Size == size);
                if (item != null) cart.Remove(item);

                HttpContext.Session.Set("Cart", cart);
            }
            return Json(new { success = true });
        }
    }
}