namespace GentleStore.Api.Contracts;

public record StoreListItemDto(
    Guid Id, string Name, string Slug, string? CustomDomain, string Phone, string? LogoUrl,
    bool IsActive, string Currency, bool PaymentsEnabled,
    int ProductCount, int CategoryCount, DateTime CreatedAt);

public record StoreDetailDto(
    Guid Id, string Name, string Slug, string? CustomDomain, string Phone, string? LogoUrl,
    string? Description, string Currency, bool IsActive, bool PaymentsEnabled, DateTime CreatedAt);

/// <param name="CustomDomain">The store's own domain, if any; blank for none.</param>
public record CreateStoreRequest(
    string Name, string? Slug, string? CustomDomain, string Phone, string? LogoUrl,
    string? Description, string? Currency, bool? IsActive);

/// <param name="CustomDomain">The store's own domain; blank removes it.</param>
public record UpdateStoreRequest(
    string Name, string? Slug, string? CustomDomain, string Phone, string? LogoUrl,
    string? Description, string Currency, bool IsActive);

public record AdminUserDto(
    Guid Id, string Email, string FullName, string Role,
    Guid? StoreId, string? StoreName, bool IsActive, DateTime CreatedAt);

public record CreateUserRequest(string Email, string FullName, string Password, string Role, Guid? StoreId);

public record UpdateUserRequest(string FullName, string? Password, bool IsActive, string Role, Guid? StoreId);

public record AdminStatsDto(int StoreCount, int ActiveStoreCount, int ProductCount, int UserCount);

// ---- Store payments ----

/// <param name="ServiceAvailable">
/// False when PaymentService is not configured or not answering. The screen shows why rather than
/// offering a switch that cannot work.
/// </param>
/// <param name="Methods">Every payment method this deployment can offer, with the store's current choice marked.</param>
public record StorePaymentSettingsDto(
    Guid StoreId,
    string StoreName,
    string Currency,
    bool PaymentsEnabled,
    bool ServiceAvailable,
    string? ServiceProblem,
    string? CredentialRef,
    string Mode,
    IReadOnlyList<PaymentMethodDto> Methods);

/// <param name="CurrencySupported">
/// Whether the method can take this store's currency at all. A store priced in USD cannot use an
/// AMD-only processor, and finding that out at a customer's checkout is far too late.
/// </param>
public record PaymentMethodDto(
    string Key,
    string DisplayName,
    IReadOnlyList<string> SupportedCurrencies,
    bool CurrencySupported,
    bool Enabled);

/// <param name="CredentialRef">
/// Names the store's own credentials inside PaymentService's secret store — never the API key
/// itself, which must not travel through this application or its logs.
/// </param>
/// <param name="Mode">"Test" or "Live".</param>
public record UpdateStorePaymentsRequest(
    bool Enabled,
    string? CredentialRef,
    string? Mode,
    List<string>? Methods);
