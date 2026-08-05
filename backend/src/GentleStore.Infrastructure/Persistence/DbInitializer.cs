using GentleStore.Domain.Entities;
using GentleStore.Domain.Enums;
using GentleStore.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GentleStore.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        string superAdminEmail,
        string superAdminPassword,
        string demoOwnerPassword,
        bool seedDemoData = true)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasher>();

        await db.Database.MigrateAsync();

        if (!await db.Users.AnyAsync(u => u.Role == UserRole.SuperAdmin))
        {
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = superAdminEmail,
                FullName = "Platform Administrator",
                PasswordHash = hasher.Hash(superAdminPassword),
                Role = UserRole.SuperAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        if (!seedDemoData)
            return;

        if (await db.Stores.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        var bloom = BuildBloomPetal(now);
        var bean = BuildBeanScene(now);
        db.Stores.AddRange(bloom, bean);

        db.Users.AddRange(
            new User
            {
                Id = Guid.NewGuid(),
                Email = "owner@bloom-petal.local",
                FullName = "Bloom & Petal Owner",
                PasswordHash = hasher.Hash(demoOwnerPassword),
                Role = UserRole.StoreOwner,
                StoreId = bloom.Id,
                IsActive = true,
                CreatedAt = now
            },
            new User
            {
                Id = Guid.NewGuid(),
                Email = "owner@bean-scene.local",
                FullName = "Bean Scene Owner",
                PasswordHash = hasher.Hash(demoOwnerPassword),
                Role = UserRole.StoreOwner,
                StoreId = bean.Id,
                IsActive = true,
                CreatedAt = now
            });

        await db.SaveChangesAsync();
    }

    private static Store BuildBloomPetal(DateTime now)
    {
        var store = new Store
        {
            Id = Guid.NewGuid(),
            Name = "Bloom & Petal",
            Slug = "bloom-petal",
            Phone = "+15551234567",
            LogoUrl = "https://picsum.photos/seed/bloompetal-logo/200/200",
            Description = "Fresh, hand-tied bouquets and houseplants delivered with love.",
            Currency = "USD",
            IsActive = true,
            CreatedAt = now
        };

        var bouquets = new Category { Id = Guid.NewGuid(), Name = "Bouquets", DisplayOrder = 1 };
        var plants = new Category { Id = Guid.NewGuid(), Name = "House Plants", DisplayOrder = 2 };
        var gifts = new Category { Id = Guid.NewGuid(), Name = "Gifts", DisplayOrder = 3 };
        store.Categories.Add(bouquets);
        store.Categories.Add(plants);
        store.Categories.Add(gifts);

        var tagNew = new Tag { Id = Guid.NewGuid(), Name = "New", DisplayOrder = 1 };
        var tagPopular = new Tag { Id = Guid.NewGuid(), Name = "Popular", DisplayOrder = 2 };
        var tagSale = new Tag { Id = Guid.NewGuid(), Name = "Sale", DisplayOrder = 3 };
        store.Tags.Add(tagNew);
        store.Tags.Add(tagPopular);
        store.Tags.Add(tagSale);

        AddProduct(store, bouquets, "Sunrise Bouquet", "A cheerful mix of roses, tulips and gerberas.", 39.90m, 25, 1,
            new[] { "bloom-sunrise-1", "bloom-sunrise-2" }, new[] { tagPopular, tagNew });
        AddProduct(store, bouquets, "Blush Peony Bunch", "Soft pink peonies wrapped in kraft paper.", 54.00m, 12, 2,
            new[] { "bloom-peony-1" }, new[] { tagPopular });
        AddProduct(store, plants, "Monstera Deliciosa", "Lush split-leaf plant in a ceramic pot.", 45.50m, 8, 1,
            new[] { "bloom-monstera-1" }, new[] { tagNew });
        AddProduct(store, plants, "Snake Plant", "Low-maintenance air purifier, perfect for offices.", 28.00m, 30, 2,
            new[] { "bloom-snake-1" }, new[] { tagSale });
        AddProduct(store, gifts, "Ceramic Vase Set", "Set of two hand-glazed vases.", 34.00m, 15, 1,
            new[] { "bloom-vase-1" }, new[] { tagSale });

        return store;
    }

    private static Store BuildBeanScene(DateTime now)
    {
        var store = new Store
        {
            Id = Guid.NewGuid(),
            Name = "Bean Scene Coffee",
            Slug = "bean-scene",
            Phone = "+15559876543",
            LogoUrl = "https://picsum.photos/seed/beanscene-logo/200/200",
            Description = "Small-batch roasted coffee, loose-leaf tea and fresh pastries.",
            Currency = "USD",
            IsActive = true,
            CreatedAt = now
        };

        var coffee = new Category { Id = Guid.NewGuid(), Name = "Coffee", DisplayOrder = 1 };
        var tea = new Category { Id = Guid.NewGuid(), Name = "Tea", DisplayOrder = 2 };
        var pastries = new Category { Id = Guid.NewGuid(), Name = "Pastries", DisplayOrder = 3 };
        store.Categories.Add(coffee);
        store.Categories.Add(tea);
        store.Categories.Add(pastries);

        var tagNew = new Tag { Id = Guid.NewGuid(), Name = "New", DisplayOrder = 1 };
        var tagBest = new Tag { Id = Guid.NewGuid(), Name = "Bestseller", DisplayOrder = 2 };
        var tagOrganic = new Tag { Id = Guid.NewGuid(), Name = "Organic", DisplayOrder = 3 };
        store.Tags.Add(tagNew);
        store.Tags.Add(tagBest);
        store.Tags.Add(tagOrganic);

        AddProduct(store, coffee, "House Blend Beans 1kg", "Balanced medium roast with chocolate notes.", 22.00m, 40, 1,
            new[] { "bean-house-1" }, new[] { tagBest });
        AddProduct(store, coffee, "Ethiopia Single Origin 250g", "Bright and floral, hints of citrus.", 15.50m, 22, 2,
            new[] { "bean-ethiopia-1" }, new[] { tagNew, tagOrganic });
        AddProduct(store, tea, "Jasmine Green Tea 100g", "Fragrant jasmine-scented loose-leaf green tea.", 12.00m, 35, 1,
            new[] { "bean-jasmine-1" }, new[] { tagOrganic });
        AddProduct(store, pastries, "Butter Croissant", "Flaky, all-butter croissant baked fresh daily.", 3.50m, 0, 1,
            new[] { "bean-croissant-1" }, new[] { tagBest });
        AddProduct(store, pastries, "Almond Danish", "Buttery pastry with almond cream and flaked almonds.", 4.20m, 18, 2,
            new[] { "bean-danish-1" }, Array.Empty<Tag>());

        return store;
    }

    private static void AddProduct(
        Store store,
        Category category,
        string name,
        string description,
        decimal price,
        int stock,
        int order,
        string[] imageSeeds,
        Tag[] tags)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Store = store,
            Category = category,
            Name = name,
            Description = description,
            Price = price,
            IsAvailable = stock > 0,
            DisplayOrder = order,
            CreatedAt = store.CreatedAt
        };

        var imgOrder = 1;
        foreach (var seed in imageSeeds)
        {
            product.Images.Add(new ProductImage
            {
                Id = Guid.NewGuid(),
                ImageUrl = $"https://picsum.photos/seed/{seed}/800/800",
                DisplayOrder = imgOrder++
            });
        }

        foreach (var tag in tags)
            product.ProductTags.Add(new ProductTag { Product = product, Tag = tag });

        store.Products.Add(product);
    }
}
