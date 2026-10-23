using GentleStore.Domain.Entities;
using GentleStore.Domain.Enums;
using GentleStore.Infrastructure.Persistence;
using GentleStore.Infrastructure.Telegram;
using Microsoft.EntityFrameworkCore;

namespace GentleStore.Api.Notifications;

public interface IOrderNotifier
{
    /// <summary>
    /// Queues the event for every linked staff member of the order's store. Only adds rows — the
    /// caller's own SaveChanges commits them with the order change, so they stand or fall together.
    /// </summary>
    Task EnqueueAsync(Order order, OrderNotificationEvent ev, CancellationToken ct = default);
}

public sealed class OrderNotifier : IOrderNotifier
{
    private readonly AppDbContext _db;
    private readonly ITelegramClient _telegram;

    public OrderNotifier(AppDbContext db, ITelegramClient telegram)
    {
        _db = db;
        _telegram = telegram;
    }

    public async Task EnqueueAsync(Order order, OrderNotificationEvent ev, CancellationToken ct = default)
    {
        if (!_telegram.IsConfigured) return;

        var recipients = await _db.Users
            .Where(u => u.StoreId == order.StoreId && u.IsActive && u.TelegramChatId != null)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        _db.OrderNotifications.AddRange(recipients.Select(userId => new OrderNotification
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            UserId = userId,
            Event = ev,
            Status = OrderNotificationStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now
        }));
    }
}
