using ECommerceClothing.Data;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Microsoft.AspNetCore.Http;
using ClosedXML.Excel;

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

        // ================= DASHBOARD =================
        public async Task<IActionResult> Dashboard()
        {
            var data = new ECommerceClothing.Models.Dashboard();

            // 1. Tính các con số tổng quát
            data.TotalProducts = await _context.Products.CountAsync();
            data.TotalOrders = await _context.Orders.CountAsync();
            data.TotalCustomers = await _context.Users.Where(u => u.Email != "admin@gmail.com").CountAsync();

            // 2. TỔNG DOANH THU THẬT (Chỉ loại bỏ đơn Cancelled)
            data.TotalRevenue = await _context.Orders
                .Where(o => !o.Status.Contains("Cancelled"))
                .SumAsync(o => o.TotalAmount);

            // 3. Lấy 5 đơn hàng mới nhất
            data.RecentOrders = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync();

            // 👉 4. DATA THẬT CHO BIỂU ĐỒ TRÒN (Chỉ lấy Delivered và Cancelled)
            int delivered = await _context.Orders.CountAsync(o => o.Status.Contains("Delivered"));
            int cancelled = await _context.Orders.CountAsync(o => o.Status.Contains("Cancelled"));
            data.OrderStatusCounts = new List<int> { delivered, cancelled };

            // 👉 5. DATA THẬT CHO BIỂU ĐỒ ĐƯỜNG (Doanh thu 7 ngày qua)
            var sevenDaysAgo = DateTime.Now.Date.AddDays(-6);
            var ordersLast7Days = await _context.Orders
                .Where(o => !o.Status.Contains("Cancelled") && o.OrderDate >= sevenDaysAgo)
                .ToListAsync();

            var labels = new List<string>();
            var revenueData = new List<decimal>();

            for (int i = 6; i >= 0; i--)
            {
                var targetDate = DateTime.Now.Date.AddDays(-i);
                labels.Add(targetDate.ToString("dd/MM")); // Trục ngang: Ngày/Tháng

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

        // ================= PRODUCTS =================
        public IActionResult Products(int? categoryId)
        {
            ViewBag.Categories = _context.Categories.ToList();

            var productsQuery = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                // THÊM: Bao gồm cả ProductSizes để sau này có thể hiển thị tồn kho chi tiết ra danh sách
                .Include(p => p.ProductSizes)
                .AsQueryable();

            if (categoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId);
            }

            return View("Products", productsQuery.ToList());
        }

        // ================= CREATE PRODUCT =================
        [HttpGet]
        public IActionResult CreateProduct()
        {
            LoadCategories();
            return View("CreateProduct");
        }

        private void LoadCategories()
        {
            ViewBag.Categories = _context.Categories.ToList();
            ViewBag.ProductTypes = _context.ProductTypes.ToList();
        }

        // API lấy danh sách ProductType theo Category (Cho Ajax Dropdown)
        [HttpGet]
        public IActionResult GetProductTypesByCategory(int categoryId)
        {
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
        // ✅ [UPDATE] Thêm 2 tham số List<string> và List<int> để hứng dữ liệu Size và Quantity từ form
        public async Task<IActionResult> CreateProduct(Product model, List<IFormFile> images, List<string> SelectedSizes, List<int> SelectedQuantities)
        {
            ModelState.Remove("Images");
            ModelState.Remove("Category");
            ModelState.Remove("Size");

            if (!ModelState.IsValid)
            {
                LoadCategories(); // Tách hàm nạp Dropdown ra cho gọn
                return View("CreateProduct", model);
            }

            model.CreatedAt = DateTime.Now;
            _context.Products.Add(model);
            await _context.SaveChangesAsync(); // Lưu để sinh ra Product.Id

            // ✅ [UPDATE] Lưu chi tiết Size và Stock vào bảng ProductSize
            int totalStock = 0;
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
                    totalStock += SelectedQuantities[i]; // Cộng dồn số lượng để ra tổng kho
                }
            }

            // Cập nhật lại tổng tồn kho (Stock) và chuỗi Size (để dễ view ngoài trang danh sách)
            model.Stock = totalStock;
            model.Size = string.Join(",", SelectedSizes ?? new List<string>());
            _context.Products.Update(model);
            await _context.SaveChangesAsync(); // Lưu bảng Size và update Product lần 2

            // Lưu hình ảnh
            if (images != null && images.Count > 0)
            {
                await UploadProductImages(model.Id, images);
            }

            return RedirectToAction("Products", "Admin", new { area = "Admin" });
        }

        // ================= EDIT PRODUCT (CHUẨN) =================
        // 1. Hàm GET
        [HttpGet]
        public async Task<IActionResult> EditProduct(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                // ✅ [UPDATE] Include ProductSizes để view form Edit có thể render ra checkbox và số lượng cũ
                .Include(p => p.ProductSizes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            // Nạp dữ liệu Dropdown
            ViewBag.Categories = _context.Categories.ToList();
            ViewBag.ProductTypes = _context.ProductTypes
            .Where(t => t.CategoryId == product.CategoryId)
            .ToList();

            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]

        // ✅ [UPDATE] Thêm tham số Size và Quantities cho Edit
        public async Task<IActionResult> EditProduct(Product model, List<IFormFile> images, List<int> deletedImageIds, List<string> SelectedSizes, List<int> SelectedQuantities)
        {
            ModelState.Remove("Category");
            ModelState.Remove("Images");
            ModelState.Remove("ProductType");
            ModelState.Remove("Size");
            ModelState.Remove("CreatedAt");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingProduct = await _context.Products
                        .Include(p => p.Images)
                        .Include(p => p.ProductSizes) // Lấy kèm Size cũ
                        .FirstOrDefaultAsync(p => p.Id == model.Id);

                    if (existingProduct == null) return NotFound();

                    // 2. Cập nhật dữ liệu cơ bản
                    existingProduct.Name = model.Name;
                    existingProduct.Price = model.Price;
                    existingProduct.CategoryId = model.CategoryId;
                    existingProduct.ProductTypeId = model.ProductTypeId;
                    existingProduct.Description = model.Description;

                    // Xóa toàn bộ Size cũ của SP này trước
                    if (existingProduct.ProductSizes != null && existingProduct.ProductSizes.Any())
                    {
                        _context.ProductSizes.RemoveRange(existingProduct.ProductSizes);
                    }

                    // Thêm lại Size mới
                    int totalStock = 0;
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
                            totalStock += SelectedQuantities[i];
                        }
                    }

                    // Cập nhật lại tổng tồn
                    existingProduct.Stock = totalStock;
                    existingProduct.Size = string.Join(",", SelectedSizes ?? new List<string>());

                    // 3. Xử lý xóa ảnh cũ
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

                    // 4. Thêm ảnh mới
                    if (images != null && images.Count > 0)
                    {
                        await UploadProductImages(model.Id, images);
                    }

                    // 5. Lưu vào DB
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Product update successful!";
                    return RedirectToAction("Products", "Admin", new { area = "Admin" });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error: " + ex.Message);
                }
            }

            // NẾU LỖI: Load lại ViewBag
            ViewBag.Categories = _context.Categories.ToList();
            ViewBag.ProductTypes = _context.ProductTypes
                .Where(t => t.CategoryId == model.CategoryId)
                .ToList();
            model.Images = _context.ProductImages.Where(p => p.ProductId == model.Id).ToList();

            return View("EditProduct", model);
        }

        // ================= DELETE PRODUCT =================
        public IActionResult Delete(int id)
        {
            var product = _context.Products.Find(id);

            if (product != null)
            {
                // Lưu ý: SQL Server thường sẽ chặn xóa Product nếu còn ProductSize dính với nó.
                // Nếu bị lỗi lúc xóa, bạn phải viết thêm lệnh xóa ProductSize trước:
                // var sizes = _context.ProductSizes.Where(s => s.ProductId == id).ToList();
                // _context.ProductSizes.RemoveRange(sizes);

                _context.Products.Remove(product);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Product deleted successfully!";
            }

            return RedirectToAction("Products", "Admin");
        }

        // ================= CATEGORIES =================
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

        // ================= HÀM PHỤ UPLOAD ẢNH =================
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

        // 1. DANH SÁCH VOUCHER (READ)
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

        // 2. THÊM MỚI VOUCHER (CREATE) - GET
        public IActionResult CreateVoucher()
        {
            return View();
        }

        // 3. THÊM MỚI VOUCHER (CREATE) - POST
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

        // 4. SỬA VOUCHER (EDIT) - GET
        public async Task<IActionResult> EditVoucher(int? id)
        {
            if (id == null) return NotFound();

            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher == null) return NotFound();

            return View(voucher);
        }

        // 5. SỬA VOUCHER (EDIT) - POST
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

        // 6. XÓA VOUCHER (DELETE) - POST
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

        // =======================================================
        // HÀM 1: DÀNH CHO TRANG DANH SÁCH (Chỉ lo việc tìm kiếm)
        // =======================================================
        public async Task<IActionResult> Customers(string search, string filter)
        {
            var query = _context.Users
            .Where(u => u.Email != "admin@gmail.com") 
            .AsQueryable();

            // Tính năng: Tìm kiếm (Search)
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(u =>
                    (u.FullName != null && u.FullName.ToLower().Contains(search)) ||
                    (u.Email != null && u.Email.ToLower().Contains(search))
                // Đã ẩn tìm sđt để chống lỗi
                );
            }

            var customers = await query.ToListAsync();

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentFilter = filter;

            return View(customers);
        }

        // ĐỔI LẠI THÀNH string id CHO KHỚP VỚI BẢNG ASPNETUSERS
        public async Task<IActionResult> CustomerDetails(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            // Hệ thống giờ sẽ tự động tìm trong bảng AspNetUsers
            var user = await _context.Users.FindAsync(id);

            if (user == null) return NotFound();

            var orders = await _context.Orders
                .Where(o => o.UserId == id) // Chỗ này hết bị lỗi cãi nhau Số/Chuỗi rồi!
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
                PhoneNumber = "--",
                TotalOrders = totalOrders,
                TotalSpent = totalSpent,
                CancelRate = cancelRate,
                OrderHistory = orders
            };

            return View(viewModel);
        }

        public async Task<IActionResult> ExportDashboardReport()
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
                // Tên Sheet tiếng Anh
                var worksheet = workbook.Worksheets.Add("Revenue_Report");

                // --- PHẦN 1: HEADER LÀM MÀU ---
                var titleCell = worksheet.Cell(1, 1);
                titleCell.Value = "E-COMMERCE REVENUE REPORT";
                titleCell.Style.Font.Bold = true;
                titleCell.Style.Font.FontSize = 16;
                titleCell.Style.Font.FontColor = XLColor.DarkBlue;
                worksheet.Range("A1:F1").Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                worksheet.Cell(2, 1).Value = $"Period: {sevenDaysAgo:dd/MM/yyyy} to {DateTime.Now:dd/MM/yyyy}";
                worksheet.Range("A2:F2").Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                worksheet.Range("A2:F2").Style.Font.Italic = true;

                // --- PHẦN 2: THỐNG KÊ NHANH ---
                worksheet.Cell(4, 1).Value = "TOTAL REVENUE:";
                worksheet.Cell(4, 2).Value = totalRevenue;
                worksheet.Cell(4, 2).Style.NumberFormat.Format = "#,##0\" VND\"";
                worksheet.Cell(4, 1).Style.Font.Bold = true;

                worksheet.Cell(5, 1).Value = "Delivered / Cancelled:";
                worksheet.Cell(5, 2).Value = $"{deliveredCount} orders / {cancelledCount} orders";
                worksheet.Cell(5, 1).Style.Font.Bold = true;

                // --- PHẦN 3: BẢNG CHI TIẾT (ĐỘ SKIN VIP) ---
                int startRow = 7;
                worksheet.Cell(startRow, 1).Value = "ORDER ID";
                worksheet.Cell(startRow, 2).Value = "CUSTOMER";
                worksheet.Cell(startRow, 3).Value = "PHONE";
                worksheet.Cell(startRow, 4).Value = "ORDER DATE";
                worksheet.Cell(startRow, 5).Value = "TOTAL AMOUNT";
                worksheet.Cell(startRow, 6).Value = "STATUS";

                // Decorate tiêu đề bảng
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

                    // Ép kiểu chữ để không bị mất số 0 và không hiện chấm xanh rác
                    worksheet.Cell(currentRow, 3).Value = "'" + order.PhoneNumber;

                    worksheet.Cell(currentRow, 4).Value = order.OrderDate.ToString("dd/MM/yyyy HH:mm");
                    worksheet.Cell(currentRow, 5).Value = order.TotalAmount;
                    worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0";

                    // Trạng thái (Đổi màu chữ theo status)
                    worksheet.Cell(currentRow, 6).Value = order.Status;
                    if (order.Status.Contains("Delivered"))
                        worksheet.Cell(currentRow, 6).Style.Font.FontColor = XLColor.Green;
                    if (order.Status.Contains("Cancelled"))
                        worksheet.Cell(currentRow, 6).Style.Font.FontColor = XLColor.Red;

                    // Kẻ khung (Border) cho từng dòng
                    worksheet.Range(currentRow, 1, currentRow, 6).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                    worksheet.Range(currentRow, 1, currentRow, 6).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

                    // Tô màu xen kẽ (Zebra)
                    if (currentRow % 2 == 0)
                        worksheet.Range(currentRow, 1, currentRow, 6).Style.Fill.BackgroundColor = XLColor.AliceBlue;

                    currentRow++;
                }

                // Kẻ khung cho tiêu đề bảng
                headerRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                headerRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);

                // --- PHẦN 4: DÒNG TỔNG KẾT BÊN DƯỚI BẢNG ---
                worksheet.Cell(currentRow, 4).Value = "GRAND TOTAL:";
                worksheet.Cell(currentRow, 4).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                // Cài công thức hàm SUM tự động của Excel
                worksheet.Cell(currentRow, 5).FormulaA1 = $"SUM(E{startRow + 1}:E{currentRow - 1})";
                worksheet.Cell(currentRow, 5).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0";

                worksheet.Columns().AdjustToContents(); // Auto-fit các cột

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    // Tên file tải xuống chuẩn tiếng Anh
                    string fileName = $"Revenue_Report_{DateTime.Now:dd_MM_yyyy}.xlsx";
                    return File(content, contentType, fileName);
                }
            }
        }
    }

}