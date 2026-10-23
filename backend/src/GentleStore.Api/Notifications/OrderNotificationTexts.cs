using System.Globalization;
using System.Net;
using System.Text;
using GentleStore.Domain.Entities;
using GentleStore.Domain.Enums;

namespace GentleStore.Api.Notifications;

/// <summary>Telegram (HTML parse mode) messages for staff, in the language each user linked with.</summary>
public static class OrderNotificationTexts
{
    public const string DefaultLanguage = "en";

    public static readonly string[] Languages = ["en", "hy", "ru"];

    // Telegram rejects messages over 4096 characters; an order may have up to 50 lines.
    private const int MaxListedLines = 15;

    private sealed record Texts(
        string Placed, string Paid, string AwaitingPayment, string AwaitingQuote, string ToQuote, string More,
        string Total, string Pickup, string Delivery, string Customer, string Note, string Open,
        string Linked, string Unlinked, string LinkInvalid);

    private static readonly Dictionary<string, Texts> ByLanguage = new()
    {
        ["en"] = new(
            Placed: "New order {0}",
            Paid: "Payment received for order {0}",
            AwaitingPayment: "Awaiting payment",
            AwaitingQuote: "Awaiting quote: set the prices in the console",
            ToQuote: "to quote",
            More: "…and {0} more",
            Total: "Total",
            Pickup: "Pick up",
            Delivery: "Delivery",
            Customer: "Customer",
            Note: "Customer note",
            Open: "Open in console",
            Linked: "Telegram is connected. New orders and payments will be sent to this chat.",
            Unlinked: "Notifications are turned off for this chat. You can reconnect from the console at any time.",
            LinkInvalid: "This link is invalid or has expired. Open the console and press \"Connect Telegram\" again."),
        ["hy"] = new(
            Placed: "Նոր պատվեր՝ {0}",
            Paid: "Վճարումը ստացվել է՝ պատվեր {0}",
            AwaitingPayment: "Սպասում է վճարման",
            AwaitingQuote: "Սպասում է գնահատման․ նշեք գները վահանակում",
            ToQuote: "ենթակա է գնահատման",
            More: "…և ևս {0} ապրանք",
            Total: "Ընդամենը",
            Pickup: "Վերցնել տեղում",
            Delivery: "Առաքում",
            Customer: "Հաճախորդ",
            Note: "Հաճախորդի նշումը",
            Open: "Բացել վահանակում",
            Linked: "Telegram-ը միացված է։ Նոր պատվերներն ու վճարումները կուղարկվեն այս զրույցին։",
            Unlinked: "Ծանուցումներն այս զրույցի համար անջատված են։ Կարող եք կրկին միացնել վահանակից։",
            LinkInvalid: "Հղումն անվավեր է կամ ժամկետանց։ Բացեք վահանակը և կրկին սեղմեք «Միացնել Telegram-ը»։"),
        ["ru"] = new(
            Placed: "Новый заказ {0}",
            Paid: "Получена оплата заказа {0}",
            AwaitingPayment: "Ожидает оплаты",
            AwaitingQuote: "Ожидает расчёта: укажите цены в консоли",
            ToQuote: "по запросу",
            More: "…и ещё позиций: {0}",
            Total: "Итого",
            Pickup: "Самовывоз",
            Delivery: "Доставка",
            Customer: "Покупатель",
            Note: "Комментарий покупателя",
            Open: "Открыть в консоли",
            Linked: "Telegram подключён. Новые заказы и оплаты будут приходить в этот чат.",
            Unlinked: "Уведомления для этого чата отключены. Подключить снова можно в консоли.",
            LinkInvalid: "Ссылка недействительна или устарела. Откройте консоль и снова нажмите «Подключить Telegram».")
    };

    public static string Normalize(string? language)
    {
        var code = language?.Split('-', '_')[0].ToLowerInvariant();
        return code is not null && ByLanguage.ContainsKey(code) ? code : DefaultLanguage;
    }

    public static string Linked(string? language) => For(language).Linked;
    public static string Unlinked(string? language) => For(language).Unlinked;

    /// <summary>Nobody is identified yet, so every language at once.</summary>
    public static string LinkInvalid() => string.Join("\n\n", Languages.Select(l => ByLanguage[l].LinkInvalid));

    public static string Render(OrderNotificationEvent ev, Order order, string? language, string consoleUrl)
    {
        var t = For(language);
        var sb = new StringBuilder();

        var title = ev == OrderNotificationEvent.OrderPaid ? t.Paid : t.Placed;
        sb.Append("<b>").Append(E(string.Format(title, order.OrderNumber))).Append("</b>\n");

        if (ev == OrderNotificationEvent.OrderPlaced)
        {
            if (order.Status == OrderStatus.AwaitingPayment) sb.Append(E(t.AwaitingPayment)).Append('\n');
            else if (order.Status == OrderStatus.AwaitingQuote) sb.Append(E(t.AwaitingQuote)).Append('\n');

            sb.Append('\n');
            var lines = order.Lines.OrderBy(l => l.DisplayOrder).ToList();
            foreach (var line in lines.Take(MaxListedLines))
            {
                sb.Append(line.Quantity).Append(" × ").Append(E(Clip(line.ProductName, 80)));
                if (!string.IsNullOrWhiteSpace(line.VariantLabel)) sb.Append(" (").Append(E(Clip(line.VariantLabel, 80))).Append(')');
                sb.Append(" — ").Append(line.LineTotal is { } lt ? E(Money(lt, order.Currency)) : E(t.ToQuote)).Append('\n');
            }
            if (lines.Count > MaxListedLines)
                sb.Append(E(string.Format(t.More, lines.Count - MaxListedLines))).Append('\n');
        }

        sb.Append('\n');
        sb.Append(E(t.Total)).Append(": <b>")
            .Append(order.Total is { } total ? E(Money(total, order.Currency)) : E(t.ToQuote)).Append("</b>\n");

        if (ev == OrderNotificationEvent.OrderPlaced)
        {
            sb.Append(order.Fulfilment == FulfilmentMethod.Delivery
                ? $"{E(t.Delivery)}: {E(order.DeliveryAddress ?? "")}"
                : E(t.Pickup)).Append('\n');
        }

        sb.Append(E(t.Customer)).Append(": ").Append(E(order.ContactName)).Append(", ").Append(E(order.ContactPhone)).Append('\n');

        if (ev == OrderNotificationEvent.OrderPlaced && !string.IsNullOrWhiteSpace(order.CustomerNote))
            sb.Append(E(t.Note)).Append(": ").Append(E(Clip(order.CustomerNote, 500))).Append('\n');

        var link = $"{consoleUrl.TrimEnd('/')}/store/orders?order={order.Id}";
        sb.Append('\n').Append("<a href=\"").Append(E(link)).Append("\">").Append(E(t.Open)).Append("</a>");

        return sb.ToString();
    }

    private static Texts For(string? language) => ByLanguage[Normalize(language)];

    private static string Money(decimal amount, string currency) =>
        $"{amount.ToString("#,0.##", CultureInfo.InvariantCulture)} {currency}";

    private static string E(string value) => WebUtility.HtmlEncode(value);

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}
