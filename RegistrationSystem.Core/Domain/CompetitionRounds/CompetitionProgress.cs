namespace RegistrationSystem.Core.Domain.CompetitionRounds;

/// <summary>
/// Tracks a registration's progression through the competition round pipeline.
/// One document per registration, with an embedded list of round entries
/// matching the category's configured RoundDefinitions.
/// </summary>
public class CompetitionProgress
{
    public string Id { get; set; } = string.Empty;
    public string RegistrationId { get; set; } = string.Empty;

    // Denormalized from Registration
    public int CompetitionYear { get; set; }
    public string DivisionId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string? Cid { get; set; }
    public string CompetitorName { get; set; } = string.Empty;

    /// <summary>
    /// Ordered list of round entries — one per RoundDefinition in the category.
    /// </summary>
    public List<RoundEntry> Rounds { get; set; } = new();

    // Computed helpers

    /// <summary>
    /// True if any non-pending round has not been acknowledged.
    /// Bypassed rounds are included — the red dot clears when the user visits their messages page.
    /// Active rounds that were auto-advanced start as Acknowledged=true, so they don't trigger this.
    /// Used for red dot notification indicators.
    /// </summary>
    public bool HasPendingAcknowledgment => Rounds.Any(r =>
        r.Status != RoundEntryStatus.Pending && !r.Acknowledged);

    public RoundEntry? GetRound(string roundDefinitionId) =>
        Rounds.FirstOrDefault(r => r.RoundDefinitionId == roundDefinitionId);

    public RoundEntry? GetActiveRound() =>
        Rounds.FirstOrDefault(r => r.Status == RoundEntryStatus.Active);
}

/// <summary>
/// A single round entry within a competitor's competition progress.
/// Maps 1:1 to a RoundDefinition configured in the category settings.
/// </summary>
public class RoundEntry
{
    /// <summary>FK to RoundDefinition.Id in the category's settings.</summary>
    public string RoundDefinitionId { get; set; } = string.Empty;

    /// <summary>Copied from RoundDefinition for sorting without settings lookup.</summary>
    public int Order { get; set; }

    /// <summary>Copied from RoundDefinition for display without settings lookup.</summary>
    public string Name { get; set; } = string.Empty;

    public RoundEntryStatus Status { get; set; } = RoundEntryStatus.Pending;

    /// <summary>Null until admin enters a result.</summary>
    public RoundResult? Result { get; set; }

    // Scheduling (only for rounds linked to a SchedulingSession)
    /// <summary>
    /// The booked date and time in UTC for this round, set when the participant
    /// books a slot via the scheduling system. Display to participants in Central Time.
    /// Cleared when the booking is cancelled; participant reverts to Active status.
    /// </summary>
    public DateTimeOffset? ScheduledDateTime { get; set; }

    /// <summary>
    /// The section label of the scheduling session the participant booked into
    /// (e.g. "A", "B", "C" for parallel sessions). Null if the session has no sections.
    /// Used by the competition tracker and round messages to identify which session group
    /// the competitor is assigned to.
    /// </summary>
    public string? ScheduledSection { get; set; }

    // Acknowledgment (auto-set when user visits their tracking page)
    public bool Acknowledged { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }

    // Bypass (admin can skip this round for individual competitors)
    public bool Bypassed { get; set; }
    public string? BypassReason { get; set; }

    /// <summary>
    /// Admin comment for this round entry, or instructions for the next round.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Placement number for final rounds (1st, 2nd, 3rd, etc.).
    /// Only populated when RoundDefinition.HasPlacement is true and Result is Placed.
    /// </summary>
    public int? Placement { get; set; }
}

/// <summary>
/// Lifecycle status of a round entry within a competitor's progression.
/// </summary>
public enum RoundEntryStatus
{
    /// <summary>Not yet reached — previous round not complete.</summary>
    Pending = 0,

    /// <summary>Currently in this round — awaiting scheduling or result entry.</summary>
    Active = 1,

    /// <summary>
    /// Participant has booked a scheduling slot for this round.
    /// ScheduledDateTime and ScheduledSection are set on the RoundEntry.
    /// Reverts to Active if the booking is cancelled.
    /// </summary>
    Scheduled = 5,

    /// <summary>Result entered, round done — passed/qualified.</summary>
    Completed = 2,

    /// <summary>Skipped by admin (e.g., previous year participation).</summary>
    Bypassed = 3,

    /// <summary>Failed/NotQualified/NoShow in this round — stops progression.</summary>
    Eliminated = 4
}

/// <summary>
/// Result of a round, entered by admin. The available results depend on the
/// RoundDefinition's ResultType (PassFail vs QualifyEliminate) and HasPlacement flag.
/// </summary>
public enum RoundResult
{
    // PassFail results
    Pass = 0,
    Fail = 1,

    // QualifyEliminate results
    Qualified = 2,
    NotQualified = 3,

    // Common
    NoShow = 4,

    // Placement results (HasPlacement = true)
    Placed = 5,
    Participated = 6
}
