namespace GentleStore.Api.Contracts;

// ---- Storefront ----

public record PlaceOrderItemRequest(Guid ProductId, Guid? VariantId, int Quantity);

/// <param name="ContactName">
/// Ignored when the browser already carries a customer session — the stored record wins, so a
/// checkout cannot rename a customer the store curated.
/// </param>
/// <param name="ContactPhone">Required only for a guest checkout on a public storefront.</param>
public record PlaceOrderRequest(
    List<PlaceOrderItemRequest> Items,
    string Fulfilment,
    string? DeliveryAddress,
    string? Note,
    string? ContactName,
    string? ContactPhone);

public record PublicOrderLineDto(
    Guid? ProductId, string ProductName, string? VariantLabel,
    int Quantity, decimal? UnitPrice, decimal? LineTotal);

/// <param name="ContactPhoneMasked">Masked even for its owner — storefronts get opened on shared phones.</param>
public record PublicOrderDto(
    Guid Id, string OrderNumber, string Status, string Currency, decimal? Total,
    string Fulfilment, string? DeliveryAddress, string? Note,
    string ContactName, string ContactPhoneMasked, bool AwaitingQuote,
    DateTime PlacedAt, IReadOnlyList<PublicOrderLineDto> Lines);

// ---- Backoffice ----

public record OrderLineDto(
    Guid Id, Guid? ProductId, Guid? ProductVariantId, string ProductName, string? VariantLabel,
    int Quantity, decimal? UnitPrice, decimal? LineTotal);

/// <param name="IdentityTier">
/// <c>Invited</c> (phone verified by the invite the store sent), <c>Returning</c> or <c>Guest</c>
/// (self-declared). This is what tells staff whether to trust the contact details on sight.
/// </param>
public record OrderListItemDto(
    Guid Id, string OrderNumber, string Status, string IdentityTier,
    Guid CustomerId, string ContactName, string ContactPhone,
    string Currency, decimal? Total, int ItemCount, string Fulfilment,
    int CustomerOrderCount, DateTime PlacedAt);

public record OrderDetailDto(
    Guid Id, string OrderNumber, string Status, string IdentityTier,
    Guid CustomerId, string ContactName, string ContactPhone, string ContactPhoneNormalized,
    string CustomerOrigin, int CustomerOrderCount,
    string Fulfilment, string? DeliveryAddress, string? CustomerNote, string? StoreNote,
    string Currency, decimal? Total, DateTime PlacedAt, DateTime UpdatedAt,
    IReadOnlyList<OrderLineDto> Lines);

public record UpdateOrderStatusRequest(string Status, string? StoreNote);

public record QuoteOrderLineRequest(Guid LineId, decimal UnitPrice);

/// <summary>Fills in the prices of "price on request" lines and moves the order to Quoted.</summary>
public record QuoteOrderRequest(List<QuoteOrderLineRequest> Lines, string? StoreNote);
