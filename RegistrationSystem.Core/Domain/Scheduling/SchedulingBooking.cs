namespace RegistrationSystem.Core.Domain.Scheduling;

/// <summary>
/// Records a participant's booking of a specific time slot within a scheduling session.
/// One document per booking attempt; cancelled bookings are retained for audit trail.
///
/// This is the authoritative source for slot occupancy. When a participant books:
///   1. A SchedulingBooking is inserted here (Status = Active).
///   2. The matching RoundEntry in CompetitionProgress is updated:
///      Status → Scheduled, ScheduledDateTime set, ScheduledSection set.
///
/// When a participant cancels:
///   1. This booking's Status is set to Cancelled.
///   2. The RoundEntry is reverted: Status → Active, ScheduledDateTime/Section cleared.
///
/// Slot availability = Slot.Capacity − Count(Active bookings for that sessionId + slotId).
/// </summary>
public class SchedulingBooking
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Which scheduling session this booking belongs to.</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Which specific slot (date + time) within the session was booked.</summary>
    public string SlotId { get; set; } = string.Empty;

    public string RegistrationId { get; set; } = string.Empty;
    public string? Cid { get; set; }
    public string CompetitorName { get; set; } = string.Empty;

    /// <summary>Slot date — for display and querying without loading the session.</summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Slot start time in UTC — denormalized from the slot for display.
    /// Always show to participants in Central Time.
    /// </summary>
    public TimeOnly TimeUtc { get; set; }

    /// <summary>
    /// Section label from the session (e.g. "A", "B").
    /// Null if the session has no parallel sections.
    /// Stored here and mirrored to RoundEntry.ScheduledSection for display in the competition tracker.
    /// </summary>
    public string? SectionLabel { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Active;

    public DateTimeOffset BookedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

public enum BookingStatus
{
    Active = 0,
    Cancelled = 1
}
