namespace RegistrationSystem.Core.Application.Settings;

/// <summary>
/// Reason why registration is closed for a category.
/// Ordered by hierarchy priority (global → division → category → dates).
/// </summary>
public enum RegistrationClosedReason
{
    Open = 0,
    GloballyDisabled = 1,
    DivisionDisabled = 2,
    CategoryDisabled = 3,
    NotStarted = 4,
    Ended = 5
}

public class CategoryRegistrationStatus
{
    public required string CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required string DivisionId { get; init; }
    public required string DivisionName { get; init; }

    public bool IsOpen { get; init; } // Whether registration is currently open
    public RegistrationClosedReason Reason { get; init; } // If closed, the reason why
    public DateTimeOffset? EffectiveStart { get; init; } // Effective start date (override or global)
    public DateTimeOffset? EffectiveEnd { get; init; } // Effective end date (override or global)
    public bool HasOverride { get; init; } // Category has override dates (different from global)
    public bool IsManuallyEnabled { get; init; } // Category's manual enabled flag is true
    public string StatusMessage { get; init; } = string.Empty; // Human-readable status message
    public string StatusLabel { get; init; } = string.Empty; // Short status label for UI badges
}

public class DivisionRegistrationStatus
{
    public required string DivisionId { get; init; }
    public required string DivisionName { get; init; }

    public bool IsManuallyEnabled { get; init; } // Division's manual enabled flag is true
    public bool HasOpenCategories => OpenCategoryCount > 0; // Any categories currently open
    public int OpenCategoryCount { get; init; } // Categories currently open for registration
    public int TotalCategoryCount { get; init; } // Total number of categories in this division
    public int EnabledCategoryCount { get; init; } // Categories manually enabled (regardless of date status)
    public int OverrideCategoryCount { get; init; } // Categories using override dates
    public List<CategoryRegistrationStatus> Categories { get; init; } = new();
}

public class GlobalRegistrationStatus
{
    public bool IsGloballyEnabled { get; init; } // Global registration toggle is ON
    public DateTimeOffset? GlobalStart { get; init; } // Global registration start date
    public DateTimeOffset? GlobalEnd { get; init; } // Global registration end date
    public DateOnly AgeCutoffDate { get; init; } // Age cutoff date for eligibility calculations
    public bool IsWithinGlobalDateWindow { get; init; } // Currently within the global date window
    public int TotalOpenCategories { get; init; } // Total categories currently open
    public int TotalCategories { get; init; } // Total categories across all divisions
    public int TotalEnabledCategories { get; init; } // Total categories manually enabled
    public int CategoriesWithOverrides { get; init; } // Categories using override dates
    public int TotalDivisions { get; init; } // Total divisions
    public int EnabledDivisions { get; init; } // Divisions manually enabled
    public List<DivisionRegistrationStatus> Divisions { get; init; } = new();
    public string SummaryMessage { get; init; } = string.Empty; // Summary message for the dashboard
}
