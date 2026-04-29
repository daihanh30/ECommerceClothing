using ECommerceClothing.Data;
using ECommerceClothing.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;

namespace ECommerceClothing.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;

        public AccountController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext context)  
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
        }

        //FORGOT PASSWORD (OTP) 

        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {

            if (string.IsNullOrWhiteSpace(email))
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

            string otpCode = new Random().Next(100000, 999999).ToString();

            user.ResetPasswordOtp = otpCode;
            user.OtpExpiryTime = DateTime.Now.AddMinutes(5);
            await _userManager.UpdateAsync(user);

            try
            {
                var emailSettings = await _context.ShopSettings.FirstOrDefaultAsync();

                if (emailSettings == null || string.IsNullOrEmpty(emailSettings.SenderEmail) || string.IsNullOrEmpty(emailSettings.SenderPassword))
                {
                    ViewBag.Error = "The email system has not been configured. Please contact the administrator.";
                    return View();
                }

                var senderEmail = emailSettings.SenderEmail;
                var senderPassword = emailSettings.SenderPassword;
                var senderName = emailSettings.SenderName ?? "Nixone Official";

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
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi gửi mail: " + ex.Message);
                ViewBag.Error = "An error occurred while sending the email. Please try again later.";
                return View();
            }
        }
        

        // Hiển thị ra UI
        [HttpGet]
        public IActionResult VerifyOTP(string email)
        {
            ViewBag.Email = email;
            return View();
        }

        // form ở UI khi submit sẽ vào đây
        [HttpPost]
        public async Task<IActionResult> VerifyOTP(string email, string otp)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (string.IsNullOrWhiteSpace(otp))
            {
                ViewBag.Email = email;
                ViewBag.Error = "Please enter the OTP code.";
                return View();
            }
            if (user != null && user.ResetPasswordOtp == otp && user.OtpExpiryTime > DateTime.Now)
            {
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
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                ViewBag.Error = "The new password must not be blank or contain only spaces!";
                ViewBag.Email = email;
                return View();
            }
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                user.ResetPasswordOtp = null;
                user.OtpExpiryTime = null;
                await _userManager.UpdateAsync(user);

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

        //REGISTER 
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(User model, string confirmPassword)
        {
            bool isNameEmpty = string.IsNullOrWhiteSpace(model.FullName);
            bool isEmailEmpty = string.IsNullOrWhiteSpace(model.Email);
            bool isPassEmpty = string.IsNullOrWhiteSpace(model.Password);
            bool isConfirmEmpty = string.IsNullOrWhiteSpace(confirmPassword);

            if (isNameEmpty && isEmailEmpty && isPassEmpty && isConfirmEmpty)
            {
                ViewBag.Error = "Please enter all the required information!";
                return View(model);
            }

            if (isNameEmpty)
            {
                ViewBag.Error = "Invalid full name (cannot be empty or contain only spaces)!";
                return View(model);
            }

            if (isEmailEmpty)
            {
                ViewBag.Error = "Invalid email (cannot be empty or contain only spaces)!";
                return View(model);
            }

            if (!model.Email.Contains("@") || !model.Email.Contains("."))
            {
                ViewBag.Error = "Please enter a valid email address (must include '@')!";
                return View(model);
            }

            if (isPassEmpty)
            {
                ViewBag.Error = "Invalid password (cannot be empty or contain only spaces)!";
                return View(model);
            }

            if (isConfirmEmpty)
            {
                ViewBag.Error = "Please confirm your password!";

                return View(model);
            }

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

        //LOGIN 
        [HttpGet]
        public IActionResult Login()
        {
            if (_signInManager.IsSignedIn(User)) return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            bool isEmailEmpty = string.IsNullOrWhiteSpace(email);
            bool isPasswordEmpty = string.IsNullOrWhiteSpace(password);

            if (isEmailEmpty && isPasswordEmpty)
            {
                ViewBag.Error = "Please enter both email and password!";
                ViewBag.Email = email;
                return View();
            }

            if (isEmailEmpty)
            {
                ViewBag.Error = "Invalid email (cannot be empty or contain only spaces)!";
                ViewBag.Email = email;
                return View();
            }

            if (isPasswordEmpty)
            {
                ViewBag.Error = "Invalid password (cannot be empty or contain only spaces)!";
                ViewBag.Email = email;
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
            ViewBag.Email = email;
            return View();
        }

        //LOGOUT
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        //TẠO ADMIN 
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