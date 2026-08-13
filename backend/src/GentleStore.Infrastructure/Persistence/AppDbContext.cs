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
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<VariantAttribute> VariantAttributes => Set<VariantAttribute>();
    public DbSet<VariantAttributeDefinition> VariantAttributeDefinitions => Set<VariantAttributeDefinition>();
    public DbSet<VariantAttributeOption> VariantAttributeOptions => Set<VariantAttributeOption>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerInvite> CustomerInvites => Set<CustomerInvite>();
    public DbSet<CustomerSession> CustomerSessions => Set<CustomerSession>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

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
            e.Property(x => x.StorefrontAccess).HasConversion<int>();
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

        mb.Entity<VariantAttributeDefinition>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(80);
            e.HasOne(x => x.Store).WithMany(x => x.VariantAttributeDefinitions)
                .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.StoreId, x.Name }).IsUnique();
        });

        mb.Entity<VariantAttributeOption>(e =>
        {
            e.Property(x => x.Value).IsRequired().HasMaxLength(200);
            e.HasOne(x => x.Definition).WithMany(x => x.Options)
                .HasForeignKey(x => x.VariantAttributeDefinitionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.VariantAttributeDefinitionId, x.Value }).IsUnique();
        });

        mb.Entity<ProductVariant>(e =>
        {
            e.Property(x => x.Sku).HasMaxLength(80);
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.HasOne(x => x.Product).WithMany(x => x.Variants)
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ProductId, x.DisplayOrder });
        });

        mb.Entity<VariantAttribute>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(80);
            e.Property(x => x.Value).IsRequired().HasMaxLength(200);
            e.HasOne(x => x.Variant).WithMany(x => x.Attributes)
                .HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Definition).WithMany()
                .HasForeignKey(x => x.VariantAttributeDefinitionId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Option).WithMany()
                .HasForeignKey(x => x.VariantAttributeOptionId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.ProductVariantId, x.Name }).IsUnique();
        });

        mb.Entity<Customer>(e =>
        {
            e.Property(x => x.Phone).IsRequired().HasMaxLength(40);
            e.Property(x => x.PhoneNormalized).IsRequired().HasMaxLength(20);
            e.Property(x => x.FullName).HasMaxLength(150);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.Property(x => x.Origin).HasConversion<int>();
            e.HasOne(x => x.Store).WithMany(x => x.Customers)
                .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
            // One customer record per phone number within a store. This is what lets a guest
            // checkout and a later store invite land on the same person.
            e.HasIndex(x => new { x.StoreId, x.PhoneNormalized }).IsUnique();
        });

        mb.Entity<CustomerInvite>(e =>
        {
            e.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
            e.Property(x => x.RedeemedIp).HasMaxLength(64);
            e.Property(x => x.RedeemedUserAgent).HasMaxLength(400);
            e.HasOne(x => x.Customer).WithMany(x => x.Invites)
                .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Store).WithMany()
                .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
            // Redemption looks the invite up by hash alone, so it must be globally unique.
            e.HasIndex(x => x.TokenHash).IsUnique();
        });

        mb.Entity<CustomerSession>(e =>
        {
            e.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
            e.Property(x => x.CreatedIp).HasMaxLength(64);
            e.Property(x => x.LastSeenIp).HasMaxLength(64);
            e.Property(x => x.UserAgent).HasMaxLength(400);
            e.HasOne(x => x.Customer).WithMany(x => x.Sessions)
                .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Store).WithMany()
                .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Invite).WithMany(x => x.Sessions)
                .HasForeignKey(x => x.CustomerInviteId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.TokenHash).IsUnique();
        });

        mb.Entity<Order>(e =>
        {
            e.Property(x => x.OrderNumber).IsRequired().HasMaxLength(32);
            e.Property(x => x.ContactName).IsRequired().HasMaxLength(150);
            e.Property(x => x.ContactPhone).IsRequired().HasMaxLength(40);
            e.Property(x => x.ContactPhoneNormalized).IsRequired().HasMaxLength(20);
            e.Property(x => x.DeliveryAddress).HasMaxLength(500);
            e.Property(x => x.CustomerNote).HasMaxLength(1000);
            e.Property(x => x.StoreNote).HasMaxLength(1000);
            e.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.IdentityTier).HasConversion<int>();
            e.Property(x => x.Fulfilment).HasConversion<int>();

            e.HasOne(x => x.Store).WithMany(x => x.Orders)
                .HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
            // Deleting a customer must not erase the store's sales history, so orders survive as
            // records with the contact details they were placed with.
            e.HasOne(x => x.Customer).WithMany(x => x.Orders)
                .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Session).WithMany()
                .HasForeignKey(x => x.CustomerSessionId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => new { x.StoreId, x.OrderNumber }).IsUnique();
            e.HasIndex(x => new { x.StoreId, x.Status });
            e.HasIndex(x => new { x.StoreId, x.PlacedAt });
            // Order history for a self-declared browser is scoped to its own session.
            e.HasIndex(x => x.CustomerSessionId);
        });

        mb.Entity<OrderLine>(e =>
        {
            e.Property(x => x.ProductName).IsRequired().HasMaxLength(200);
            e.Property(x => x.VariantLabel).HasMaxLength(400);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Ignore(x => x.LineTotal);

            e.HasOne(x => x.Order).WithMany(x => x.Lines)
                .HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            // The product may be deleted long after the order; the line keeps its snapshot.
            e.HasOne<Product>().WithMany()
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<ProductVariant>().WithMany()
                .HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.OrderId, x.DisplayOrder });
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
