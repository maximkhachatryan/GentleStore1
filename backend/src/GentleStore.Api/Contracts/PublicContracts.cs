namespace GentleStore.Api.Contracts;

public record PublicStoreListItemDto(string Slug, string Name, string? LogoUrl, string? Description, string Phone);

public record PublicStoreDto(string Slug, string Name, string? LogoUrl, string? Description, string Phone, string Currency);

public record PublicCategoryDto(Guid Id, string Name, int DisplayOrder, int ProductCount);

public record PublicTagDto(Guid Id, string Name);

public record PublicImageDto(string ImageUrl, int DisplayOrder);

public record PublicProductListItemDto(
    Guid Id, string Name, decimal Price, string Currency, bool InStock,
    string? PrimaryImageUrl, Guid CategoryId, IReadOnlyList<string> Tags);

public record PublicProductDto(
    Guid Id, string Name, string? Description, decimal Price, string Currency,
    bool InStock, Guid CategoryId, string CategoryName,
    IReadOnlyList<PublicImageDto> Images, IReadOnlyList<PublicTagDto> Tags);
