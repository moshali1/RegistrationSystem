using RegistrationSystem.Core.Application.CompetitionRounds;
using RegistrationSystem.Core.Application.Settings;
using RegistrationSystem.Core.Domain.CompetitionRounds;
using RegistrationSystem.Core.Domain.Registrations;
using RegistrationSystem.Core.Domain.Scheduling;
using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Core.Application.Scheduling;

/// <summary>
/// Handles scheduling session lookups, slot availability, booking, and cancellation.
///
/// Booking flow:
///   1. Participant is on the Messages page and sees their Active round entry linked to a session.
///   2. They navigate to the scheduling page and select a session + slot.
///   3. BookSlotAsync verifies eligibility, checks availability, inserts the booking, and
///      updates CompetitionProgress: RoundEntry.Status → Scheduled, ScheduledDateTime set,
///      ScheduledSection set (if session has sections).
///
/// Cancellation flow (cancel-first rescheduling):
///   1. CancelBookingAsync marks the booking Cancelled and reverts the RoundEntry to Active,
///      clearing ScheduledDateTime and ScheduledSection.
///   2. Participant then books a new slot from scratch.
/// </summary>
public class SchedulingService
{
    private readonly ISchedulingBookingRepository _bookingRepository;
    private readonly ICompetitionProgressRepository _progressRepository;
    private readonly SettingsService _settingsService;

    private static readonly TimeZoneInfo CentralTime =
        TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

