using ECommerceClothing.Data;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

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
         
        public async Task<IActionResult> Index()
        { 
            var appDbContext = _context.ProductTypes.Include(p => p.Category);
             
            ViewData["CategoryId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Categories, "Id", "Name");

            return View("~/Areas/Admin/Views/Admin/ProductType.cshtml", await appDbContext.ToListAsync());
        }

        //Mở Popup tạo mới
        public IActionResult Create()
        { 
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            return View();
        }

        //add producttype
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductType productType)
        {
            if (ModelState.IsValid)
            {
                _context.Add(productType);
                await _context.SaveChangesAsync();
                 
                TempData["SuccessMessage"] = "The product type has been successfully created!";

                return RedirectToAction(nameof(Index));
            }
             
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", productType.CategoryId);
            return View(productType);
        }

        //delete producttype
        public IActionResult Delete(int id)
        { 
            bool isUsed = _context.Products.Any(p => p.ProductTypeId == id);

            if (isUsed)
            { 
                TempData["ErrorMessage"] = "This category cannot be deleted because dependent data exists.";
                return RedirectToAction("Index");
            }
             
            var productType = _context.ProductTypes.Find(id);
            if (productType != null)
            {
                _context.ProductTypes.Remove(productType);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Category deleted successfully!";
            }

            return RedirectToAction("Index");
        } 
    }
}