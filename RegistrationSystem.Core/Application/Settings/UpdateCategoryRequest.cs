using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Core.Application.Settings;

public class UpdateCategoryRequest
{
    public string DivisionId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }

    public int? MaxAgeYears { get; set; }

    // Optional per-category overrides; if null, use global
    public DateTimeOffset? RegistrationStart { get; set; }
    public DateTimeOffset? RegistrationEnd { get; set; }

    public PortionOption PortionOption { get; set; } = PortionOption.NotApplicable;
}