    public SchedulingService(
        ISchedulingBookingRepository bookingRepository,
        ICompetitionProgressRepository progressRepository,
        SettingsService settingsService)
    {
        _bookingRepository = bookingRepository;
        _progressRepository = progressRepository;
        _settingsService = settingsService;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SESSION ELIGIBILITY
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns all sessions (from the given settings) that the registration is eligible to book into,
    /// filtered by gender, state, category, and session open status.
    /// </summary>
    public IReadOnlyList<SchedulingSession> GetEligibleSessions(
        Registration registration,
        IEnumerable<SchedulingSession> allSessions)
    {
        return allSessions.Where(s => IsEligible(registration, s)).ToList();
    }

    /// <summary>
    /// Returns sessions eligible for the registration that are linked to the given round name
    /// and are currently open for booking.
    /// </summary>
    public IReadOnlyList<SchedulingSession> GetOpenSessionsForRound(
        Registration registration,
        IEnumerable<SchedulingSession> allSessions,
        string roundName)
    {
        return allSessions
            .Where(s => s.IsOpen &&
                        s.LinkedRoundName != null &&
                        s.LinkedRoundName.Equals(roundName, StringComparison.OrdinalIgnoreCase) &&
                        IsEligible(registration, s))
            .ToList();
    }

    public bool IsEligible(Registration registration, SchedulingSession session)
    {
        // Gender filter
        if (session.GenderFilter != SessionGenderFilter.All)
        {
            var isMale = registration.PersonalInfo.Gender == Gender.Male;
            if (session.GenderFilter == SessionGenderFilter.MaleOnly && !isMale) return false;
            if (session.GenderFilter == SessionGenderFilter.FemaleOnly && isMale) return false;
        }

        // Geographic filter (MN = Minnesota state code)
        if (session.GeographicFilter != SessionGeographicFilter.All)
        {
            var isMn = registration.AddressInfo.StateProvince != null &&
                       registration.AddressInfo.StateProvince.Equals("MN", StringComparison.OrdinalIgnoreCase);
            if (session.GeographicFilter == SessionGeographicFilter.MinnesotaOnly && !isMn) return false;
            if (session.GeographicFilter == SessionGeographicFilter.OutsideMinnesota && isMn) return false;
        }

        // Category filter
        if (session.AllowedCategoryIds.Count > 0 &&
            !session.AllowedCategoryIds.Contains(registration.CompetitionSelection.CategoryId))
            return false;

        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SLOT AVAILABILITY
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns a dictionary of slotId → bookedCount for a session,
    /// used to compute remaining capacity for display.
    /// </summary>
    public Task<Dictionary<string, int>> GetSlotBookingCountsAsync(
        string sessionId, CancellationToken cancellationToken = default)
        => _bookingRepository.GetSlotBookingCountsAsync(sessionId, cancellationToken);

    /// <summary>
    /// Returns all bookings (active and cancelled) for a registration, sorted newest first.
    /// Used by participant-facing pages to detect existing bookings.
    /// </summary>
    public Task<IReadOnlyList<SchedulingBooking>> GetBookingsByRegistrationIdAsync(
        string registrationId, CancellationToken cancellationToken = default)
        => _bookingRepository.GetByRegistrationIdAsync(registrationId, cancellationToken);

    /// <summary>
    /// Returns all bookings (active and cancelled) for a session.
    /// Used by admin booking management view.
    /// </summary>
    public Task<IReadOnlyList<SchedulingBooking>> GetBookingsBySessionAsync(
        string sessionId, CancellationToken cancellationToken = default)
        => _bookingRepository.GetBySessionAsync(sessionId, cancellationToken);

    public Task<IReadOnlyList<SchedulingBooking>> GetAllBookingsAsync(
        CancellationToken cancellationToken = default)
        => _bookingRepository.GetAllAsync(cancellationToken);

    /// <summary>
    /// Cancels all active bookings for a registration (called when a registration is deleted).
    /// Does NOT revert CompetitionProgress — the progress record is deleted separately.
    /// </summary>
    public Task CancelAllByRegistrationIdAsync(
        string registrationId, string? reason = "Registration deleted",
        CancellationToken cancellationToken = default)
        => _bookingRepository.CancelAllByRegistrationIdAsync(registrationId, reason, cancellationToken);

    /// <summary>
    /// Admin cancel: cancels an active booking and reverts the competitor's RoundEntry to Active.
    /// </summary>
    public Task<bool> AdminCancelBookingAsync(
        string bookingId,
        string? reason,
        IEnumerable<SchedulingSession> allSessions,
        CancellationToken cancellationToken = default)
        => CancelBookingAsync(bookingId, reason, allSessions, cancellationToken);

    /// <summary>
    /// Returns a slot's remaining capacity: Slot.Capacity − active bookings.
    /// Used for real-time availability checks before booking.
    /// </summary>
    public async Task<int> GetRemainingCapacityAsync(
        string sessionId, string slotId, int slotCapacity,
        CancellationToken cancellationToken = default)
    {
        var booked = await _bookingRepository.CountActiveBySlotAsync(sessionId, slotId, cancellationToken);
        return Math.Max(0, slotCapacity - booked);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BOOKING
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Books a slot for a participant. Verifies eligibility and availability,
    /// inserts the booking, then updates the matching RoundEntry in CompetitionProgress.
    ///
    /// Returns a result indicating success or the reason for failure.
    /// </summary>
    public async Task<BookingResult> BookSlotAsync(
        SchedulingSession session,
        SchedulingSlot slot,
        Registration registration,
        IEnumerable<SchedulingSession> allSessions,
        CancellationToken cancellationToken = default)
    {
        // Eligibility check
        if (!IsEligible(registration, session))
            return BookingResult.Fail("You are not eligible to book this session.");

        if (!session.IsOpen)
            return BookingResult.Fail("Scheduling for this session is not currently open.");

        if (!slot.IsActive)
            return BookingResult.Fail("This slot is not available.");

        // Already booked in this group (or session)?
        // Load all active bookings for this registration and check against sessions in the same group.
        var existingBookings = await _bookingRepository.GetByRegistrationIdAsync(registration.Id, cancellationToken);
        var activeBookings = existingBookings.Where(b => b.Status == BookingStatus.Active).ToList();
        if (activeBookings.Count > 0)
        {
            var groupSessionIds = session.GroupId != null
                ? allSessions.Where(s => s.GroupId == session.GroupId).Select(s => s.Id).ToHashSet()
                : new HashSet<string> { session.Id };
            if (activeBookings.Any(b => groupSessionIds.Contains(b.SessionId)))
                return BookingResult.Fail("You already have a booking for this round. Cancel it first to choose a different slot.");
        }

        // Availability check
        var booked = await _bookingRepository.CountActiveBySlotAsync(session.Id, slot.Id, cancellationToken);
        if (booked >= slot.Capacity)
            return BookingResult.Fail("This slot is now full. Please choose another.");

        // Compute scheduled datetime (UTC)
        var scheduledUtc = new DateTimeOffset(
            slot.Date.Year, slot.Date.Month, slot.Date.Day,
            slot.TimeUtc.Hour, slot.TimeUtc.Minute, slot.TimeUtc.Second, TimeSpan.Zero);

        // Create booking
        var booking = new SchedulingBooking
        {
            SessionId = session.Id,
            SlotId = slot.Id,
            RegistrationId = registration.Id,
            Cid = registration.Cid,
            CompetitorName = registration.PersonalInfo.FullName,
            Date = slot.Date,
            TimeUtc = slot.TimeUtc,
            SectionLabel = session.SectionLabel,
            Status = BookingStatus.Active,
            BookedAt = DateTimeOffset.UtcNow
        };
        await _bookingRepository.SaveAsync(booking, cancellationToken);

        // Update CompetitionProgress
        var progress = await _progressRepository.GetByRegistrationIdAsync(registration.Id, cancellationToken);
        if (progress != null && session.LinkedRoundName != null)
        {
            var roundEntry = progress.Rounds.FirstOrDefault(r =>
                r.Name.Equals(session.LinkedRoundName, StringComparison.OrdinalIgnoreCase));
            if (roundEntry != null)
            {
                roundEntry.Status = RoundEntryStatus.Scheduled;
                roundEntry.ScheduledDateTime = scheduledUtc;
                roundEntry.ScheduledSection = session.SectionLabel;
                roundEntry.Acknowledged = true;
                roundEntry.AcknowledgedAt = DateTimeOffset.UtcNow;
                await _progressRepository.SaveAsync(progress, cancellationToken);
            }
        }

        return BookingResult.Ok(booking);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CANCELLATION
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Cancels an active booking and reverts the competitor's RoundEntry to Active,
    /// clearing ScheduledDateTime and ScheduledSection.
    /// The participant can then book a new slot.
    /// </summary>
    public async Task<bool> CancelBookingAsync(
        string bookingId,
        string? reason,
        IEnumerable<SchedulingSession> allSessions,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken);
        if (booking == null || booking.Status != BookingStatus.Active) return false;

        await _bookingRepository.CancelAsync(bookingId, reason, cancellationToken);

        // Revert CompetitionProgress
        var progress = await _progressRepository.GetByRegistrationIdAsync(booking.RegistrationId, cancellationToken);
        if (progress != null)
        {
            var session = allSessions.FirstOrDefault(s => s.Id == booking.SessionId);
            if (session?.LinkedRoundName != null)
            {
                var roundEntry = progress.Rounds.FirstOrDefault(r =>
                    r.Name.Equals(session.LinkedRoundName, StringComparison.OrdinalIgnoreCase));
                if (roundEntry != null && roundEntry.Status == RoundEntryStatus.Scheduled)
                {
                    roundEntry.Status = RoundEntryStatus.Active;
                    roundEntry.ScheduledDateTime = null;
                    roundEntry.ScheduledSection = null;
                    await _progressRepository.SaveAsync(progress, cancellationToken);
                }
            }
        }

        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DISPLAY HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Converts a UTC slot time to Central Time for display.
    /// Always show CT to participants, consistent with app-wide time conventions.
    /// </summary>
    public static DateTimeOffset ToCentralTime(DateTimeOffset utc)
        => TimeZoneInfo.ConvertTime(utc, CentralTime);

    /// <summary>
    /// Interprets a naive DateTime as Central Time and returns it as UTC.
    /// Used when converting CT datetime-local inputs to UTC for storage.
    /// </summary>
    public static DateTimeOffset CentralTimeToUtc(DateTime centralDt)
    {
        var unspecified = DateTime.SpecifyKind(centralDt, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, CentralTime));
    }

    /// <summary>
    /// Interprets a naive TimeOnly as Central Time and returns its UTC equivalent.
    /// Used for slot generator time inputs.
    /// </summary>
    public static TimeOnly CentralTimeOnlyToUtc(TimeOnly centralTime)
    {
        var today = DateTime.Today;
        var centralDt = today.Add(centralTime.ToTimeSpan());
        var utcDt = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(centralDt, DateTimeKind.Unspecified), CentralTime);
        return TimeOnly.FromDateTime(utcDt);
    }

    /// <summary>
    /// Converts a UTC TimeOnly to Central Time for display.
    /// </summary>
    public static TimeOnly UtcTimeOnlyToCentral(TimeOnly utcTime)
    {
        var today = DateTime.Today;
        var utcDt = DateTime.SpecifyKind(today.Add(utcTime.ToTimeSpan()), DateTimeKind.Utc);
        var centralDt = TimeZoneInfo.ConvertTimeFromUtc(utcDt, CentralTime);
        return TimeOnly.FromDateTime(centralDt);
    }

    /// <summary>
    /// Formats a slot time for participant display, e.g. "10:30 AM CT".
    /// </summary>
    public static string FormatSlotTime(DateOnly date, TimeOnly timeUtc)
    {
        var utc = new DateTimeOffset(
            date.Year, date.Month, date.Day,
            timeUtc.Hour, timeUtc.Minute, 0, TimeSpan.Zero);
        var ct = TimeZoneInfo.ConvertTime(utc, CentralTime);
        return ct.ToString("h:mm tt") + " CT";
    }
}

/// <summary>Result of a booking attempt.</summary>
public class BookingResult
{
    public bool Success { get; private init; }
    public string? Error { get; private init; }
    public SchedulingBooking? Booking { get; private init; }

    public static BookingResult Ok(SchedulingBooking booking) =>
        new() { Success = true, Booking = booking };

    public static BookingResult Fail(string error) =>
        new() { Success = false, Error = error };
}
