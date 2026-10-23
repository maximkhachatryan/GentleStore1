using GentleStore.Domain.Enums;

namespace GentleStore.Domain.Entities;

/// <summary>
/// Outbox row: one event for one staff member. Written in the same transaction as the order change
/// it reports, and one row per recipient so a retry never repeats a message someone already got.
/// </summary>
public class OrderNotification
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public OrderNotificationEvent Event { get; set; }
    public OrderNotificationStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }

    public Order? Order { get; set; }
    public User? User { get; set; }
}
