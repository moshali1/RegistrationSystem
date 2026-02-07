namespace RegistrationSystem.Core.Application.Settings;

public class DivisionRegistrationStatus
{
    public string DivisionId { get; init; } = string.Empty;
    public string DivisionName { get; init; } = string.Empty;

    public bool IsManuallyEnabled { get; init; }
    public bool HasOpenCategories => OpenCategoryCount > 0;
    public int OpenCategoryCount { get; init; }
    public int TotalCategoryCount { get; init; }
    public int EnabledCategoryCount { get; init; }
    public int OverrideCategoryCount { get; init; }
    public List<CategoryRegistrationStatus> Categories { get; init; } = new();
}
