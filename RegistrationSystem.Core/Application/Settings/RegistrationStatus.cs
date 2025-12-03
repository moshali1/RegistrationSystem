namespace RegistrationSystem.Core.Application.Settings;

/// <summary>
/// Reason why registration is closed for a category.
/// Ordered by hierarchy priority (global → division → category → dates).
/// </summary>
public enum RegistrationClosedReason
{
    /// <summary>Registration is currently open.</summary>
    Open = 0,

    /// <summary>Global registration toggle is OFF (admin control).</summary>
    GloballyDisabled = 1,

    /// <summary>Division is manually disabled.</summary>
    DivisionDisabled = 2,

    /// <summary>Category is manually disabled.</summary>
    CategoryDisabled = 3,

    /// <summary>Current date is before the effective start date.</summary>
    NotStarted = 4,

    /// <summary>Current date is after the effective end date.</summary>
    Ended = 5
}

/// <summary>
/// Registration status for a single category.
/// </summary>
public class CategoryRegistrationStatus
{
    public required string CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required string DivisionId { get; init; }
    public required string DivisionName { get; init; }

    /// <summary>Whether registration is currently open for this category.</summary>
    public bool IsOpen { get; init; }

    /// <summary>If closed, the reason why.</summary>
    public RegistrationClosedReason Reason { get; init; }

    /// <summary>The effective start date (override or global).</summary>
    public DateTimeOffset? EffectiveStart { get; init; }

    /// <summary>The effective end date (override or global).</summary>
    public DateTimeOffset? EffectiveEnd { get; init; }

    /// <summary>Whether this category has override dates (different from global).</summary>
    public bool HasOverride { get; init; }

    /// <summary>Whether the category's manual enabled flag is true.</summary>
    public bool IsManuallyEnabled { get; init; }

    /// <summary>Human-readable status message.</summary>
    public string StatusMessage { get; init; } = string.Empty;

    /// <summary>Short status label for UI badges.</summary>
    public string StatusLabel { get; init; } = string.Empty;
}

/// <summary>
/// Registration status summary for a division.
/// </summary>
public class DivisionRegistrationStatus
{
    public required string DivisionId { get; init; }
    public required string DivisionName { get; init; }

    /// <summary>Whether the division's manual enabled flag is true.</summary>
    public bool IsManuallyEnabled { get; init; }

    /// <summary>Whether any categories in this division are currently open.</summary>
    public bool HasOpenCategories => OpenCategoryCount > 0;

    /// <summary>Count of categories currently open for registration.</summary>
    public int OpenCategoryCount { get; init; }

    /// <summary>Total number of categories in this division.</summary>
    public int TotalCategoryCount { get; init; }

    /// <summary>Count of categories that are manually enabled (regardless of date status).</summary>
    public int EnabledCategoryCount { get; init; }

    /// <summary>Count of categories using override dates.</summary>
    public int OverrideCategoryCount { get; init; }

    /// <summary>Detailed status for each category.</summary>
    public List<CategoryRegistrationStatus> Categories { get; init; } = new();
}

/// <summary>
/// Complete registration status for the entire competition.
/// </summary>
public class GlobalRegistrationStatus
{
    /// <summary>Whether the global registration toggle is ON.</summary>
    public bool IsGloballyEnabled { get; init; }

    /// <summary>Global registration start date (applies to categories without overrides).</summary>
    public DateTimeOffset? GlobalStart { get; init; }

    /// <summary>Global registration end date (applies to categories without overrides).</summary>
    public DateTimeOffset? GlobalEnd { get; init; }

    /// <summary>Age cutoff date for eligibility calculations.</summary>
    public DateOnly AgeCutoffDate { get; init; }

    /// <summary>Whether we're currently within the global date window.</summary>
    public bool IsWithinGlobalDateWindow { get; init; }

    /// <summary>Total categories currently open for registration.</summary>
    public int TotalOpenCategories { get; init; }

    /// <summary>Total categories across all divisions.</summary>
    public int TotalCategories { get; init; }

    /// <summary>Total categories that are manually enabled.</summary>
    public int TotalEnabledCategories { get; init; }

    /// <summary>Categories using override dates.</summary>
    public int CategoriesWithOverrides { get; init; }

    /// <summary>Total divisions.</summary>
    public int TotalDivisions { get; init; }

    /// <summary>Divisions that are manually enabled.</summary>
    public int EnabledDivisions { get; init; }

    /// <summary>Detailed status for each division.</summary>
    public List<DivisionRegistrationStatus> Divisions { get; init; } = new();

    /// <summary>Summary message for the dashboard.</summary>
    public string SummaryMessage { get; init; } = string.Empty;
}
