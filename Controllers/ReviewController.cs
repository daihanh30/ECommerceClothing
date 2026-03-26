using ECommerceClothing.Data;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting; // Thêm cái này để xử lý lưu file ảnh
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // ✅ LỖI NẰM Ở ĐÂY NÈ: Bắt buộc phải có dòng này mới dùng được FirstOrDefaultAsync
using System.Security.Claims;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ECommerceClothing.Controllers
{
    [Authorize] // Bắt buộc phải đăng nhập mới được đánh giá
    public class ReviewController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ReviewController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpPost]
        // Lưu ý: Nếu file Model của ní tên là "Review" (số ít) thì sửa chữ "Reviews" bên dưới thành "Review" nhé.
        public async Task<IActionResult> SubmitFeedback(Reviews model)
        {
            // 1. Lấy ID của User đang đăng nhập
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            model.UserId = userId;
            model.CreatedAt = DateTime.Now;

            // 2. Kiểm tra xem khách có up ảnh không? Nếu có thì lưu vào thư mục wwwroot/images/reviews
            if (model.ImageFile != null)
            {
                // Tạo thư mục nếu chưa có
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "reviews");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Tạo tên file độc nhất (tránh trùng tên)
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ImageFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Copy file vào thư mục
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(fileStream);
                }

                // Gán đường dẫn vào DB
                model.ImageUrl = "/images/reviews/" + uniqueFileName;
            }

            // 3. Lưu toàn bộ dữ liệu (Sao, Comment, Link Ảnh) vào Database
            _context.Reviews.Add(model);
            await _context.SaveChangesAsync();

            // 4. Báo thành công (Đã đổi sang Tiếng Anh) và đá khách hàng về lại trang Lịch sử đơn hàng
            TempData["SuccessMessage"] = "Thank you for your feedback!";

            return RedirectToAction("MyOrders", "Profile");
        }

        [HttpGet]
        public async Task<IActionResult> GetReviewDetails(int orderId)
        {
            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.OrderId == orderId);
            if (review == null) return Json(new { success = false });

            return Json(new
            {
                success = true,
                rating = review.Rating,
                comment = review.Comment,
                imageUrl = review.ImageUrl,
                date = review.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            });
        }
    }
}