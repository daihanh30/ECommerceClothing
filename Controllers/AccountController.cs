using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ECommerceClothing.Models;
using System.Net;
using System.Net.Mail;

namespace ECommerceClothing.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _config; // Thêm để đọc cấu hình Email

        public AccountController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration config) // Inject IConfiguration
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _config = config;
        }

        // ================= FORGOT PASSWORD (OTP) =================

        [HttpGet]

        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ViewBag.Error = "Please enter your email address.";
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                ViewBag.Error = "Email address not found in our system.";
                return View();
            }

            // 1. Tạo OTP 6 số
            string otpCode = new Random().Next(100000, 999999).ToString();

            // 2. Lưu vào DB (Hạn dùng 5 phút)
            user.ResetPasswordOtp = otpCode;
            user.OtpExpiryTime = DateTime.Now.AddMinutes(5);
            await _userManager.UpdateAsync(user);

            // 3. Gửi Email thực tế
            try
            {
                var senderEmail = _config["EmailSettings:SenderEmail"];
                var senderPassword = _config["EmailSettings:SenderPassword"];
                var senderName = _config["EmailSettings:SenderName"];

                var mail = new MailMessage();
                mail.To.Add(email);
                mail.From = new MailAddress(senderEmail, senderName);
                mail.Subject = "NIXONE - Password Reset OTP";
                mail.Body = $"<div style='font-family: Arial;'><h2>Password Reset Request</h2>" +
                           $"<p>Hello {user.FullName},</p>" +
                           $"<p>Your verification code is: <b style='font-size: 20px; color: #f87171;'>{otpCode}</b></p>" +
                           $"<p>This code will expire in 5 minutes. If you didn't request this, please ignore this email.</p></div>";
                mail.IsBodyHtml = true;

                using var smtp = new SmtpClient("smtp.gmail.com", 587);
                smtp.EnableSsl = true;
                smtp.Credentials = new NetworkCredential(senderEmail, senderPassword);
                await smtp.SendMailAsync(mail);

                return RedirectToAction("VerifyOTP", new { email = email });
            }
            catch (Exception)
            {
                ViewBag.Error = "An error occurred while sending the email. Please try again later.";
                return View();
            }
        }

        [HttpGet]
        public IActionResult VerifyOTP(string email)
        {
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOTP(string email, string otp)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null && user.ResetPasswordOtp == otp && user.OtpExpiryTime > DateTime.Now)
            {
                // Mã đúng và còn hạn -> Cho phép qua trang Reset
                return RedirectToAction("ResetPassword", new { email = email });
            }

            ViewBag.Email = email;
            ViewBag.Error = "Invalid or expired OTP code.";
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string email)
        {
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string email, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match!";
                ViewBag.Email = email;
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                // Xóa OTP sau khi dùng xong
                user.ResetPasswordOtp = null;
                user.OtpExpiryTime = null;
                await _userManager.UpdateAsync(user);

                // Reset mật khẩu bằng Identity
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

                if (result.Succeeded)
                {
                    return RedirectToAction("Login", new { resetSuccess = "true" });
                }

                ViewBag.Error = result.Errors.FirstOrDefault()?.Description;
            }

            ViewBag.Email = email;
            return View();
        }

        // ================= REGISTER (ĐĂNG KÝ) =================
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(User model, string confirmPassword)
        {
            if (model.Password != confirmPassword)
            {
                ViewBag.Error = "The re-entered password does not match!";
                return View(model);
            }

            var userExists = await _userManager.FindByEmailAsync(model.Email);
            if (userExists != null)
            {
                ViewBag.Error = "This email address has already been registered!";
                return View(model);
            }

            var user = new AppUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                CreatedAt = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                return RedirectToAction("Login", new { success = "true" });
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

                var result = await _userManager.CreateAsync(adminUser, "Admin@12345");
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(adminUser, "Admin");
                    return Content("Success! Admin created.");
                }
                return Content("Error: " + result.Errors.FirstOrDefault()?.Description);
            }

            if (!await _userManager.IsInRoleAsync(adminExist, "Admin"))
            {
                await _userManager.AddToRoleAsync(adminExist, "Admin");
                return Content("Admin role granted.");
            }

            return Content("Admin already exists!");
        }
    }
}