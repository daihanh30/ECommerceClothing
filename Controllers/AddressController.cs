using ECommerceClothing.Data;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity; // Cần thêm dòng này để dùng UserManager
using System.Security.Claims;

namespace ECommerceClothing.Controllers
{
    public class AddressController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager; // 1. Khai báo thêm UserManager

        // 2. Cập nhật Constructor để Inject UserManager vào
        public AddressController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 1. Lấy danh sách địa chỉ
        [HttpGet]
        public async Task<IActionResult> GetMyAddresses()
        {
            var userId = _userManager.GetUserId(User); // Dùng UserManager cho đồng bộ
            if (userId == null) return Json(new { success = false, msg = "Unauthorized" });

            var list = await _context.UserAddresses
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.IsDefault)
                .ToListAsync();

            return Json(new { success = true, data = list });
        }

        // 2. THÊM ĐỊA CHỈ MỚI
        [HttpPost]
        public async Task<IActionResult> AddAddress([FromBody] UserAddress model)
        {
            try
            {
                var userId = _userManager.GetUserId(User);
                if (userId == null) return Json(new { success = false, msg = "Please login again." });
                if (model == null) return Json(new { success = false, msg = "Data is null." });

                model.UserId = userId;

                // Xử lý logic địa chỉ mặc định
                if (model.IsDefault)
                {
                    var oldDefault = await _context.UserAddresses
                        .FirstOrDefaultAsync(x => x.UserId == userId && x.IsDefault);
                    if (oldDefault != null)
                    {
                        oldDefault.IsDefault = false;
                        _context.UserAddresses.Update(oldDefault);
                    }
                }
                else
                {
                    // Nếu chưa có địa chỉ nào thì cái đầu tiên mặc định là Default
                    if (!await _context.UserAddresses.AnyAsync(x => x.UserId == userId))
                    {
                        model.IsDefault = true;
                    }
                }

                _context.UserAddresses.Add(model);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, msg = "Server Error: " + ex.Message });
            }
        }

        // 3. Lấy địa chỉ mặc định
        [HttpGet]
        public async Task<IActionResult> GetDefaultAddress()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Json(new { success = false });

            var def = await _context.UserAddresses.FirstOrDefaultAsync(x => x.UserId == userId && x.IsDefault);
            return Json(new { success = true, data = def });
        }

        // 4. XÓA ĐỊA CHỈ (Đã sửa lỗi int/string và UserManager)
        [HttpPost]
        public async Task<IActionResult> DeleteAddress(int id) // 3. Đổi sang int id để khớp với Database
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Json(new { success = false, msg = "Unauthorized" });

            // Tìm địa chỉ khớp ID và đúng của User đó
            var address = await _context.UserAddresses
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (address == null)
            {
                return Json(new { success = false, msg = "Address not found!" });
            }

            if (address.IsDefault)
            {
                return Json(new { success = false, msg = "Cannot delete the default address!" });
            }

            _context.UserAddresses.Remove(address);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}