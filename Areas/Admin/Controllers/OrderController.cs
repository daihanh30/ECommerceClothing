using ECommerceClothing.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceClothing.Areas.Admin.Controllers
{
    [Area("Admin")]
    // [Authorize(Roles = "Admin")] // Bỏ comment dòng này nếu ní đã làm phân quyền Role
    public class OrderController : Controller
    {
        private readonly AppDbContext _context;

        public OrderController(AppDbContext context)
        {
            _context = context;
        }

        // Hiện danh sách tất cả đơn hàng
        public async Task<IActionResult> Index(string status = "")
        {
            var query = _context.Orders.AsQueryable();

            // Lọc theo trạng thái nếu Admin muốn
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.Status == status);
            }

            // Sắp xếp đơn mới nhất lên đầu
            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();

            ViewBag.CurrentStatus = status;
            return View(orders);
        }

        // 1. Hàm hiển thị trang Chi tiết đơn hàng
        public async Task<IActionResult> Details(int id)
        {
            // Tìm đơn hàng kèm theo chi tiết sản phẩm bên trong
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                    .ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // 2. Hàm xử lý Cập nhật trạng thái khi Admin bấm nút
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string newStatus)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            // Cập nhật trạng thái mới
            order.Status = newStatus;
            await _context.SaveChangesAsync();

            // Dùng TempData để báo thành công ra màn hình
            TempData["SuccessMessage"] = $"Order #{id} has been updated to: {newStatus}";

            return RedirectToAction(nameof(Details), new { id = order.Id });
        }
    }
}