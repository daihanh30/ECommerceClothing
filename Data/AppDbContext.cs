using ECommerceClothing.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace ECommerceClothing.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
        public DbSet<Role> Roles { get; set; }

        //public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<ProductType> ProductTypes { get; set; }
        public DbSet<Order> Orders { get; set; } // Nhớ đặt tên là Orders (số nhiều)
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<UserAddress> UserAddresses { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<ProductSize> ProductSizes { get; set; }
        public DbSet<OrderDetail> OrderDetail { get; set; }
        public DbSet<Reviews> Reviews { get; set; }

    }
}