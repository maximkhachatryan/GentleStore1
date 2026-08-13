namespace GentleStore.Domain.Enums;

public enum OrderStatus
{
    /// <summary>Placed, fully priced, waiting for the store to acknowledge it.</summary>
    New = 0,

    /// <summary>Contains at least one "price on request" item, so there is no total yet.</summary>
    AwaitingQuote = 1,

    /// <summary>The store filled in the missing prices; the customer can see the total.</summary>
    Quoted = 2,

    /// <summary>The store reached the customer and agreed on the order.</summary>
    Confirmed = 3,

    /// <summary>Waiting for pickup, or out for delivery.</summary>
    Ready = 4,

    Completed = 5,
    Cancelled = 6
}
