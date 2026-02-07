using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Core.Application.Settings;

public class CompetitionSettingsValidator
{
    public ValidationResult Validate(CompetitionSettings settings)
    {
        var errors = new List<string>();

        ValidateGlobalSettings(settings, errors);
        ValidateDivisions(settings.Divisions, errors);

        return new ValidationResult(errors);
    }

    private static void ValidateGlobalSettings(CompetitionSettings settings, List<string> errors)
    {
        if (settings.RegistrationEnabled)
        {
            if (!settings.RegistrationStart.HasValue || !settings.RegistrationEnd.HasValue)
                errors.Add("Start and end dates are required when registration is enabled.");

            if (settings.RegistrationStart.HasValue &&
                settings.RegistrationEnd.HasValue &&
                settings.RegistrationEnd <= settings.RegistrationStart)
                errors.Add("Global registration end must be after start.");
        }
    }

    private static void ValidateDivisions(List<Division> divisions, List<string> errors)
    {
        var divisionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var division in divisions)
        {
            if (string.IsNullOrWhiteSpace(division.Name))
                errors.Add("Division name cannot be empty.");

            if (!divisionNames.Add(division.Name))
                errors.Add($"Duplicate division name: {division.Name}");

            ValidateCategories(division, errors);
        }
    }

    private static void ValidateCategories(Division division, List<string> errors)
    {
        var categoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in division.Categories)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
                errors.Add($"Category name cannot be empty in division '{division.Name}'.");

            if (!categoryNames.Add(category.Name))
                errors.Add($"Duplicate category name '{category.Name}' in division '{division.Name}'.");

            if (category.MaxAgeYears is < 0)
                errors.Add($"Max age cannot be negative for category '{category.Name}'.");

            if (category.RegistrationStart.HasValue &&
                category.RegistrationEnd.HasValue &&
                category.RegistrationEnd <= category.RegistrationStart)
                errors.Add($"Category '{category.Name}' registration end must be after start.");
        }
    }
}

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; }

    public ValidationResult(List<string> errors)
    {
        Errors = errors ?? [];
    }

    public void ThrowIfInvalid()
    {
        if (!IsValid)
            throw new ValidationException(string.Join(" ", Errors));
    }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
