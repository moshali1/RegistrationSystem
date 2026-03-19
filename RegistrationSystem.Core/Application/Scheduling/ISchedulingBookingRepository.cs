using RegistrationSystem.Core.Domain.Scheduling;

namespace RegistrationSystem.Core.Application.Scheduling;

public interface ISchedulingBookingRepository
{
    Task<SchedulingBooking?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Returns the active (non-cancelled) booking for a registration in a specific session, or null.</summary>
    Task<SchedulingBooking?> GetActiveByRegistrationAndSessionAsync(
        string registrationId, string sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchedulingBooking>> GetBySessionAsync(
        string sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchedulingBooking>> GetByRegistrationIdAsync(
        string registrationId, CancellationToken cancellationToken = default);

    /// <summary>Count of active bookings for a specific slot (used for availability checks).</summary>
    Task<int> CountActiveBySlotAsync(
        string sessionId, string slotId, CancellationToken cancellationToken = default);

    /// <summary>Counts of active bookings per slotId for a session, for bulk availability display.</summary>
    Task<Dictionary<string, int>> GetSlotBookingCountsAsync(
        string sessionId, CancellationToken cancellationToken = default);

    Task SaveAsync(SchedulingBooking booking, CancellationToken cancellationToken = default);

    Task CancelAsync(string id, string? reason, CancellationToken cancellationToken = default);

    /// <summary>Cancels all active bookings for a registration (used when a registration is deleted).</summary>
    Task CancelAllByRegistrationIdAsync(string registrationId, string? reason, CancellationToken cancellationToken = default);

    /// <summary>Returns all booking records across all sessions (used for data integrity checks).</summary>
    Task<IReadOnlyList<SchedulingBooking>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Hard-deletes all booking records (any status) for a registration. Used for cleanup of orphaned records.</summary>
    Task DeleteAllByRegistrationIdAsync(string registrationId, CancellationToken cancellationToken = default);
}
