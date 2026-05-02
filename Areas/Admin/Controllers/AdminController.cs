using ECommerceClothing.Data;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Microsoft.AspNetCore.Http;
using ClosedXML.Excel;
using System.ComponentModel.DataAnnotations;

namespace ECommerceClothing.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        // DASHBOARD 
        public async Task<IActionResult> Dashboard()
        {
            var data = new ECommerceClothing.Models.Dashboard();

            // Tính các con số tổng quát
            data.TotalProducts = await _context.Products.CountAsync();
            data.TotalOrders = await _context.Orders.CountAsync();
            data.TotalCustomers = await _context.Users.Where(u => u.Email != "admin@gmail.com").CountAsync();

            // tổng doanh thu
            data.TotalRevenue = await _context.Orders
                .Where(o => !o.Status.Contains("Cancelled"))
                .SumAsync(o => o.TotalAmount);

            // get 5 order gần nhất
            data.RecentOrders = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync();

            // biểu đồ tròn
            int delivered = await _context.Orders.CountAsync(o => o.Status.Contains("Delivered"));
            int cancelled = await _context.Orders.CountAsync(o => o.Status.Contains("Cancelled"));
            data.OrderStatusCounts = new List<int> { delivered, cancelled };

            // biểu đồ đường
            var sevenDaysAgo = DateTime.Now.Date.AddDays(-6);
            var ordersLast7Days = await _context.Orders
                .Where(o => !o.Status.Contains("Cancelled") && o.OrderDate >= sevenDaysAgo)
                .ToListAsync();

            var labels = new List<string>();
            var revenueData = new List<decimal>();

            for (int i = 6; i >= 0; i--)
            {
                var targetDate = DateTime.Now.Date.AddDays(-i);
                labels.Add(targetDate.ToString("dd/MM"));

                // Tính tổng tiền ngày hôm đó
                var dailyTotal = ordersLast7Days
                    .Where(o => o.OrderDate.Date == targetDate.Date)
                    .Sum(o => o.TotalAmount);

                revenueData.Add(dailyTotal);
            }

            data.ChartLabels = labels;
            data.ChartData = revenueData;

            return View(data);
        }

        //PRODUCTS
        public IActionResult Products(int? categoryId)
        {
            ViewBag.Categories = _context.Categories.ToList();

            var productsQuery = _context.Products
                .Include(p => p.ProductTypeObj)
                    .ThenInclude(pt => pt.Category)
                .Include(p => p.Images)
                .Include(p => p.ProductSizes)
                .AsQueryable();

            if (categoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.ProductTypeObj.CategoryId == categoryId);
            }
            var productList = productsQuery.ToList();
            ViewBag.TotalProducts = productList.Count;
            ViewBag.OutOfStock = productList.Count(p => p.ProductSizes.Sum(ps => ps.Quantity) <= 0);
            return View("Products", productsQuery.ToList());
        }

        //CREATE PRODUCT
        [HttpGet]
        public IActionResult CreateProduct()
        {
            LoadCategories(); 
            return View("CreateProduct");
        }

        private void LoadCategories(int? categoryId = null)
        {
            ViewBag.Categories = _context.Categories.ToList();

            // Nếu có truyền categoryId thì chỉ load Type của category đó, ngược lại trả về list rỗng
            if (categoryId.HasValue && categoryId > 0)
            {
                ViewBag.ProductTypes = _context.ProductTypes.Where(t => t.CategoryId == categoryId.Value).ToList();
            }
            else
            {
                ViewBag.ProductTypes = new List<ProductType>();
            }
        }

        // API lấy danh sách ProductType theo Category (Ajax Dropdown)
        [HttpGet]
        public IActionResult GetProductTypesByCategory(int categoryId)
        {
            // k gửi về type nếu là access
            var category = _context.Categories.Find(categoryId);
            if (category != null && category.Name.ToLower().Contains("accessories"))
            {
                return Json(new List<object>()); 
            }

            var types = _context.ProductTypes
                                .Where(p => p.CategoryId == categoryId)
                                .Select(p => new {
                                    id = p.ProductTypeId,
                                    name = p.Name
                                })
                                .ToList();
            return Json(types);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(Product model, List<IFormFile> images, List<string> SelectedSizes, List<int> SelectedQuantities, int SelectedCategoryId)
        // thêm tham số SelectedCategoryId: Vì model.Product không còn CategoryId, phải nhận riêng từ form
        {
            ModelState.Remove("Images");
            ModelState.Remove("ProductTypeObj");
            ModelState.Remove("ProductSizes");
            var categorySelected = await _context.Categories.FindAsync(SelectedCategoryId);
            bool isAccessories = categorySelected != null && categorySelected.Name.ToLower().Contains("accessories");

            if (isAccessories)
            {
                ModelState.Remove("ProductTypeId");

                // Tự động tìm xem đã có type cho Accessories chưa
                var hiddenType = await _context.ProductTypes.FirstOrDefaultAsync(pt => pt.CategoryId == SelectedCategoryId);

                // Nếu Db chưa có, tự động tạo mới 
                if (hiddenType == null)
                {
                    hiddenType = new ProductType { CategoryId = SelectedCategoryId, Name = "Accessories (Default)" };
                    _context.ProductTypes.Add(hiddenType);
                    await _context.SaveChangesAsync();
                }

                //Gán ID hợp lệ này cho sản phẩm
                model.ProductTypeId = hiddenType.ProductTypeId;
            }
            else
            {
                bool hasProductTypes = await _context.ProductTypes.AnyAsync(pt => pt.CategoryId == SelectedCategoryId);
                if (!hasProductTypes)
                {
                    ModelState.Remove("ProductTypeId");
                }
            }

            // Kiểm tra ảnh
            if (images == null || images.Count == 0)
            {
                ModelState.AddModelError("Images", "Please select at least one product image.");
            }
            else
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                foreach (var file in images)
                {
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
                    {
                        ModelState.AddModelError(string.Empty, $"The file '{file.FileName}' is invalid. Only JPG and PNG are allowed.");
                    }
                }
            }

            // Kiểm tra tồn kho 
            int checkTotalStock = 0;
            if (SelectedSizes != null && SelectedSizes.Any() && SelectedQuantities != null)
            {
                checkTotalStock = SelectedQuantities.Sum();

                if (SelectedQuantities.Any(q => q < 0))
                {
                    ModelState.AddModelError(string.Empty, "The quantity of each size must not be less than 0.");
                }
            }

            if (checkTotalStock <= 0)
            {
                ModelState.AddModelError(string.Empty, "Total inventory quantity must be at least 1. You must enter a quantity for sizes to create this product.");
            }

            if (string.IsNullOrWhiteSpace(model.Description))
            {
                ModelState.AddModelError("Description", "Please enter a product description (it must not contain only spaces).");
            }

            if (!ModelState.IsValid)
            {
                LoadCategories(SelectedCategoryId);
                ViewBag.SelectedCategoryId = SelectedCategoryId;
                return View("CreateProduct", model);
            }

            model.CreatedAt = DateTime.Now;

            _context.Products.Add(model);
            await _context.SaveChangesAsync(); 

            //Lưu chi tiết size và stock vào bảng ProductSize
            if (SelectedSizes != null && SelectedQuantities != null && SelectedSizes.Count == SelectedQuantities.Count)
            {
                for (int i = 0; i < SelectedSizes.Count; i++)
                {
                    var pSize = new ProductSize
                    {
                        ProductId = model.Id,
                        SizeName = SelectedSizes[i],
                        Quantity = SelectedQuantities[i]
                    };
                    _context.ProductSizes.Add(pSize);
                }
                await _context.SaveChangesAsync();
            }

            //Lưu hình ảnh
            if (images != null && images.Count > 0)
            {
                await UploadProductImages(model.Id, images);
            }
            TempData["SuccessMessage"] = "Product created successfully!";

            return RedirectToAction("Products", "Admin", new { area = "Admin" });
        }
        // EDIT PRODUCT

        //dùng để mở giao diện edit, load dữ liệu cũ lên form   
        [HttpGet]
        public async Task<IActionResult> EditProduct(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.ProductSizes)
                .Include(p => p.ProductTypeObj)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            ViewBag.Categories = _context.Categories.ToList();

            // Lấy CategoryId ra một biến riêng để View dùng
            int currentCategoryId = product.ProductTypeObj?.CategoryId ?? 0;
            ViewBag.SelectedCategoryId = currentCategoryId;

            ViewBag.ProductTypes = _context.ProductTypes
                .Where(t => t.CategoryId == currentCategoryId)
                .ToList();

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(Product model, List<IFormFile> images, List<int> deletedImageIds, List<string> SelectedSizes, List<int> SelectedQuantities, int SelectedCategoryId)
        {
            ModelState.Remove("Images");
            ModelState.Remove("ProductTypeObj");
            ModelState.Remove("ProductSizes");

            if (string.IsNullOrWhiteSpace(model.Description))
            {
                ModelState.AddModelError("Description", "Please enter a product description.");
            }

            int checkTotalStock = 0;
            if (SelectedSizes != null && SelectedSizes.Any() && SelectedQuantities != null)
            {
                checkTotalStock = SelectedQuantities.Sum();
            }

            if (checkTotalStock <= 0)
            {
                ModelState.AddModelError(string.Empty, "Total inventory quantity must be at least 1.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingProduct = await _context.Products
                        .Include(p => p.Images)
                        .Include(p => p.ProductSizes)
                        .FirstOrDefaultAsync(p => p.Id == model.Id);

                    if (existingProduct == null) return NotFound();

                    existingProduct.Name = model.Name;
                    existingProduct.Price = model.Price;
                    existingProduct.ProductTypeId = model.ProductTypeId;
                    existingProduct.Description = model.Description;

                    if (existingProduct.ProductSizes != null && existingProduct.ProductSizes.Any())
                    {
                        _context.ProductSizes.RemoveRange(existingProduct.ProductSizes);
                    }

                    if (SelectedSizes != null && SelectedQuantities != null && SelectedSizes.Count == SelectedQuantities.Count)
                    {
                        for (int i = 0; i < SelectedSizes.Count; i++)
                        {
                            _context.ProductSizes.Add(new ProductSize
                            {
                                ProductId = model.Id,
                                SizeName = SelectedSizes[i],
                                Quantity = SelectedQuantities[i]
                            });
                        }
                    }

                    // Xóa ảnh cũ
                    if (deletedImageIds != null && deletedImageIds.Any())
                    {
                        var imagesToDelete = _context.ProductImages.Where(img => deletedImageIds.Contains(img.Id)).ToList();
                        foreach (var img in imagesToDelete)
                        {
                            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", img.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                        }
                        _context.ProductImages.RemoveRange(imagesToDelete);
                    }

                    // Thêm ảnh mới
                    if (images != null && images.Count > 0)
                    {
                        await UploadProductImages(model.Id, images);
                    }

                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Product update successful!";
                    return RedirectToAction("Products", "Admin", new { area = "Admin" });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error: " + ex.Message);
                }
            }
            // Xử lý khi Model bị lỗi 
            if (SelectedSizes != null && SelectedQuantities != null && SelectedSizes.Count == SelectedQuantities.Count)
            {
                model.ProductSizes = new List<ProductSize>();
                for (int i = 0; i < SelectedSizes.Count; i++)
                {
                    model.ProductSizes.Add(new ProductSize
                    {
                        SizeName = SelectedSizes[i],
                        Quantity = SelectedQuantities[i]
                    });
                }
            }

            ViewBag.Categories = _context.Categories.ToList();
            ViewBag.SelectedCategoryId = SelectedCategoryId;
            ViewBag.ProductTypes = _context.ProductTypes
                .Where(t => t.CategoryId == SelectedCategoryId)
                .ToList();
            model.Images = _context.ProductImages.Where(p => p.ProductId == model.Id).ToList();

            return View("EditProduct", model);
        }

        //  DELETE PRODUCT 
        public IActionResult Delete(int id)
        {
            var product = _context.Products.Find(id);

            if (product != null)
            {
                _context.Products.Remove(product);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Product deleted successfully!";
            }

            return RedirectToAction("Products", "Admin");
        }

        //CATEGORIES
        public IActionResult Categories()
        {
            var categories = _context.Categories.ToList();
            return View("Categories", categories);
        }

        [HttpGet]
        public IActionResult CreateCategory() => View("CreateCategory");

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateCategory(Category category)
        {
            if (!ModelState.IsValid) return View("CreateCategory", category);

            _context.Categories.Add(category);
            _context.SaveChanges();
            return RedirectToAction("Categories");
        }

        //HÀM PHỤ UPLOAD ẢNH
        private async Task UploadProductImages(int productId, List<IFormFile> images)
        {
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            foreach (var img in images)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(img.FileName);
                var path = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                {
                    await img.CopyToAsync(stream);
                }

                _context.ProductImages.Add(new ProductImage
                {
                    ProductId = productId,
                    ImageUrl = "/images/products/" + fileName
                });
            }
            await _context.SaveChangesAsync();
        }

        //VOUCHER
        public async Task<IActionResult> Vouchers(string type)
        {
            // Lấy toàn bộ danh sách Voucher dưới dạng Queryable để lọc
            var vouchers = _context.Vouchers.AsQueryable();

            // Logic lọc theo type
            if (type == "public")
            {
                vouchers = vouchers.Where(v => v.IsPublic == true);
            }
            else if (type == "hidden")
            {
                vouchers = vouchers.Where(v => v.IsPublic == false);
            }

            // Trả về View danh sách đã lọc
            return View(await vouchers.ToListAsync());
        }

        // create voucher    
        public IActionResult CreateVoucher()
        {
            return View();
        }

        //create voucher
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateVoucher(Voucher model)
        {
            if (ModelState.IsValid)
            {
                model.Code = model.Code.Trim().ToUpper();

                bool isCodeExist = await _context.Vouchers.AnyAsync(v => v.Code == model.Code);
                if (isCodeExist)
                {
                    ModelState.AddModelError("Code", "This promo code already exists!");
                    return View(model);
                }

                if (model.EndDate <= model.StartDate)
                {
                    ModelState.AddModelError("EndDate", "End Date must be greater than Start Date.");
                    return View(model);
                }

                _context.Vouchers.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Voucher created successfully!";
                return RedirectToAction(nameof(Vouchers));
            }
            return View(model);
        }

        // edit voucher
        public async Task<IActionResult> EditVoucher(int? id)
        {
            if (id == null) return NotFound();

            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher == null) return NotFound();

            return View(voucher);
        }

        // edit voucher
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditVoucher(int id, Voucher model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                model.Code = model.Code.Trim().ToUpper();

                bool isCodeExist = await _context.Vouchers.AnyAsync(v => v.Code == model.Code && v.Id != model.Id);
                if (isCodeExist)
                {
                    ModelState.AddModelError("Code", "This promo code already exists!");
                    return View(model);
                }

                if (model.EndDate <= model.StartDate)
                {
                    ModelState.AddModelError("EndDate", "End Date must be greater than Start Date.");
                    return View(model);
                }

                try
                {
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Voucher updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Vouchers.AnyAsync(e => e.Id == model.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Vouchers));
            }
            return View(model);
        }

        // delete voucher
        [HttpPost]
        public async Task<IActionResult> DeleteVoucher(int id)
        {
            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher == null) return Json(new { success = false, message = "Voucher not found!" });

            if (voucher.UsedCount > 0)
            {
                return Json(new { success = false, message = "Cannot delete this voucher because it has already been used in orders. Please disable it instead!" });
            }

            _context.Vouchers.Remove(voucher);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Voucher deleted successfully!" });
        }

        
        public async Task<IActionResult> Customers(string search, string filter)
        {
            var query = _context.Users
            .Where(u => u.Email != "admin@gmail.com") 
            .AsQueryable();

            // Search
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(u =>
                    (u.FullName != null && u.FullName.ToLower().Contains(search)) ||
                    (u.Email != null && u.Email.ToLower().Contains(search))
                );
            }

            var customers = await query.ToListAsync();

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentFilter = filter;

            return View(customers);
        }

        public async Task<IActionResult> CustomerDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            // Hệ thống giờ sẽ tự động tìm trong bảng AspNetUsers
            var user = await _context.Users.FindAsync(id);

            if (user == null) return NotFound();

            var orders = await _context.Orders
                .Where(o => o.UserId == id) 
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            int totalOrders = orders.Count;
            int cancelledOrders = orders.Count(o => o.Status == "Cancelled");
            decimal totalSpent = orders.Where(o => o.Status == "Completed" || o.Status == "Delivered").Sum(o => o.TotalAmount);
            double cancelRate = totalOrders == 0 ? 0 : Math.Round(((double)cancelledOrders / totalOrders) * 100, 1);

            var viewModel = new ECommerceClothing.ViewModels.CustomerDetail
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                TotalOrders = totalOrders,
                TotalSpent = totalSpent,
                CancelRate = cancelRate,
                OrderHistory = orders
            };

            return View(viewModel);
        }

        public async Task<IActionResult> ExportDashboardReport(string range = "all")
        {
            var sevenDaysAgo = DateTime.Now.Date.AddDays(-6);
            var recentOrders = await _context.Orders
                .Where(o => o.OrderDate >= sevenDaysAgo)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            int deliveredCount = recentOrders.Count(o => o.Status.Contains("Delivered"));
            int cancelledCount = recentOrders.Count(o => o.Status.Contains("Cancelled"));
            var totalRevenue = recentOrders.Where(o => !o.Status.Contains("Cancelled")).Sum(o => o.TotalAmount);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Revenue_Report");

                var titleCell = worksheet.Cell(1, 1);
                titleCell.Value = "E-COMMERCE REVENUE REPORT";
                titleCell.Style.Font.Bold = true;
                titleCell.Style.Font.FontSize = 16;
                titleCell.Style.Font.FontColor = XLColor.DarkBlue;
                worksheet.Range("A1:F1").Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                worksheet.Cell(2, 1).Value = $"Period: {sevenDaysAgo:dd/MM/yyyy} to {DateTime.Now:dd/MM/yyyy}";
                worksheet.Range("A2:F2").Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Range("A2:F2").Style.Font.Italic = true;

                worksheet.Cell(4, 1).Value = "TOTAL REVENUE:";
                worksheet.Cell(4, 2).Value = totalRevenue;
                worksheet.Cell(4, 2).Style.NumberFormat.Format = "#,##0\" VND\"";
                worksheet.Cell(4, 1).Style.Font.Bold = true;

                worksheet.Cell(5, 1).Value = "Delivered / Cancelled:";
                worksheet.Cell(5, 2).Value = $"{deliveredCount} orders / {cancelledCount} orders";
                worksheet.Cell(5, 1).Style.Font.Bold = true;

                int startRow = 7;
                worksheet.Cell(startRow, 1).Value = "ORDER ID";
                worksheet.Cell(startRow, 2).Value = "CUSTOMER";
                worksheet.Cell(startRow, 3).Value = "PHONE";
                worksheet.Cell(startRow, 4).Value = "ORDER DATE";
                worksheet.Cell(startRow, 5).Value = "TOTAL AMOUNT";
                worksheet.Cell(startRow, 6).Value = "STATUS";

                var headerRange = worksheet.Range(startRow, 1, startRow, 6);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Fill.BackgroundColor = XLColor.MidnightBlue;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                int currentRow = startRow + 1;
                foreach (var order in recentOrders)
                {
                    worksheet.Cell(currentRow, 1).Value = "#" + order.Id.ToString("D5");
                    worksheet.Cell(currentRow, 2).Value = order.FullName;

                    worksheet.Cell(currentRow, 3).Value = "'" + order.PhoneNumber;

                    worksheet.Cell(currentRow, 4).Value = order.OrderDate.ToString("dd/MM/yyyy HH:mm");
                    worksheet.Cell(currentRow, 5).Value = order.TotalAmount;
                    worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0";

                    worksheet.Cell(currentRow, 6).Value = order.Status;
                    if (order.Status.Contains("Delivered"))
                        worksheet.Cell(currentRow, 6).Style.Font.FontColor = XLColor.Green;
                    if (order.Status.Contains("Cancelled"))
                        worksheet.Cell(currentRow, 6).Style.Font.FontColor = XLColor.Red;

                    worksheet.Range(currentRow, 1, currentRow, 6).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                    worksheet.Range(currentRow, 1, currentRow, 6).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

                    if (currentRow % 2 == 0)
                        worksheet.Range(currentRow, 1, currentRow, 6).Style.Fill.BackgroundColor = XLColor.AliceBlue;

                    currentRow++;
                }

                headerRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                headerRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

                worksheet.Cell(currentRow, 4).Value = "GRAND TOTAL:";
                worksheet.Cell(currentRow, 4).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                worksheet.Cell(currentRow, 5).FormulaA1 = $"SUM(E{startRow + 1}:E{currentRow - 1})";
                worksheet.Cell(currentRow, 5).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0";

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    string fileName = $"Revenue_Report_{DateTime.Now:dd_MM_yyyy}.xlsx";
                    return File(content, contentType, fileName);
                }
            }
        }

        //SHOP LOCATION
        [HttpGet]
        public async Task<IActionResult> ManageShop()
        {
            // Tìm cấu hình trong DB, nếu chưa có thì tạo một object mặc định để điền sẵn vào Form
            var setting = await _context.ShopSettings.FirstOrDefaultAsync() ?? new ShopSetting
            {
                ShopName = "NIXONE Shop",
                Address = "Ho Chi Minh City, Vietnam",
                Latitude = 10.7769,
                Longitude = 106.7009
            };
            return View(setting);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateShop(ShopSetting model)
        {
            // Lấy bản ghi cũ lên
            var setting = await _context.ShopSettings.FirstOrDefaultAsync();

            if (setting == null)
            {
                // Nếu chưa có gì thì lưu cái model từ form gửi lên 
                _context.ShopSettings.Add(model);
            }
            else
            {
                setting.Latitude = model.Latitude;
                setting.Longitude = model.Longitude;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "The coordinates have been updated!";
            return RedirectToAction("ManageShop");
        }
        public async Task<IActionResult> Contact()
        {
            var shopInfo = await _context.ShopSettings.FirstOrDefaultAsync();
            ViewBag.ShopLocation = shopInfo; 
            return View();
        }
    }

}