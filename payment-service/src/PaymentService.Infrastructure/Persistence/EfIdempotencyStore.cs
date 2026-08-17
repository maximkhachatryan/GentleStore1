using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using PaymentService.Application.Abstractions;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Configuration;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>
/// Idempotency backed by a unique index.
/// <para>
/// The insert is the lock: two concurrent duplicates both try to claim the key and Postgres lets
/// exactly one through, which is a stronger guarantee than "check, then write" gives and needs no
/// extra infrastructure. A cache would be faster and would also lose its contents on a restart,
/// which is precisely when a caller is retrying.
/// </para>
/// </summary>
public sealed class EfIdempotencyStore : IIdempotencyStore
{
    private const string UniqueViolation = "23505";

    private readonly PaymentsDbContext _db;
    private readonly IClock _clock;
    private readonly PaymentServiceOptions _options;

    public EfIdempotencyStore(PaymentsDbContext db, IClock clock, IOptions<PaymentServiceOptions> options)
    {
        _db = db;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<IdempotencyReservation> ReserveAsync(
        Guid merchantId, string key, string endpoint, string requestHash, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var record = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            MerchantId = merchantId,
            Key = key,
            Endpoint = endpoint,
            RequestHash = requestHash,
            CreatedAt = now,
            ExpiresAt = now.AddHours(_options.IdempotencyKeyLifetimeHours)
        };

        _db.IdempotencyRecords.Add(record);

        try
        {
            await _db.SaveChangesAsync(ct);
            return new IdempotencyReservation(IdempotencyOutcome.Proceed);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            _db.Entry(record).State = EntityState.Detached;
        }

        var existing = await _db.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.MerchantId == merchantId && r.Key == key, ct);

        // Swept between the failed insert and this read. Vanishingly rare; treating it as a fresh
        // request is the safe reading, because an expired key is one nothing is waiting on.
        if (existing is null) return new IdempotencyReservation(IdempotencyOutcome.Proceed);

        // Same key, different request. Replaying the old response would hide a caller's bug behind
        // a success, so it is surfaced instead.
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal) ||
            !string.Equals(existing.Endpoint, endpoint, StringComparison.Ordinal))
            return new IdempotencyReservation(IdempotencyOutcome.Conflict);

        return existing.ResponseStatusCode is { } statusCode
            ? new IdempotencyReservation(IdempotencyOutcome.Replay, statusCode, existing.ResponseBody)
            : new IdempotencyReservation(IdempotencyOutcome.InProgress);
    }

    public Task CompleteAsync(
        Guid merchantId, string key, int statusCode, string? responseBody, Guid? paymentId = null,
        CancellationToken ct = default) =>
        _db.IdempotencyRecords
            .Where(r => r.MerchantId == merchantId && r.Key == key)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.ResponseStatusCode, statusCode)
                .SetProperty(r => r.ResponseBody, responseBody)
                .SetProperty(r => r.PaymentId, paymentId), ct);

    public Task ReleaseAsync(Guid merchantId, string key, CancellationToken ct = default) =>
        _db.IdempotencyRecords
            .Where(r => r.MerchantId == merchantId && r.Key == key && r.ResponseStatusCode == null)
            .ExecuteDeleteAsync(ct);
}
