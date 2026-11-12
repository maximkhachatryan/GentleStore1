namespace GentleStore.Api.Contracts;

public record StoreProfileDto(
    Guid Id, string Name, string Slug, string Phone, string? LogoUrl,
    string? Description, string Currency, bool IsActive, string StorefrontAccess);

public record UpdateStoreProfileRequest(
    string Name, string Phone, string? LogoUrl, string? Description, string Currency,
    string? StorefrontAccess);

public record CategoryDto(Guid Id, string Name, int DisplayOrder, int ProductCount);

public record CreateCategoryRequest(string Name, int DisplayOrder);

public record UpdateCategoryRequest(string Name, int DisplayOrder);

public record ProductImageDto(Guid Id, string ImageUrl, int DisplayOrder);

public record ProductTagDto(Guid Id, string Name);

public record VariantAttributeDto(Guid? DefinitionId, Guid? OptionId, string Name, string Value);

/// <param name="SalePrice">Stored as set, even after the sale ends; the product's SaleEndsAt says when it stops.</param>
public record ProductVariantDto(
    Guid Id, string? Sku, decimal Price, bool IsAvailable, int DisplayOrder,
    IReadOnlyList<VariantAttributeDto> Attributes, decimal? SalePrice);

public record CreateProductVariantRequest(
    string? Sku, decimal Price, bool IsAvailable, int DisplayOrder, List<Guid> OptionIds,
    decimal? SalePrice = null);

public record UpdateProductVariantRequest(
    string? Sku, decimal Price, bool IsAvailable, int DisplayOrder, List<Guid> OptionIds,
    decimal? SalePrice = null);

/// <param name="SalePrice">Stored as set; whether it currently applies also depends on <paramref name="SaleEndsAt"/>.</param>
/// <param name="SaleEndsAt">When the sale stops for the product and its variants; null runs until ended.</param>
public record ProductDto(
    Guid Id, Guid CategoryId, string CategoryName, string Name, string? Description,
    decimal? Price, bool IsAvailable, int DisplayOrder,
    IReadOnlyList<ProductImageDto> Images, IReadOnlyList<ProductTagDto> Tags,
    IReadOnlyList<ProductVariantDto> Variants, DateTime CreatedAt,
    decimal? SalePrice, DateTime? SaleEndsAt);

public record CreateProductRequest(
    Guid CategoryId, string Name, string? Description, decimal? Price,
    bool IsAvailable, int DisplayOrder, List<Guid>? TagIds,
    decimal? SalePrice = null, DateTime? SaleEndsAt = null);

public record UpdateProductRequest(
    Guid CategoryId, string Name, string? Description, decimal? Price,
    bool IsAvailable, int DisplayOrder, List<Guid>? TagIds,
    decimal? SalePrice = null, DateTime? SaleEndsAt = null);

/// <summary>
/// Puts every product in scope on sale at <paramref name="Percent"/> off its regular price,
/// replacing any sale already set on them. With neither filter it covers the whole store; with both,
/// products that match both.
/// </summary>
/// <param name="EndsAt">When the sale stops; null runs until it is ended.</param>
public record ApplySaleRequest(Guid? CategoryId, Guid? TagId, int Percent, DateTime? EndsAt);

/// <summary>Takes every product in scope off sale. Scope works as in <see cref="ApplySaleRequest"/>.</summary>
public record EndSaleRequest(Guid? CategoryId, Guid? TagId);

/// <param name="ProductCount">How many products changed.</param>
public record SaleResultDto(int ProductCount);

public record AddProductImageRequest(string ImageUrl, int? DisplayOrder);

public record TagDto(Guid Id, string Name, int DisplayOrder);

public record CreateTagRequest(string Name, int DisplayOrder);

public record UpdateTagRequest(string Name, int DisplayOrder);

public record VariantAttributeOptionDto(Guid Id, string Value, int DisplayOrder);

public record VariantAttributeDefinitionDto(
    Guid Id, string Name, int DisplayOrder, IReadOnlyList<VariantAttributeOptionDto> Options);

public record CreateVariantAttributeDefinitionRequest(string Name, int DisplayOrder);

public record UpdateVariantAttributeDefinitionRequest(string Name, int DisplayOrder);

public record CreateVariantAttributeOptionRequest(string Value, int DisplayOrder);

public record UpdateVariantAttributeOptionRequest(string Value, int DisplayOrder);

// ---- Storefront customers ----

/// <param name="Status">
/// Derived, not stored: <c>blocked</c>, <c>active</c> (at least one signed-in device),
/// <c>invited</c> (link outstanding), <c>expired</c> (link lapsed unused), <c>new</c>.
/// </param>
/// <param name="Origin">
/// <c>StoreInvited</c> or <c>SelfRegistered</c> — whether staff added this person or they
/// introduced themselves at a public checkout.
/// </param>
public record CustomerDto(
    Guid Id, string Phone, string PhoneNormalized, string? FullName, string? Note,
    bool IsBlocked, string Status, string Origin, int ActiveDeviceCount, int OrderCount,
    DateTime? PendingInviteExpiresAt, DateTime? LastSeenAt, DateTime? FirstActivatedAt, DateTime CreatedAt);

public record CreateCustomerRequest(string Phone, string? FullName, string? Note);

public record UpdateCustomerRequest(string Phone, string? FullName, string? Note);

/// <summary>
/// The one and only time the invite secret leaves the server — the database keeps just its hash,
/// so a link that is not delivered now has to be regenerated.
/// </summary>
public record CustomerInviteLinkDto(Guid Id, string Url, DateTime ExpiresAt);

/// <param name="Status"><c>pending</c>, <c>used</c>, <c>revoked</c> or <c>expired</c>.</param>
public record CustomerInviteDto(
    Guid Id, string Status, DateTime CreatedAt, DateTime ExpiresAt,
    DateTime? RedeemedAt, DateTime? RevokedAt, string? RedeemedUserAgent);

/// <summary>One browser the customer is signed in on.</summary>
public record CustomerDeviceDto(Guid Id, DateTime CreatedAt, DateTime LastSeenAt, string? UserAgent);

public record CustomerDetailDto(
    CustomerDto Customer,
    IReadOnlyList<CustomerDeviceDto> Devices,
    IReadOnlyList<CustomerInviteDto> Invites);

/// <param name="Available">False when this deployment has no bot configured.</param>
public record TelegramStatusDto(bool Available, bool Linked, string? BotUsername);

public record LinkTelegramRequest(string? Language);

public record TelegramLinkDto(string Url, DateTime ExpiresAt);
