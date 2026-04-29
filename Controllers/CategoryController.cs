using ECommerceClothing.Data;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceClothing.Areas.Admin.Controllers 
{
    [Area("Admin")] 
    public class CategoryController : Controller
    {
        private readonly AppDbContext _context;

        public CategoryController(AppDbContext context)
        {
            _context = context;
        }

        // add category
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Categories.Add(category);
                _context.SaveChanges(); 

                TempData["SuccessMessage"] = "Category created successfully!";

                return RedirectToAction("Categories", "Admin", new { area = "Admin" });
            }

            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Categories.Update(category);
                _context.SaveChanges();
            }

            return RedirectToAction("Categories", "Admin", new { area = "Admin" });
        }

        public IActionResult Delete(int id)
        {
            bool isUsedByTypes = _context.ProductTypes.Any(pt => pt.CategoryId == id);

            bool isUsedByProducts = _context.Products.Any(p => p.ProductTypeObj.CategoryId == id);

            if (isUsedByTypes || isUsedByProducts)
            {
                TempData["ErrorMessage"] = "This category cannot be deleted because dependent data exists.";

                return RedirectToAction("Categories", "Admin", new { area = "Admin" });
            }

            var category = _context.Categories.Find(id);
            if (category != null)
            {
                _context.Categories.Remove(category);
                _context.SaveChanges();

                TempData["SuccessMessage"] = "Category deleted successfully!";
            }

            return RedirectToAction("Categories", "Admin", new { area = "Admin" });
        }
    }
}