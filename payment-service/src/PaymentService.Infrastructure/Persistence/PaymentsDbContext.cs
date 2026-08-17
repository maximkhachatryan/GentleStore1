using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>
/// The payment service owns its own database. Nothing here joins to GentleStore's tables and
/// nothing there joins to these — <c>MerchantId</c>, <c>OrderId</c> and <c>CustomerId</c> are ids
/// this service records but never resolves, which is what lets the two deploy and migrate on their
/// own schedules.
/// </summary>
public class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options)
    {
    }

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<MerchantGatewayAccount> MerchantGatewayAccounts => Set<MerchantGatewayAccount>();
    public DbSet<GatewayEvent> GatewayEvents => Set<GatewayEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly ValueConverter<Dictionary<string, string>, string> MapConverter = new(
        map => JsonSerializer.Serialize(map, Json),
        text => JsonSerializer.Deserialize<Dictionary<string, string>>(text, Json) ?? new Dictionary<string, string>());

    // Without a comparer EF treats the dictionary as a reference and misses in-place edits, so a
    // metadata change would silently not be saved.
    private static readonly ValueComparer<Dictionary<string, string>> MapComparer = new(
        (a, b) => a != null && b != null && a.Count == b.Count && !a.Except(b).Any(),
        map => map.Aggregate(0, (hash, kv) => HashCode.Combine(hash, kv.Key.GetHashCode(), kv.Value.GetHashCode())),
        map => new Dictionary<string, string>(map));

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        mb.Entity<Payment>(e =>
        {
            e.Property(x => x.Reference).IsRequired().HasMaxLength(40);
            e.Property(x => x.OrderNumber).HasMaxLength(64);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.GatewayKey).IsRequired().HasMaxLength(64);
            e.Property(x => x.GatewayPaymentId).HasMaxLength(200);
            e.Property(x => x.GatewayStatusCode).HasMaxLength(100);
            e.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            e.Property(x => x.CustomerName).HasMaxLength(150);
            e.Property(x => x.CustomerEmail).HasMaxLength(256);
            e.Property(x => x.CustomerPhone).HasMaxLength(40);
            e.Property(x => x.MethodBrand).HasMaxLength(64);
            e.Property(x => x.MethodLast4).HasMaxLength(4);
            e.Property(x => x.ReturnUrl).HasMaxLength(1000);
            e.Property(x => x.CancelUrl).HasMaxLength(1000);
            e.Property(x => x.CallbackUrl).HasMaxLength(1000);
            e.Property(x => x.FailureCode).HasMaxLength(100);
            e.Property(x => x.FailureMessage).HasMaxLength(1000);
            e.Property(x => x.IdempotencyKey).HasMaxLength(128);

            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.CaptureMode).HasConversion<int>();
            e.Property(x => x.MethodKind).HasConversion<int>();

            e.Property(x => x.Metadata)
                .HasConversion(MapConverter, MapComparer)
                .HasColumnType("jsonb")
                .IsRequired();

            // Postgres' own row version. Two webhooks for one payment arriving together is normal;
            // this makes the loser retry against fresh state instead of overwriting the winner.
            e.Property(x => x.Version).IsRowVersion();

            e.HasOne(x => x.GatewayAccount).WithMany()
                .HasForeignKey(x => x.GatewayAccountId).OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => new { x.MerchantId, x.CreatedAt });
            e.HasIndex(x => new { x.MerchantId, x.Status });
            e.HasIndex(x => new { x.MerchantId, x.OrderId });
            e.HasIndex(x => new { x.MerchantId, x.CustomerId });

            // How a webhook finds its payment, so it must be indexed and unique per gateway.
            // Filtered because the id is null until the gateway has accepted the payment.
            e.HasIndex(x => new { x.GatewayKey, x.GatewayPaymentId })
                .IsUnique()
                .HasFilter("\"GatewayPaymentId\" IS NOT NULL");

            // The idempotency table is the enforcement point; this is here so a replay can be
            // resolved to its payment without a second lookup.
            e.HasIndex(x => new { x.MerchantId, x.IdempotencyKey })
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");

            // Drives the reconciler: find everything still in flight and past its window.
            e.HasIndex(x => new { x.Status, x.ExpiresAt });
        });

        mb.Entity<PaymentAttempt>(e =>
        {
            e.Property(x => x.Operation).HasConversion<int>();
            e.Property(x => x.Outcome).HasConversion<int>();
            e.Property(x => x.GatewayReference).HasMaxLength(200);
            e.Property(x => x.GatewayIdempotencyKey).HasMaxLength(128);
            e.Property(x => x.ErrorCode).HasMaxLength(100);
            e.Property(x => x.ErrorMessage).HasMaxLength(2000);
            e.Property(x => x.ResponseSnapshot).HasMaxLength(8000);

            e.HasOne(x => x.Payment).WithMany(x => x.Attempts)
                .HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.PaymentId, x.StartedAt });

            // Finds attempts left hanging by a crash — the ones that may have taken money.
            e.HasIndex(x => new { x.Outcome, x.StartedAt });
        });

        mb.Entity<Refund>(e =>
        {
            e.Property(x => x.Reference).IsRequired().HasMaxLength(40);
            e.Property(x => x.Currency).IsRequired().HasMaxLength(3);
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.GatewayRefundId).HasMaxLength(200);
            e.Property(x => x.GatewayStatusCode).HasMaxLength(100);
            e.Property(x => x.FailureCode).HasMaxLength(100);
            e.Property(x => x.FailureMessage).HasMaxLength(1000);
            e.Property(x => x.IdempotencyKey).HasMaxLength(128);
            e.Property(x => x.Version).IsRowVersion();

            e.HasOne(x => x.Payment).WithMany(x => x.Refunds)
                .HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => x.PaymentId);
            e.HasIndex(x => x.GatewayRefundId).HasFilter("\"GatewayRefundId\" IS NOT NULL");
        });

        mb.Entity<MerchantGatewayAccount>(e =>
        {
            e.Property(x => x.GatewayKey).IsRequired().HasMaxLength(64);
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(150);
            e.Property(x => x.CredentialRef).IsRequired().HasMaxLength(200);
            e.Property(x => x.WebhookSecretRef).HasMaxLength(200);
            e.Property(x => x.Mode).HasConversion<int>();
            e.Property(x => x.SupportedCurrencies).IsRequired();

            // One enrolment per gateway per mode, which is what makes the upsert well defined.
            e.HasIndex(x => new { x.MerchantId, x.GatewayKey, x.Mode }).IsUnique();

            // Webhook routing: every enabled account for a gateway is a verification candidate.
            e.HasIndex(x => new { x.GatewayKey, x.IsEnabled });
        });

        mb.Entity<GatewayEvent>(e =>
        {
            e.Property(x => x.GatewayKey).IsRequired().HasMaxLength(64);
            e.Property(x => x.ExternalEventId).IsRequired().HasMaxLength(200);
            e.Property(x => x.EventType).IsRequired().HasMaxLength(150);
            e.Property(x => x.GatewayPaymentId).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.LastError).HasMaxLength(2000);
            e.Property(x => x.Payload).IsRequired();

            e.Property(x => x.Headers)
                .HasConversion(MapConverter, MapComparer)
                .HasColumnType("jsonb")
                .IsRequired();

            // The deduplication guarantee. A gateway retrying a delivery collides here and the
            // second copy is ignored rather than applied twice.
            e.HasIndex(x => new { x.GatewayKey, x.ExternalEventId }).IsUnique();
            e.HasIndex(x => new { x.Status, x.ReceivedAt });
            e.HasIndex(x => x.PaymentId);
        });

        mb.Entity<OutboxMessage>(e =>
        {
            e.Property(x => x.EventType).IsRequired().HasMaxLength(100);
            e.Property(x => x.Payload).IsRequired();
            e.Property(x => x.CallbackUrl).IsRequired().HasMaxLength(1000);
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.LastError).HasMaxLength(2000);

            // The dispatcher's only query: pending rows whose backoff has elapsed.
            e.HasIndex(x => new { x.Status, x.NextAttemptAt });
            e.HasIndex(x => x.PaymentId);
        });

        mb.Entity<IdempotencyRecord>(e =>
        {
            e.Property(x => x.Key).IsRequired().HasMaxLength(128);
            e.Property(x => x.Endpoint).IsRequired().HasMaxLength(200);
            e.Property(x => x.RequestHash).IsRequired().HasMaxLength(64);

            // Unique because that is the mechanism, not just a constraint: two concurrent
            // duplicates race to insert and exactly one wins.
            e.HasIndex(x => new { x.MerchantId, x.Key }).IsUnique();
            e.HasIndex(x => x.ExpiresAt);
        });
    }
}
