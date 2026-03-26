using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ECommerceClothing.Models;

namespace ECommerceClothing.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager; // Đã thêm RoleManager

        // Đã cập nhật Constructor để nhận RoleManager
        public AccountController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
        }

        // ================= REGISTER (ĐĂNG KÝ) =================
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(User model, string confirmPassword) // Hứng dữ liệu từ Form
        {
            // 1. Kiểm tra Confirm Password
            if (model.Password != confirmPassword)
            {
                ViewBag.Error = "The re-entered password does not match.!";
                return View(model);
            }

            // 2. Kiểm tra Email đã tồn tại chưa
            var userExists = await _userManager.FindByEmailAsync(model.Email);
            if (userExists != null)
            {
                ViewBag.Error = "This email address has already been registered!";
                return View(model);
            }

            // 3. Tạo User mới
            var user = new AppUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                CreatedAt = DateTime.Now
            };

            // 4. Lưu xuống DB
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                return RedirectToAction("Login", "Account", new { success = "true" });
            }

            ViewBag.Error = result.Errors.FirstOrDefault()?.Description;
            return View(model);
        }

        // ================= LOGIN (ĐĂNG NHẬP) =================
        [HttpGet]
        public IActionResult Login()
        {
            if (_signInManager.IsSignedIn(User)) return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Please enter all the required information!";
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(user, password, false, false);
                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "Home");
                }
            }

            ViewBag.Error = "Incorrect email or password!";
            return View();
        }

        // ================= LOGOUT =================
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // ================= TẠO ADMIN (CHẠY 1 LẦN) =================
        [HttpGet]
        public async Task<IActionResult> CreateAdmin()
        {
            // 1. Tạo Role "Admin" vào Database nếu chưa có
            if (!await _roleManager.RoleExistsAsync("Admin"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            var adminEmail = "admin@gmail.com";
            var adminExist = await _userManager.FindByEmailAsync(adminEmail);

            if (adminExist == null)
            {
                var adminUser = new AppUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Admin",
                    CreatedAt = DateTime.Now
                };

                // Nhờ Identity tạo tài khoản
                var result = await _userManager.CreateAsync(adminUser, "Admin@12345");

                if (result.Succeeded)
                {
                    // 2. Gắn Role "Admin" cho tài khoản vừa tạo
                    await _userManager.AddToRoleAsync(adminUser, "Admin");
                    return Content("Tuyệt vời! Đã tạo Admin và cấp quyền thành công. Tài khoản: admin@gmail.com | Mật khẩu: Admin@12345");
                }

                return Content("Lỗi tạo Admin: " + result.Errors.FirstOrDefault()?.Description);
            }
            else
            {
                // 3. Nếu tài khoản đã tồn tại, đảm bảo nó được gắn Role Admin
                if (!await _userManager.IsInRoleAsync(adminExist, "Admin"))
                {
                    await _userManager.AddToRoleAsync(adminExist, "Admin");
                    return Content("Đã cấp thêm quyền Admin cho tài khoản hiện tại.");
                }
            }

            return Content("Tài khoản Admin đã tồn tại và đã có quyền!");
        }
    }
}