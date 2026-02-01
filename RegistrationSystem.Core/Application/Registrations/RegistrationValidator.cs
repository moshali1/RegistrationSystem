using RegistrationSystem.Core.Application.Settings;
using RegistrationSystem.Core.Domain.Registrations;
using RegistrationSystem.Core.Domain.Settings;
using RegistrationSystem.Core.ReferenceData;

namespace RegistrationSystem.Core.Application.Registrations;

public class RegistrationValidator
{
    private readonly IRegistrationRepository _repository;
    private readonly SettingsService _settingsService;

    public RegistrationValidator(IRegistrationRepository repository, SettingsService settingsService)
    {
        _repository = repository;
        _settingsService = settingsService;
    }

    public async Task<RegistrationValidationResult> ValidateAsync(
        Registration registration,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);

        await ValidateDuplicatesAsync(registration, settings, errors, cancellationToken);
        ValidateCategoryEligibility(registration, settings, errors);
        ValidatePhoneNumbers(registration, errors);
        ValidateCountry(registration, errors);

        return new RegistrationValidationResult(errors);
    }

    public async Task<RegistrationValidationResult> ValidateForSubmissionAsync(
        Registration registration,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);

        ValidateRequiredFields(registration, errors);
        ValidatePhoneNumbers(registration, errors);
        ValidateCountry(registration, errors);
        await ValidateDuplicatesAsync(registration, settings, errors, cancellationToken);
        ValidateCategoryEligibility(registration, settings, errors);
        ValidatePortionChoice(registration, settings, errors);

        return new RegistrationValidationResult(errors);
    }

    private static void ValidateRequiredFields(Registration registration, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(registration.PersonalInfo.FirstName))
            errors.Add("First name is required.");
        if (string.IsNullOrWhiteSpace(registration.PersonalInfo.LastName))
            errors.Add("Last name is required.");
        if (registration.PersonalInfo.DateOfBirth == default)
            errors.Add("Date of birth is required.");

        if (string.IsNullOrWhiteSpace(registration.AddressInfo.Country))
            errors.Add("Country is required.");
        if (string.IsNullOrWhiteSpace(registration.AddressInfo.StateProvince))
            errors.Add("State/Province is required.");
        if (string.IsNullOrWhiteSpace(registration.AddressInfo.City))
            errors.Add("City is required.");

        if (string.IsNullOrWhiteSpace(registration.CompetitionSelection.DivisionId))
            errors.Add("Division is required.");
        if (string.IsNullOrWhiteSpace(registration.CompetitionSelection.CategoryId))
            errors.Add("Category is required.");

        if (string.IsNullOrWhiteSpace(registration.ParentInfo.FirstName))
            errors.Add("Parent/Guardian first name is required.");
        if (string.IsNullOrWhiteSpace(registration.ParentInfo.LastName))
            errors.Add("Parent/Guardian last name is required.");
        if (string.IsNullOrWhiteSpace(registration.ParentInfo.PhoneNumber))
            errors.Add("Parent/Guardian phone number is required.");

        if (registration.TeacherInfo == null)
        {
            errors.Add("Teacher information is required.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(registration.TeacherInfo.FirstName))
                errors.Add("Teacher first name is required.");
            if (string.IsNullOrWhiteSpace(registration.TeacherInfo.LastName))
                errors.Add("Teacher last name is required.");
            if (string.IsNullOrWhiteSpace(registration.TeacherInfo.PhoneNumber))
                errors.Add("Teacher phone number is required.");
            if (string.IsNullOrWhiteSpace(registration.TeacherInfo.Institution))
                errors.Add("Institution name is required.");
        }

        if (!registration.TermsAccepted)
            errors.Add("You must accept the terms and conditions.");
    }

    private async Task ValidateDuplicatesAsync(
        Registration registration,
        CompetitionSettings? settings,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(registration.PersonalInfo.FirstName) ||
            string.IsNullOrWhiteSpace(registration.PersonalInfo.LastName) ||
            registration.PersonalInfo.DateOfBirth == default ||
            string.IsNullOrEmpty(registration.CompetitionSelection.CategoryId))
        {
            return;
        }

        var duplicates = await _repository.FindDuplicatesAsync(
            registration.PersonalInfo.FirstName,
            registration.PersonalInfo.LastName,
            registration.PersonalInfo.DateOfBirth,
            registration.CompetitionYear,
            cancellationToken);

        // Check for same category (true duplicate)
        var sameCategoryDuplicates = duplicates
            .Where(d => d.Id != registration.Id)
            .Where(d => d.CompetitionSelection.CategoryId == registration.CompetitionSelection.CategoryId)
            .ToList();

        if (sameCategoryDuplicates.Any())
        {
            errors.Add("This competitor is already registered for this category.");
            return;
        }

        // Check AllowMultipleInDivision rule
        if (settings == null || string.IsNullOrEmpty(registration.CompetitionSelection.DivisionId))
            return;

        var division = settings.FindDivision(registration.CompetitionSelection.DivisionId);
        var targetCategory = division?.FindCategory(registration.CompetitionSelection.CategoryId);

        if (targetCategory == null || targetCategory.AllowMultipleInDivision)
            return;

        var countingRegistrations = duplicates
            .Where(d => d.Id != registration.Id)
            .Where(d => d.CompetitionSelection.DivisionId == registration.CompetitionSelection.DivisionId)
            .Where(d => d.CompetitionSelection.CategoryId != registration.CompetitionSelection.CategoryId)
            .Where(d =>
            {
                var cat = division?.FindCategory(d.CompetitionSelection.CategoryId);
                return cat != null && !cat.AllowMultipleInDivision;
            })
            .ToList();

        if (countingRegistrations.Any())
        {
            var existingCategory = division?.FindCategory(countingRegistrations.First().CompetitionSelection.CategoryId);
            var existingCategoryName = existingCategory?.Name ?? "another category";
            errors.Add($"This competitor is already registered for {existingCategoryName} in this division. Only one non-exception category is allowed per division.");
        }
    }

    private static void ValidateCategoryEligibility(
        Registration registration,
        CompetitionSettings? settings,
        List<string> errors)
    {
        if (settings == null || string.IsNullOrEmpty(registration.CompetitionSelection.CategoryId))
            return;

        var division = settings.FindDivision(registration.CompetitionSelection.DivisionId);
        if (division == null)
        {
            errors.Add("Selected division is not valid.");
            return;
        }

        var category = division.FindCategory(registration.CompetitionSelection.CategoryId);
        if (category == null)
        {
            errors.Add("Selected category is not valid.");
            return;
        }

        if (!category.IsEnabled)
        {
            errors.Add("The selected category is not currently open for registration.");
        }

        // Age check
        if (category.MaxAgeYears.HasValue && registration.PersonalInfo.DateOfBirth != default)
        {
            var age = registration.CalculateAgeAsOf(settings.AgeCutoffDate);
            if (age > category.MaxAgeYears.Value)
            {
                errors.Add($"Competitor's age ({age}) exceeds the maximum age ({category.MaxAgeYears}) for this category as of the cutoff date ({settings.AgeCutoffDate:MMM d, yyyy}).");
            }
        }
    }

    private static void ValidatePortionChoice(
        Registration registration,
        CompetitionSettings? settings,
        List<string> errors)
    {
        if (settings == null) return;

        var division = settings.FindDivision(registration.CompetitionSelection.DivisionId);
        var category = division?.FindCategory(registration.CompetitionSelection.CategoryId);

        if (category?.PortionOption == PortionOption.TopOrBottom &&
            registration.CompetitionSelection.PortionChoice == null)
        {
            errors.Add("You must select a portion (Top or Bottom) for this category.");
        }
    }

    private static void ValidatePhoneNumbers(Registration registration, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(registration.PersonalInfo.PhoneNumber) &&
            !RegistrationFormatter.IsValidPhoneNumber(registration.PersonalInfo.PhoneNumber))
        {
            errors.Add("Competitor phone number is invalid. Please enter a valid 10-digit phone number.");
        }

        if (!string.IsNullOrWhiteSpace(registration.ParentInfo.PhoneNumber) &&
            !RegistrationFormatter.IsValidPhoneNumber(registration.ParentInfo.PhoneNumber))
        {
            errors.Add("Parent phone number is invalid. Please enter a valid 10-digit phone number.");
        }

        if (registration.TeacherInfo != null &&
            !string.IsNullOrWhiteSpace(registration.TeacherInfo.PhoneNumber) &&
            !RegistrationFormatter.IsValidPhoneNumber(registration.TeacherInfo.PhoneNumber))
        {
            errors.Add("Teacher phone number is invalid. Please enter a valid 10-digit phone number.");
        }
    }

    private static void ValidateCountry(Registration registration, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(registration.AddressInfo.Country))
            return;

        var country = LocationData.GetCountryByName(registration.AddressInfo.Country);
        if (country == null)
        {
            errors.Add("Please select a valid country (United States, Canada, or Mexico).");
        }
    }
}

public class RegistrationValidationResult
{
    public bool IsValid => !Errors.Any();
    public List<string> Errors { get; }

    public RegistrationValidationResult(IEnumerable<string> errors)
    {
        Errors = errors.ToList();
    }

    public static RegistrationValidationResult Success => new(Array.Empty<string>());
}
