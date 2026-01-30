namespace RegistrationSystem.Core.Domain.CompetitionRounds;

/// <summary>
/// Tracks competition round assignments and acknowledgments for a registration.
/// One-to-one relationship with Registration.
/// </summary>
public class CompetitionRound
{
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the registration this round belongs to.
    /// </summary>
    public string RegistrationId { get; set; } = string.Empty;

    /// <summary>
    /// Competition year (denormalized for easier querying).
    /// </summary>
    public int CompetitionYear { get; set; }

    /// <summary>
    /// Division ID (denormalized for reporting).
    /// </summary>
    public string DivisionId { get; set; } = string.Empty;

    /// <summary>
    /// Category ID (denormalized for reporting).
    /// </summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>
    /// Competitor's CID (denormalized for display).
    /// </summary>
    public string? Cid { get; set; }

    /// <summary>
    /// Competitor's full name (denormalized for display).
    /// </summary>
    public string CompetitorName { get; set; } = string.Empty;

    /// <summary>
    /// Video qualification status.
    /// </summary>
    public VideoQualificationStatus VideoQualification { get; set; } = VideoQualificationStatus.Pending;

    /// <summary>
    /// When video qualification was assessed.
    /// </summary>
    public DateTimeOffset? VideoQualificationAssessedAt { get; set; }

    /// <summary>
    /// Admin comment about video qualification decision.
    /// </summary>
    public string? VideoQualificationComment { get; set; }

    /// <summary>
    /// Assigned date and time for preliminary round.
    /// </summary>
    public DateTimeOffset? PreliminaryRoundDateTime { get; set; }

    /// <summary>
    /// Whether the competitor has acknowledged their preliminary round assignment.
    /// </summary>
    public bool PreliminaryRoundAcknowledged { get; set; }

    /// <summary>
    /// When the preliminary round was acknowledged.
    /// </summary>
    public DateTimeOffset? PreliminaryRoundAcknowledgedAt { get; set; }

    /// <summary>
    /// Whether competitor qulaifies for final round or not
    /// </summary>
    public bool IsQualify { get; set; }

    public bool IsAttended { get; set; }

    /// <summary>
    /// Assigned date and time for final round.
    /// </summary>
    public DateTimeOffset? FinalRoundDateTime { get; set; }

    /// <summary>
    /// Whether the competitor has acknowledged their final round assignment.
    /// </summary>
    public bool FinalRoundAcknowledged { get; set; }

    /// <summary>
    /// When the final round was acknowledged.
    /// </summary>
    public DateTimeOffset? FinalRoundAcknowledgedAt { get; set; }

    /// <summary>
    /// When this record was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this record was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Checks if preliminary round has been assigned.
    /// </summary>
    public bool HasPreliminaryRound => PreliminaryRoundDateTime.HasValue || VideoQualification != VideoQualificationStatus.Pending; // !those that failed

    /// <summary>
    /// Checks if final round has been assigned.
    /// </summary>
    public bool HasFinalRound => FinalRoundDateTime.HasValue;

    /// <summary>
    /// Checks if all assigned rounds have been acknowledged.
    /// </summary>
    public bool AllRoundsAcknowledged =>
        (!HasPreliminaryRound || PreliminaryRoundAcknowledged) &&
        (!HasFinalRound || FinalRoundAcknowledged);

    /// <summary>
    /// Checks if any round needs acknowledgment.
    /// </summary>
    //////////////////public bool HasPendingAcknowledgment =>
    //////////////////    (HasPreliminaryRound && !PreliminaryRoundAcknowledged) ||
    //////////////////    (HasFinalRound && !FinalRoundAcknowledged);

    public bool HasPendingAcknowledgment =>
        (!FinalRoundAcknowledged);

}

/// <summary>
/// Video qualification status for competition eligibility.
/// </summary>
public enum VideoQualificationStatus
{
    /// <summary>
    /// Video has not been reviewed yet.
    /// </summary>
    Pending,

    /// <summary>
    /// Video passed qualification - eligible for competition.
    /// </summary>
    Pass,

    /// <summary>
    /// Video failed qualification - not eligible for competition.
    /// </summary>
    Fail
}