using GentleStore.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ProductTag> ProductTags => Set<ProductTag>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        mb.Entity<Store>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Slug).IsRequired().HasMaxLength(120);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Phone).IsRequired().HasMaxLength(40);
            e.Property(x => x.LogoUrl).HasMaxLength(1000);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        });

        mb.Entity<Category>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(150);
            e.HasOne(x => x.Store).WithMany(x => x.Categories)
                .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.StoreId, x.DisplayOrder });
        });

        mb.Entity<Product>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.HasOne(x => x.Store).WithMany(x => x.Products)
                .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Category).WithMany(x => x.Products)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.StoreId, x.CategoryId });
        });

        mb.Entity<ProductImage>(e =>
        {
            e.Property(x => x.ImageUrl).IsRequired().HasMaxLength(1000);
            e.HasOne(x => x.Product).WithMany(x => x.Images)
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Tag>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(80);
            e.HasOne(x => x.Store).WithMany(x => x.Tags)
                .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.StoreId, x.Name }).IsUnique();
        });

        mb.Entity<ProductTag>(e =>
        {
            e.HasKey(x => new { x.ProductId, x.TagId });
            e.HasOne(x => x.Product).WithMany(x => x.ProductTags)
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Tag).WithMany(x => x.ProductTags)
                .HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<User>(e =>
        {
            e.Property(x => x.Email).IsRequired().HasMaxLength(256);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(200);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(150);
            e.Property(x => x.Role).HasConversion<int>();
            e.HasOne(x => x.Store).WithMany(x => x.Users)
                .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
