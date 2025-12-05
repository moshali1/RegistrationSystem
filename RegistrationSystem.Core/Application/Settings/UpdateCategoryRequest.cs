using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Core.Application.Settings;

public class UpdateCategoryRequest
{
    public string DivisionId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AlternateName { get; set; }
    public bool IsEnabled { get; set; }
    public int? MaxAgeYears { get; set; }
    public PortionOption PortionOption { get; set; } = PortionOption.NotApplicable;

    // Optional per-category schedule overrides; if null, use global
    public DateTimeOffset? RegistrationStart { get; set; }
    public DateTimeOffset? RegistrationEnd { get; set; }

    // Video requirements
    public bool RequiresVideo { get; set; }
    public string? VideoInstructions { get; set; }

    // Registration rules
    public bool AllowMultipleInDivision { get; set; }
    public bool AllowEdit { get; set; } = true;
    public bool AllowWithdraw { get; set; } = true;
}