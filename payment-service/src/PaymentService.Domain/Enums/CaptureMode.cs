namespace PaymentService.Domain.Enums;

public enum CaptureMode
{
    /// <summary>Authorize and capture in one step. The right default for a shop that ships promptly.</summary>
    Automatic = 0,

    /// <summary>
    /// Hold the funds and let the merchant capture later. GentleStore orders are requests rather
    /// than reservations, so a store that confirms stock by hand wants this: authorize at checkout,
    /// capture when the order is actually confirmed.
    /// </summary>
    Manual = 1
}
