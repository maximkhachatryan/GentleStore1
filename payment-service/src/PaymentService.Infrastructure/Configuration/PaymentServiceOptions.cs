namespace PaymentService.Infrastructure.Configuration;

public sealed class PaymentServiceOptions
{
    public const string SectionName = "Payments";

    /// <summary>
    /// How long an <c>Idempotency-Key</c> is remembered. Long enough to cover any retry a caller
    /// or a proxy will make, short enough that the table stays small.
    /// </summary>
    public int IdempotencyKeyLifetimeHours { get; set; } = 24;

    public OutboxOptions Outbox { get; set; } = new();
    public ReconcilerOptions Reconciler { get; set; } = new();
    public CallbackOptions Callbacks { get; set; } = new();
}

public sealed class CallbackOptions
{
    /// <summary>
    /// Signs the callback body with HMAC-SHA256 when set. Unset in development, where the
    /// signature would just be ceremony between two containers on one machine.
    /// <para>
    /// Worth setting in production even though the call is internal: a callback URL is a plain
    /// POST endpoint on GentleStore that says "this order is paid", and the signature is what
    /// stops anything else on the network from saying it.
    /// </para>
    /// </summary>
    public string? SigningSecret { get; set; }

    public string SignatureHeader { get; set; } = "X-Payment-Signature";
}

public sealed class OutboxOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>How often the dispatcher looks for work.</summary>
    public int PollSeconds { get; set; } = 5;

    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// After this many failures the message is abandoned rather than retried forever. It is left
    /// in the table on purpose — a caller that never heard about a captured payment is something
    /// someone has to see.
    /// </summary>
    public int MaxAttempts { get; set; } = 12;

    /// <summary>Doubled per attempt and capped by <see cref="MaxBackoffSeconds"/>.</summary>
    public int BaseBackoffSeconds { get; set; } = 10;

    public int MaxBackoffSeconds { get; set; } = 3600;

    public int TimeoutSeconds { get; set; } = 10;
}

public sealed class ReconcilerOptions
{
    public bool Enabled { get; set; } = true;

    public int PollSeconds { get; set; } = 60;

    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// A payment still in flight after this long is polled rather than waited on. Covers the
    /// webhook that never arrived and the create call whose answer was lost.
    /// </summary>
    public int StalePaymentMinutes { get; set; } = 15;

    /// <summary>
    /// After this long an unfinished payment with no gateway record is written off as abandoned,
    /// so the merchant's list is not permanently full of checkouts nobody completed.
    /// </summary>
    public int AbandonAfterHours { get; set; } = 24;
}
