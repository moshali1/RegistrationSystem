namespace RegistrationSystem.Core.Domain.Scheduling;

/// <summary>
/// A scheduling session for a specific competition round. Sessions can be grouped with a GroupId
/// to represent parallel sections (A, B, C...) for the same round, each with independent slots,
/// capacity, and eligibility filters. Stored embedded in CompetitionSettings.
/// </summary>
public class SchedulingSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Display name shown to participants, e.g. "Screening Round 2026 — Session A"</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Groups parallel sessions together (same round, different sections).
    /// Sessions with the same GroupId are displayed together on the scheduling page.
    /// Null = standalone session with no parallel sections.
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// Section label within a group, e.g. "A", "B", "C".
    /// Stored on RoundEntry.ScheduledSection when a participant books this session,
    /// so the competition tracker and messaging can show which section they are in.
    /// Null if the session has no parallel sections.
    /// </summary>
    public string? SectionLabel { get; set; }

    /// <summary>
    /// Matches a RoundDefinition.Name in category settings (case-insensitive).
    /// When a participant books this session, the matching RoundEntry in their
    /// CompetitionProgress is updated: Status → Scheduled, ScheduledDateTime set.
    /// </summary>
    public string? LinkedRoundName { get; set; }

    /// <summary>Whether participants can currently make or change bookings.</summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Optional virtual conference link specific to this section.
    /// Shown to participants after they book a slot in this session.
    /// Use this when parallel sections have different conference links.
    /// </summary>
    public string? VirtualLink { get; set; }

    public DateTimeOffset? SchedulingOpensAt { get; set; }
    public DateTimeOffset? SchedulingClosesAt { get; set; }

    /// <summary>
    /// Restricts which participants can book into this session by gender.
    /// Used to route male/female competitors to separate sections.
    /// </summary>
    public SessionGenderFilter GenderFilter { get; set; } = SessionGenderFilter.All;

    /// <summary>
    /// Restricts which participants can book into this session by state.
    /// Used to route Minnesota vs. out-of-state competitors to separate sections.
    /// </summary>
    public SessionGeographicFilter GeographicFilter { get; set; } = SessionGeographicFilter.All;

    /// <summary>
    /// Restricts which categories can book into this session.
    /// Empty = all categories eligible for this session's linked round can book.
    /// </summary>
    public List<string> AllowedCategoryIds { get; set; } = new();

    /// <summary>
    /// Time slots available for booking. Each slot has its own date, time, capacity, and active flag.
    /// Default generation: 12 slots per hour (one every 5 minutes), individually configurable.
    /// All times are stored in UTC and displayed to participants in Central Time.
    /// </summary>
    public List<SchedulingSlot> Slots { get; set; } = new();
}

/// <summary>
/// A single bookable time slot within a scheduling session.
/// Each slot has its own capacity and can be individually activated or deactivated,
/// allowing admins to open slots in batches or reserve certain times.
/// </summary>
public class SchedulingSlot
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Local competition date for display (no timezone conversion applied to date).</summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Slot start time stored in UTC. Always display to participants in Central Time.
    /// At 12 slots/hour, slots are generated 5 minutes apart by default.
    /// </summary>
    public TimeOnly TimeUtc { get; set; }

    /// <summary>Maximum number of participants that can book this slot.</summary>
    public int Capacity { get; set; } = 1;

    /// <summary>
    /// When false, this slot is hidden from participants and cannot be booked.
    /// Allows admins to hold slots in reserve and open them later as needed.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Optional admin note visible only in the admin UI, e.g. "Reserved for walk-ins".</summary>
    public string? Note { get; set; }
}

public enum SessionGenderFilter
{
    All = 0,
    MaleOnly = 1,
    FemaleOnly = 2
}

public enum SessionGeographicFilter
{
    All = 0,
    MinnesotaOnly = 1,
    OutsideMinnesota = 2
}
