namespace RegistrationSystem.Core.Domain.Settings;

public class CompetitionSettings
{
    public const string SingletonId = "default-competition-settings";

    public string Id { get; set; } = SingletonId;

    public bool RegistrationEnabled { get; set; }
    public DateTimeOffset? RegistrationStart { get; set; }
    public DateTimeOffset? RegistrationEnd { get; set; }
    public DateOnly AgeCutoffDate { get; set; } = new DateOnly(DateTime.UtcNow.Year, 1, 1);

    public List<Division> Divisions { get; set; } = new();
    public CompetitionInfo CompetitionInfo { get; set; } = new();
    public CidConfiguration CidConfiguration { get; set; } = new();

    public Division? FindDivision(string divisionId) =>
        Divisions.FirstOrDefault(d => d.Id == divisionId);
}

public class Division
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<Category> Categories { get; set; } = new();

    public Category? FindCategory(string categoryId) =>
        Categories.FirstOrDefault(c => c.Id == categoryId);
}

public class Category
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string? AlternateName { get; set; }
    public bool IsEnabled { get; set; }

    public int? MaxAgeYears { get; set; }
    public PortionOption PortionOption { get; set; } = PortionOption.NotApplicable;

    public DateTimeOffset? RegistrationStart { get; set; }
    public DateTimeOffset? RegistrationEnd { get; set; }

    public bool RequiresVideo { get; set; }
    public string? VideoInstructions { get; set; }

    public bool ScreeningRoundEnabled { get; set; }

    public bool AllowMultipleInDivision { get; set; }
    public bool AllowEdit { get; set; } = true;
    public bool AllowWithdraw { get; set; } = true;

    /// <summary>
    /// Ordered list of round definitions for this category's competition pipeline.
    /// Each entry defines a round's name, type, result mode, and messaging.
    /// </summary>
    public List<RoundDefinition> Rounds { get; set; } = new();
}

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

public enum PortionOption
{
    NotApplicable = 0,
    TopOnly = 1,
    BottomOnly = 2,
    TopOrBottom = 3
}
