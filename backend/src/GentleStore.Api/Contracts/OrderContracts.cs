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
/// <param name="AwaitingPayment">The customer still owes for this order and can pay it now.</param>
/// <param name="PaymentStatus">Mirrored from the payment service; null before any attempt.</param>
public record PublicOrderDto(
    Guid Id, string OrderNumber, string Status, string Currency, decimal? Total,
    string Fulfilment, string? DeliveryAddress, string? Note,
    string ContactName, string ContactPhoneMasked, bool AwaitingQuote,
    bool PaymentRequired, bool AwaitingPayment, string? PaymentStatus, DateTime? PaidAt,
    DateTime PlacedAt, IReadOnlyList<PublicOrderLineDto> Lines);

// ---- Paying for an order ----

/// <param name="Method">A gateway key from <c>GET /payment-methods</c>. Omit to let the store's own order of preference decide.</param>
public record StartPaymentRequest(string? Method);

public record PublicPaymentMethodDto(string Key, string DisplayName);

/// <param name="Kind"><c>Redirect</c>, <c>Display</c>, <c>ClientSdk</c> or <c>None</c>.</param>
public record PaymentActionDto(string Kind, string? Url, DateTime? ExpiresAt);

/// <summary>
/// The order as it now stands, plus where to send the customer. <c>Action</c> is null when there is
/// nothing left to do — most usefully when the order turned out to be paid already.
/// </summary>
public record OrderPaymentDto(PublicOrderDto Order, PaymentActionDto? Action);

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
    bool PaymentRequired, string? PaymentStatus, DateTime? PaidAt,
    int CustomerOrderCount, DateTime PlacedAt);

public record OrderDetailDto(
    Guid Id, string OrderNumber, string Status, string IdentityTier,
    Guid CustomerId, string ContactName, string ContactPhone, string ContactPhoneNormalized,
    string CustomerOrigin, int CustomerOrderCount,
    string Fulfilment, string? DeliveryAddress, string? CustomerNote, string? StoreNote,
    string Currency, decimal? Total,
    bool PaymentRequired, string? PaymentReference, string? PaymentStatus, DateTime? PaidAt,
    DateTime PlacedAt, DateTime UpdatedAt,
    IReadOnlyList<OrderLineDto> Lines);

public record UpdateOrderStatusRequest(string Status, string? StoreNote);

public record QuoteOrderLineRequest(Guid LineId, decimal UnitPrice);

/// <summary>Fills in the prices of "price on request" lines and moves the order to Quoted.</summary>
public record QuoteOrderRequest(List<QuoteOrderLineRequest> Lines, string? StoreNote);

public record OrderPulseItemDto(Guid Id, string OrderNumber, DateTime At);

/// <summary>What the console polls to notice new and newly paid orders without fetching the list.</summary>
/// <param name="PendingCount">Orders waiting on the store: New and AwaitingQuote.</param>
public record OrderPulseDto(int PendingCount, OrderPulseItemDto? LatestPlaced, OrderPulseItemDto? LatestPaid);
