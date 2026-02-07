namespace RegistrationSystem.Core.Domain.CompetitionRounds;

public class CompetitionRound
{
    public string Id { get; set; } = string.Empty;
    public string RegistrationId { get; set; } = string.Empty;

    // Denormalized Fields
    public int CompetitionYear { get; set; }
    public string DivisionId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string? Cid { get; set; }
    public string CompetitorName { get; set; } = string.Empty;

    // Video Qualification
    public VideoQualificationStatus VideoQualification { get; set; } = VideoQualificationStatus.Pending;
    /// <summary>
    /// Instructions for the SCREENING ROUND schedule.
    /// Shown to competitors who passed video qualification under their screening round schedule.
    /// Contains venue, time, parking, what to bring, etc. for screening day.
    /// </summary>
    public string? VideoQualificationComment { get; set; }

    // Screening Round
    public DateTimeOffset? ScreeningRoundDateTime { get; set; }
    public bool ScreeningRoundBypass { get; set; }
    public ScreeningRoundStatus ScreeningRoundResult { get; set; } = ScreeningRoundStatus.Pending;
    public bool ScreeningRoundAcknowledged { get; set; }
    public DateTimeOffset? ScreeningRoundAcknowledgedAt { get; set; }
    /// <summary>
    /// Instructions for the PRELIMINARY ROUND schedule.
    /// Shown to competitors who passed screening under their preliminary round schedule.
    /// Contains venue, time, parking, what to bring, etc. for preliminary day.
    /// </summary>
    public string? ScreeningRoundComment { get; set; }

    // Preliminary Round
    public DateTimeOffset? PreliminaryRoundDateTime { get; set; }
    public bool PreliminaryRoundAcknowledged { get; set; }
    public DateTimeOffset? PreliminaryRoundAcknowledgedAt { get; set; }
    public PreliminaryRoundStatus PreliminaryRoundResult { get; set; } = PreliminaryRoundStatus.Pending;
    /// <summary>
    /// Instructions for the FINAL ROUND schedule.
    /// Shown to competitors who qualified from preliminary under their final round schedule.
    /// Contains venue, time, parking, what to bring, award ceremony details, etc.
    /// </summary>
    public string? PreliminaryRoundComment { get; set; }

    // Final Round
    public DateTimeOffset? FinalRoundDateTime { get; set; }
    public bool FinalRoundAcknowledged { get; set; }
    public DateTimeOffset? FinalRoundAcknowledgedAt { get; set; }
    public FinalRoundStatus FinalRoundResult { get; set; } = FinalRoundStatus.Pending;
    public int? FinalRoundPlacement { get; set; }
    /// <summary>
    /// Optional custom message for FINAL RESULT.
    /// Shown to competitors in their final result timeline item.
    /// If null, default placement messages are shown (e.g., "Masha'Allah! 🥇 First Place...").
    public string? FinalRoundComment { get; set; }

    // Computed
    public bool HasScreeningRound => ScreeningRoundDateTime.HasValue || ScreeningRoundBypass;
    public bool HasPreliminaryRound => PreliminaryRoundDateTime.HasValue;
    public bool HasFinalRound => FinalRoundDateTime.HasValue;

    // Used for red dot notification on registrations list
    public bool HasPendingAcknowledgment =>
        (PreliminaryRoundDateTime.HasValue && !PreliminaryRoundAcknowledged) ||
        (ScreeningRoundDateTime.HasValue && !ScreeningRoundBypass && !ScreeningRoundAcknowledged) ||
        (FinalRoundDateTime.HasValue && !FinalRoundAcknowledged);
}

public enum VideoQualificationStatus
{
    Pending,
    Pass,
    Fail
}

public enum ScreeningRoundStatus
{
    Pending,
    Pass,
    Fail,
    NoShow
}

public enum PreliminaryRoundStatus
{
    Pending,
    Qualified,
    NotQualified,
    NoShow
}

public enum FinalRoundStatus
{
    Pending,
    Placed,
    Participated,
    NoShow
}