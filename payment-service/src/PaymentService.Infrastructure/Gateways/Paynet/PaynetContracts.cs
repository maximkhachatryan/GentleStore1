using System.Text.Json;
using System.Text.Json.Serialization;

namespace PaymentService.Infrastructure.Gateways.Paynet;

// Paynet's wire shapes. Internal on purpose: nothing outside this folder should know Paynet's
// vocabulary, which is the whole point of the gateway port.

internal sealed class PaynetCreatePaymentBody
{
    [JsonPropertyName("amount")] public long Amount { get; set; }
    [JsonPropertyName("currency")] public string Currency { get; set; } = PaynetAmounts.Currency;
    [JsonPropertyName("order_id")] public string OrderId { get; set; } = string.Empty;
    [JsonPropertyName("processor")] public string Processor { get; set; } = string.Empty;
    [JsonPropertyName("return_url")] public string ReturnUrl { get; set; } = string.Empty;

    [JsonPropertyName("cancel_url"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CancelUrl { get; set; }

    [JsonPropertyName("callback_url"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CallbackUrl { get; set; }

    [JsonPropertyName("description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("customer_email"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CustomerEmail { get; set; }

    [JsonPropertyName("customer_name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CustomerName { get; set; }

    /// <summary>A verified domain. Required in practice for server-to-server calls, which carry no Origin header.</summary>
    [JsonPropertyName("domain"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Domain { get; set; }

    [JsonPropertyName("redirect_mode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RedirectMode { get; set; }

    [JsonPropertyName("language"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; set; }

    // `metadata` and `items` are deliberately never sent. The published schema types metadata as an
    // array of strings while describing it as key-value pairs, so the accepted shape is ambiguous
    // and guessing wrong is a 422 on every payment. We keep metadata on our own record and echo it
    // in our own callbacks, which is where callers read it from anyway.
}

internal sealed class PaynetRefundBody
{
    [JsonPropertyName("amount"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Amount { get; set; }

    [JsonPropertyName("reason"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; set; }
}

internal sealed class PaynetPayment
{
    [JsonPropertyName("uuid")] public string? Uuid { get; set; }
    [JsonPropertyName("order_id")] public string? OrderId { get; set; }

    // Documented as a string on the payment resource and as a number elsewhere, so both are accepted.
    [JsonPropertyName("amount"), JsonConverter(typeof(FlexibleLongConverter))]
    public long Amount { get; set; }

    [JsonPropertyName("refunded_amount"), JsonConverter(typeof(FlexibleLongConverter))]
    public long RefundedAmount { get; set; }

    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("processor")] public string? Processor { get; set; }

    /// <summary>Paynet's own checkout page. The default flow.</summary>
    [JsonPropertyName("checkout_url")] public string? CheckoutUrl { get; set; }

    /// <summary>Straight to the bank, returned when <c>redirect_mode</c> is <c>direct</c>.</summary>
    [JsonPropertyName("redirect_url")] public string? RedirectUrl { get; set; }

    /// <summary><c>qr</c> when the customer scans rather than being redirected.</summary>
    [JsonPropertyName("payment_flow")] public string? PaymentFlow { get; set; }

    [JsonPropertyName("qr_code")] public string? QrCode { get; set; }

    /// <summary>Retrieval reference number — the code a bank's support desk asks for.</summary>
    [JsonPropertyName("rrn")] public string? Rrn { get; set; }

    [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }
    [JsonPropertyName("completed_at")] public string? CompletedAt { get; set; }
    [JsonPropertyName("refunded_at")] public string? RefundedAt { get; set; }
}

internal sealed class PaynetRefund
{
    [JsonPropertyName("uuid")] public string? Uuid { get; set; }
    [JsonPropertyName("payment_uuid")] public string? PaymentUuid { get; set; }

    [JsonPropertyName("amount"), JsonConverter(typeof(FlexibleLongConverter))]
    public long Amount { get; set; }

    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    [JsonPropertyName("created_at")] public string? CreatedAt { get; set; }
    [JsonPropertyName("completed_at")] public string? CompletedAt { get; set; }
}

/// <summary>Paynet's 422 body: an overview plus per-field messages.</summary>
internal sealed class PaynetError
{
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("errors")] public Dictionary<string, string[]>? Errors { get; set; }

    /// <summary>Flattens the field errors into something a merchant can act on.</summary>
    public string Describe()
    {
        var detail = Errors is { Count: > 0 }
            ? string.Join("; ", Errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}"))
            : null;

        return string.Join(" ", new[] { Message, detail }.Where(s => !string.IsNullOrWhiteSpace(s)))
            is { Length: > 0 } combined
            ? combined
            : Error ?? "Paynet rejected the request.";
    }
}

internal sealed class PaynetWebhookEnvelope
{
    [JsonPropertyName("event")] public string? Event { get; set; }
    [JsonPropertyName("transaction")] public PaynetWebhookTransaction? Transaction { get; set; }
}

internal sealed class PaynetWebhookTransaction
{
    [JsonPropertyName("uuid")] public string? Uuid { get; set; }
    [JsonPropertyName("order_id")] public string? OrderId { get; set; }

    [JsonPropertyName("amount"), JsonConverter(typeof(FlexibleLongConverter))]
    public long Amount { get; set; }

    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("processor")] public string? Processor { get; set; }
}

/// <summary>
/// Accepts an amount as either a JSON number or a JSON string. Paynet's own schema uses both for
/// the same field depending on the endpoint, and a payment integration should not fall over on
/// that.
/// </summary>
internal sealed class FlexibleLongConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.TryGetInt64(out var number) ? number : (long)reader.GetDouble(),
            JsonTokenType.String => long.TryParse(reader.GetString(), out var parsed) ? parsed : 0,
            JsonTokenType.Null => 0,
            _ => 0
        };

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}
