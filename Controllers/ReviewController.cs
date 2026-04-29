using ECommerceClothing.Data;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;  
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;  
using System.Security.Claims;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ECommerceClothing.Controllers
{
    [Authorize]  
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
        public async Task<IActionResult> SubmitFeedback(Reviews model)
        {
            //Lấy ID của User đang đăng nhập
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            model.UserId = userId;
            model.CreatedAt = DateTime.Now;

            //Kiểm tra xem khách có up ảnh hay k
            if (model.ImageFile != null)
            {
                // Tạo thư mục nếu chưa có
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "reviews");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                } 
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ImageFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                 
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(fileStream);
                }
                 
                model.ImageUrl = "/images/reviews/" + uniqueFileName;
            }

            //Lưu toàn bộ dữ liệu vào Database
            _context.Reviews.Add(model);
            await _context.SaveChangesAsync();
             
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