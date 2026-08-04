namespace GentleStore.Api.Contracts;

public record StoreProfileDto(
    Guid Id, string Name, string Slug, string Phone, string? LogoUrl,
    string? Description, string Currency, bool IsActive);

public record UpdateStoreProfileRequest(
    string Name, string Phone, string? LogoUrl, string? Description, string Currency);

public record CategoryDto(Guid Id, string Name, int DisplayOrder, int ProductCount);

public record CreateCategoryRequest(string Name, int DisplayOrder);

public record UpdateCategoryRequest(string Name, int DisplayOrder);

public record ProductImageDto(Guid Id, string ImageUrl, int DisplayOrder);

public record ProductTagDto(Guid Id, string Name);

public record ProductDto(
    Guid Id, Guid CategoryId, string CategoryName, string Name, string? Description,
    decimal Price, int StockQuantity, bool IsAvailable, int DisplayOrder,
    IReadOnlyList<ProductImageDto> Images, IReadOnlyList<ProductTagDto> Tags, DateTime CreatedAt);

public record CreateProductRequest(
    Guid CategoryId, string Name, string? Description, decimal Price,
    int StockQuantity, bool IsAvailable, int DisplayOrder, List<Guid>? TagIds);

public record UpdateProductRequest(
    Guid CategoryId, string Name, string? Description, decimal Price,
    int StockQuantity, bool IsAvailable, int DisplayOrder, List<Guid>? TagIds);

public record AddProductImageRequest(string ImageUrl, int? DisplayOrder);

public record TagDto(Guid Id, string Name, int DisplayOrder);

public record CreateTagRequest(string Name, int DisplayOrder);

public record UpdateTagRequest(string Name, int DisplayOrder);
