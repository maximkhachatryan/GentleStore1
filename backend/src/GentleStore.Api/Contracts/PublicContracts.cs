namespace GentleStore.Api.Contracts;

public record PublicStoreListItemDto(string Slug, string Name, string? LogoUrl, string? Description, string Phone);

public record PublicStoreDto(
    string Slug, string Name, string? LogoUrl, string? Description, string Phone, string Currency,
    string AccessMode, StorefrontVisitorDto? Visitor);

public record PublicCategoryDto(Guid Id, string Name, int DisplayOrder, int ProductCount);

public record PublicTagDto(Guid Id, string Name);

public record PublicImageDto(string ImageUrl, int DisplayOrder);

public record PublicVariantAttributeDto(string Name, string Value);

public record PublicVariantDto(
    Guid Id, string? Sku, decimal Price, bool InStock,
    IReadOnlyList<PublicVariantAttributeDto> Attributes);

public record PublicProductListItemDto(
    Guid Id, string Name, decimal? Price, string Currency, bool InStock,
    string? PrimaryImageUrl, Guid CategoryId, IReadOnlyList<string> Tags);

public record PublicProductDto(
    Guid Id, string Name, string? Description, decimal? Price, string Currency,
    bool InStock, Guid CategoryId, string CategoryName,
    IReadOnlyList<PublicImageDto> Images, IReadOnlyList<PublicTagDto> Tags,
    IReadOnlyList<PublicVariantDto> Variants);

/// <summary>The signed-in customer, trimmed to what is safe to show on a possibly shared device.</summary>
public record StorefrontVisitorDto(string? DisplayName, string PhoneMasked);

/// <summary>
/// Everything the storefront needs to render its gate screen before it is allowed to see the
/// catalogue: enough branding to look trustworthy and the store's phone so a customer whose
/// link is spent can ask for a new one.
/// </summary>
public record StorefrontAccessDto(
    string Slug, string Name, string? LogoUrl, string Phone,
    string AccessMode, bool Unlocked, StorefrontVisitorDto? Visitor);

public record RedeemInviteRequest(string Token);

public record RedeemInviteResultDto(string Status, StorefrontVisitorDto? Visitor);
