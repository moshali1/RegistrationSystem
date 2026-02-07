namespace RegistrationSystem.Core.Application.Settings;

public class GlobalRegistrationStatus
{
    public bool IsGloballyEnabled { get; init; }
    public DateTimeOffset? GlobalStart { get; init; }
    public DateTimeOffset? GlobalEnd { get; init; }
    public DateOnly AgeCutoffDate { get; init; }
    public bool IsWithinGlobalDateWindow { get; init; }
    public int TotalOpenCategories { get; init; }
    public int TotalCategories { get; init; }
    public int TotalEnabledCategories { get; init; }
    public int CategoriesWithOverrides { get; init; }
    public int TotalDivisions { get; init; }
    public int EnabledDivisions { get; init; }
    public List<DivisionRegistrationStatus> Divisions { get; init; } = new();
    public string SummaryMessage { get; init; } = string.Empty;
}
