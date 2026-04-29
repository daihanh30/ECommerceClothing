

    //app.Run();
    using ECommerceClothing.Data;
    using ECommerceClothing.Models;
    using Microsoft.AspNetCore.Authentication.Cookies;  
    using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

    //  DATABASE 
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    //   MVC  
    builder.Services.AddControllersWithViews();

    // SESSION 
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

 
builder.Services.AddIdentity<ECommerceClothing.Models.AppUser, Microsoft.AspNetCore.Identity.IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
 
builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

 
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();

    app.UseSession();  
 
    app.UseAuthentication();
    app.UseAuthorization();

 
    app.MapControllerRoute(
        name: "admin_area",
        pattern: "Admin/{action=Dashboard}/{id?}",
        defaults: new { area = "Admin", controller = "Admin" }
    );
 
    app.MapControllerRoute(
        name: "areas_default",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
    );
 
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}"
    );

    app.Run();