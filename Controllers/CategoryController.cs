using ECommerceClothing.Data;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceClothing.Areas.Admin.Controllers // Ní nhớ check namespace coi có đúng Area không nhé
{
    [Area("Admin")] // Thêm cái này để nó biết nó thuộc vùng Admin
    public class CategoryController : Controller
    {
        private readonly AppDbContext _context;

        public CategoryController(AppDbContext context)
        {
            _context = context;
        }

        // --- HÀM CREATE (XỬ LÝ LỖI POPUP) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Categories.Add(category);
                _context.SaveChanges();
            }

            // QUAN TRỌNG: Thay vì return View() (gây lỗi trang trắng), 
            // ta bắt nó quay về trang danh sách Categories của AdminController
            return RedirectToAction("Categories", "Admin", new { area = "Admin" });
        }

        // --- HÀM EDIT (XỬ LÝ LỖI POPUP TƯƠNG TỰ) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Categories.Update(category);
                _context.SaveChanges();
            }

            // Lưu xong hoặc lỗi cũng quay về danh sách hết
            return RedirectToAction("Categories", "Admin", new { area = "Admin" });
        }

       
        // Hàm xóa này nhận ID từ URL, xóa xong quay về danh sách
        public IActionResult Delete(int id)
        {
            // --- BƯỚC 1: KIỂM TRA RÀNG BUỘC (Hỏi trước khi làm) ---
            // Kiểm tra xem có ProductType nào đang dùng Category này không?
            bool isUsedByTypes = _context.ProductTypes.Any(pt => pt.CategoryId == id);

            // (Tùy chọn) Kiểm tra xem có Sản phẩm nào đang dùng không?
            bool isUsedByProducts = _context.Products.Any(p => p.CategoryId == id);

            if (isUsedByTypes || isUsedByProducts)
            {
                // --- NẾU ĐANG DÙNG: KHÔNG XÓA ---
                // Gửi thông báo lỗi sang View (để thằng SweetAlert nó hiện lên)
                TempData["ErrorMessage"] = "This category cannot be deleted because dependent data exists.";

                // Quay về trang danh sách ngay lập tức
                return RedirectToAction("Categories", "Admin", new { area = "Admin" });
            }

            // --- BƯỚC 2: NẾU KHÔNG AI DÙNG -> MỚI ĐƯỢC XÓA ---
            var category = _context.Categories.Find(id);
            if (category != null)
            {
                _context.Categories.Remove(category);
                _context.SaveChanges();

                // Gửi thông báo thành công
                TempData["SuccessMessage"] = "Category deleted successfully!";
            }

            return RedirectToAction("Categories", "Admin", new { area = "Admin" });
        }
    }
}