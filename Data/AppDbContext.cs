using Microsoft.EntityFrameworkCore;
using GoldenCornOrder.Models;

namespace GoldenCornOrder.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Category> Categories => Set<Category>();
        public DbSet<MenuItem> MenuItems => Set<MenuItem>();
        public DbSet<OptionGroup> OptionGroups => Set<OptionGroup>();
        public DbSet<OptionItem> OptionItems => Set<OptionItem>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<OrderItemOption> OrderItemOptions => Set<OrderItemOption>();
        public DbSet<StoreSetting> StoreSettings => Set<StoreSetting>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Precision for decimals
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(t => t.GetProperties())
                         .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("TEXT"); // SQLite stores decimal accurately as TEXT/REAL
            }

            modelBuilder.Entity<Category>()
                .HasMany(c => c.MenuItems)
                .WithOne(m => m.Category)
                .HasForeignKey(m => m.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MenuItem>()
                .HasMany(m => m.OptionGroups)
                .WithOne(g => g.MenuItem)
                .HasForeignKey(g => g.MenuItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OptionGroup>()
                .HasMany(g => g.Options)
                .WithOne(o => o.OptionGroup)
                .HasForeignKey(o => o.OptionGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
                .HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasMany(i => i.Options)
                .WithOne(o => o.OrderItem)
                .HasForeignKey(o => o.OrderItemId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
