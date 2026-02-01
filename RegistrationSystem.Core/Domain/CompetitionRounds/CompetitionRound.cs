namespace RegistrationSystem.Core.Domain.CompetitionRounds;

public class CompetitionRound
{
    public string Id { get; set; } = string.Empty;
    public string RegistrationId { get; set; } = string.Empty;

    public int CompetitionYear { get; set; }
    public string DivisionId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string? Cid { get; set; }
    public string CompetitorName { get; set; } = string.Empty;

    // Video Qualification

    public VideoQualificationStatus VideoQualification { get; set; } = VideoQualificationStatus.Pending;
    public DateTimeOffset? VideoQualificationAssessedAt { get; set; }
    public string? VideoQualificationComment { get; set; }

    // Preliminary Round

    public DateTimeOffset? PreliminaryRoundDateTime { get; set; }
    public bool PreliminaryRoundAcknowledged { get; set; }
    public DateTimeOffset? PreliminaryRoundAcknowledgedAt { get; set; }

    // Final Round

    public bool IsQualified { get; set; }
    public bool IsAttended { get; set; }
    public DateTimeOffset? FinalRoundDateTime { get; set; }
    public bool FinalRoundAcknowledged { get; set; }
    public DateTimeOffset? FinalRoundAcknowledgedAt { get; set; }

    // Computed

    public bool HasPreliminaryRound => PreliminaryRoundDateTime.HasValue || VideoQualification != VideoQualificationStatus.Pending;
    public bool HasFinalRound => FinalRoundDateTime.HasValue;

    public bool AllRoundsAcknowledged =>
        (!HasPreliminaryRound || PreliminaryRoundAcknowledged) &&
        (!HasFinalRound || FinalRoundAcknowledged);

    public bool HasPendingAcknowledgment =>
        (HasPreliminaryRound && !PreliminaryRoundAcknowledged) ||
        (HasFinalRound && !FinalRoundAcknowledged);
}

public enum VideoQualificationStatus
{
    Pending,
    Pass,
    Fail
}
