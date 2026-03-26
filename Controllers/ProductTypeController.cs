using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ECommerceClothing.Data;
using ECommerceClothing.Models;

namespace ECommerceClothing.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ProductTypesController : Controller
    {
        private readonly AppDbContext _context;

        public ProductTypesController(AppDbContext context)
        {
            _context = context;
        }

        // 1. Hiển thị danh sách Product Type kèm tên Category cha
        public async Task<IActionResult> Index()
        {
            // Lấy danh sách ProductTypes kèm theo tên Category cha
            var appDbContext = _context.ProductTypes.Include(p => p.Category);

            // QUAN TRỌNG: Gửi danh sách Category sang View để nạp vào Dropdown trong Modal
            ViewData["CategoryId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Categories, "Id", "Name");

            return View("~/Areas/Admin/Views/Admin/ProductType.cshtml", await appDbContext.ToListAsync());
        }

        // 2. Mở Popup tạo mới
        public IActionResult Create()
        {
            // Load danh sách Category để chọn (Tops, Bottoms...)
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            return View();
        }

        // 3. Lưu vào Database
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductType productType)
        {
            if (ModelState.IsValid)
            {
                _context.Add(productType);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index)); // Quay về danh sách
            }
            // Nếu lỗi thì load lại dropdown
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", productType.CategoryId);
            return View(productType);
        }

        // GET: Admin/ProductTypes/Delete/5
        public IActionResult Delete(int id)
        {
            // --- 1. KIỂM TRA RÀNG BUỘC ---
            // Hỏi: Có sản phẩm nào đang thuộc loại này không?
            // (Lưu ý: dùng ProductTypeId vì mình đã đổi tên trong Model)
            bool isUsed = _context.Products.Any(p => p.ProductTypeId == id);

            if (isUsed)
            {
                // Nếu có: Báo lỗi và đuổi về
                TempData["ErrorMessage"] = "This category cannot be deleted because dependent data exists.";
                return RedirectToAction("Index");
            }

            // --- 2. XÓA NẾU KHÔNG CÓ RÀNG BUỘC ---
            var productType = _context.ProductTypes.Find(id);
            if (productType != null)
            {
                _context.ProductTypes.Remove(productType);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Category deleted successfully!";
            }

            return RedirectToAction("Index");
        }
        // ... (Ní có thể thêm Edit/Delete tương tự CategoryController)
    }
}