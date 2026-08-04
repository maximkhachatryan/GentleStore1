namespace GentleStore.Api.Contracts;

public record StoreListItemDto(
    Guid Id, string Name, string Slug, string Phone, string? LogoUrl,
    bool IsActive, string Currency, int ProductCount, int CategoryCount, DateTime CreatedAt);

public record StoreDetailDto(
    Guid Id, string Name, string Slug, string Phone, string? LogoUrl,
    string? Description, string Currency, bool IsActive, DateTime CreatedAt);

public record CreateStoreRequest(
    string Name, string? Slug, string Phone, string? LogoUrl,
    string? Description, string? Currency, bool? IsActive);

public record UpdateStoreRequest(
    string Name, string? Slug, string Phone, string? LogoUrl,
    string? Description, string Currency, bool IsActive);

public record AdminUserDto(
    Guid Id, string Email, string FullName, string Role,
    Guid? StoreId, string? StoreName, bool IsActive, DateTime CreatedAt);

public record CreateUserRequest(string Email, string FullName, string Password, string Role, Guid? StoreId);

public record UpdateUserRequest(string FullName, string? Password, bool IsActive, string Role, Guid? StoreId);

public record AdminStatsDto(int StoreCount, int ActiveStoreCount, int ProductCount, int UserCount);
