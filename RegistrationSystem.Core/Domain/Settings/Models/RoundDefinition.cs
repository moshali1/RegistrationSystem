namespace RegistrationSystem.Core.Domain.Settings;

/// <summary>
/// Defines a single round in a category's competition pipeline.
/// Configurable per category in admin settings.
/// </summary>
public class RoundDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Display order (1, 2, 3...). Determines round sequence.</summary>
    public int Order { get; set; }

    /// <summary>Display name (e.g., "Video Qualification", "Screening Round").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// True for live events with scheduled date/time (screening, prelim, final).
    /// False for async review rounds (video qualification).
    /// </summary>
    public bool HasSchedule { get; set; }

    /// <summary>How results are recorded: Pass/Fail or Qualify/Eliminate.</summary>
    public RoundResultType ResultType { get; set; }

    /// <summary>Can admin bypass this round for individual competitors?</summary>
    public bool AllowBypass { get; set; }

    /// <summary>Track placement (1st, 2nd, 3rd) — typically only for final rounds.</summary>
    public bool HasPlacement { get; set; }

    // ── Messages (supports HTML) ───────────────────────────────────────

    /// <summary>
    /// Shown to competitor when they pass/qualify this round.
    /// E.g., "Congratulations! You've advanced to the Preliminary Round."
    /// </summary>
    public string? PassMessage { get; set; }

    /// <summary>
    /// Shown to competitor when they fail/are eliminated in this round.
    /// E.g., "Unfortunately you did not qualify. Thank you for participating."
    /// </summary>
    public string? FailMessage { get; set; }

    /// <summary>
    /// Shown to competitor after their schedule is posted (HasSchedule rounds only).
    /// Contains venue, directions, parking, requirements, how to join, etc.
    /// </summary>
    public string? ScheduleDetails { get; set; }
}

/// <summary>
/// How results are recorded for a round.
/// </summary>
public enum RoundResultType
{
    /// <summary>Binary pass/fail (video qualification, screening).</summary>
    PassFail = 0,

    /// <summary>Qualified/NotQualified/NoShow (preliminary, final).</summary>
    QualifyEliminate = 1
}
