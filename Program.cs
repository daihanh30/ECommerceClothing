

    //app.Run();
    using ECommerceClothing.Data;
    using ECommerceClothing.Models;
    using Microsoft.AspNetCore.Authentication.Cookies; // <-- THÊM THƯ VIỆN NÀY
    using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

    // ================= DATABASE =================
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // ================= MVC =================
    builder.Services.AddControllersWithViews();

    // ================= SESSION =================
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });


// ================= AUTHENTICATION (QUAN TRỌNG - THÊM ĐOẠN NÀY) =================
// Để lệnh User.IsInRole("Admin") hoạt động, bạn cần cấu hình Cookie Auth
//builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
//    .AddCookie(options =>
//    {
//        options.LoginPath = "/Account/Login"; // Đường dẫn trang đăng nhập
//        options.AccessDeniedPath = "/Account/AccessDenied"; // Đường dẫn khi bị cấm truy cập
//    });
// ✅ DÁN ĐOẠN NÀY VÀO:
builder.Services.AddIdentity<ECommerceClothing.Models.AppUser, Microsoft.AspNetCore.Identity.IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Cấu hình lại đường dẫn login (Vì Identity mặc định nó trỏ về /Identity/Account/Login)
builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();


    // ================= PIPELINE =================
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseSession(); // Session phải đứng trước Authentication

    // Kích hoạt Middleware xác thực và phân quyền
    app.UseAuthentication();
    app.UseAuthorization();


    // --- THÊM ĐOẠN NÀY (Định tuyến cho Admin) ---
    // 1. Định tuyến cho Admin (Ưu tiên số 1)
    app.MapControllerRoute(
        name: "admin_area",
        pattern: "Admin/{action=Dashboard}/{id?}",
        defaults: new { area = "Admin", controller = "Admin" }
    );

    // 2. Định tuyến chung cho các Area khác (nếu có)
    app.MapControllerRoute(
        name: "areas_default",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
    );

    // 3. Định tuyến mặc định cho trang chủ khách hàng (BẮT BUỘC PHẢI CÓ DÒNG NÀY)
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}"
    );

    app.Run();