using RegistrationSystem.Core.Application.Settings;
using RegistrationSystem.Core.Domain.Registrations;
using RegistrationSystem.Core.Domain.Settings;
using System.Text.RegularExpressions;

namespace RegistrationSystem.Core.Application.Registrations;

/// <summary>
/// Service for managing competitor registrations.
/// </summary>
public partial class RegistrationService
{
    private readonly IRegistrationRepository _repository;
    private readonly SettingsService _settingsService;

    public RegistrationService(IRegistrationRepository repository, SettingsService settingsService)
    {
        _repository = repository;
        _settingsService = settingsService;
    }

    #region CRUD Operations

    /// <summary>
    /// Gets a registration by ID.
    /// </summary>
    public Task<Registration?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    /// <summary>
    /// Gets all registrations for a user in a competition year.
    /// </summary>
    public Task<IReadOnlyList<Registration>> GetUserRegistrationsAsync(
        string creatorUserId,
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _repository.GetByCreatorAndYearAsync(creatorUserId, competitionYear, cancellationToken);

    /// <summary>
    /// Gets all registrations for a competition year (admin).
    /// </summary>
    public Task<IReadOnlyList<Registration>> GetAllByYearAsync(
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _repository.GetByCompetitionYearAsync(competitionYear, cancellationToken);

    #endregion

    #region Create & Update

    /// <summary>
    /// Updates a registration with validation and formatting.
    /// Regenerates CID if division changes.
    /// </summary>
    public async Task<RegistrationValidationResult> UpdateAsync(
        Registration registration,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(registration.Id, cancellationToken)
            ?? throw new InvalidOperationException("Registration not found.");

        if (!existing.CanEdit)
            throw new InvalidOperationException("This registration cannot be edited.");

        // Format and sanitize input
        FormatRegistration(registration);

        // Validate
        var validationResult = await ValidateAsync(registration, cancellationToken);

        if (validationResult.IsValid)
        {
            // Check if division changed - need to regenerate CID
            var divisionChanged = existing.CompetitionSelection.DivisionId != registration.CompetitionSelection.DivisionId;

            if (divisionChanged)
            {
                var settings = await _settingsService.GetSettingsAsync(cancellationToken);
                if (settings != null)
                {
                    registration.Cid = await GenerateCidAsync(registration, settings, cancellationToken);
                }
            }

            registration.UpdatedAt = DateTimeOffset.UtcNow;
            await _repository.SaveAsync(registration, cancellationToken);
        }

        return validationResult;
    }

    /// <summary>
    /// Resubmits a registration that was sent back for corrections (Pending status).
    /// </summary>
    public async Task<RegistrationValidationResult> ResubmitAsync(
        string registrationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var registration = await _repository.GetByIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("Registration not found.");

        if (registration.CreatorUserId != userId)
            throw new InvalidOperationException("You do not have permission to submit this registration.");

        if (registration.Status != RegistrationStatus.Pending)
            throw new InvalidOperationException("Only registrations with Pending status can be resubmitted.");

        // Full validation before submission
        var validationResult = await ValidateForSubmissionAsync(registration, cancellationToken);

        if (validationResult.IsValid)
        {
            registration.Status = RegistrationStatus.AwaitingReview;
            registration.SubmittedAt = DateTimeOffset.UtcNow;
            registration.UpdatedAt = DateTimeOffset.UtcNow;
            await _repository.SaveAsync(registration, cancellationToken);
        }

        return validationResult;
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validates a registration (basic validation for saving drafts).
    /// </summary>
    public async Task<RegistrationValidationResult> ValidateAsync(
        Registration registration,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);

        // Check for duplicate competitor in SAME CATEGORY (true duplicate)
        if (!string.IsNullOrWhiteSpace(registration.PersonalInfo.FirstName) &&
            !string.IsNullOrWhiteSpace(registration.PersonalInfo.LastName) &&
            registration.PersonalInfo.DateOfBirth != default &&
            !string.IsNullOrEmpty(registration.CompetitionSelection.CategoryId))
        {
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
            }
        }

        // Check AllowMultipleInDivision rule
        if (!string.IsNullOrEmpty(registration.CompetitionSelection.DivisionId) &&
            !string.IsNullOrEmpty(registration.CompetitionSelection.CategoryId) &&
            settings != null)
        {
            var division = settings.FindDivision(registration.CompetitionSelection.DivisionId);
            var targetCategory = division?.FindCategory(registration.CompetitionSelection.CategoryId);

            // If the target category is NOT an exception (AllowMultiple = false),
            // check if there are existing "counting" registrations in the division
            if (targetCategory != null && !targetCategory.AllowMultipleInDivision)
            {
                var duplicates = await _repository.FindDuplicatesAsync(
                    registration.PersonalInfo.FirstName,
                    registration.PersonalInfo.LastName,
                    registration.PersonalInfo.DateOfBirth,
                    registration.CompetitionYear,
                    cancellationToken);

                // Find registrations in same division that are NOT exceptions (AllowMultiple = false)
                var countingRegistrations = duplicates
                    .Where(d => d.Id != registration.Id)
                    .Where(d => d.CompetitionSelection.DivisionId == registration.CompetitionSelection.DivisionId)
                    .Where(d => d.CompetitionSelection.CategoryId != registration.CompetitionSelection.CategoryId)
                    .Where(d =>
                    {
                        var cat = division?.FindCategory(d.CompetitionSelection.CategoryId);
                        // Only count registrations for categories that are NOT exceptions
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
        }

        // Check category eligibility
        if (!string.IsNullOrEmpty(registration.CompetitionSelection.CategoryId) && settings != null)
        {
            var categoryResult = ValidateCategoryEligibility(registration, settings);
            errors.AddRange(categoryResult);
        }

        // Validate phone numbers if provided
        if (!string.IsNullOrWhiteSpace(registration.PersonalInfo.PhoneNumber))
        {
            if (!IsValidPhoneNumber(registration.PersonalInfo.PhoneNumber))
                errors.Add("Competitor phone number is invalid. Please enter a valid 10-digit phone number.");
        }

        if (!string.IsNullOrWhiteSpace(registration.ParentInfo.PhoneNumber))
        {
            if (!IsValidPhoneNumber(registration.ParentInfo.PhoneNumber))
                errors.Add("Parent phone number is invalid. Please enter a valid 10-digit phone number.");
        }

        if (registration.TeacherInfo != null && !string.IsNullOrWhiteSpace(registration.TeacherInfo.PhoneNumber))
        {
            if (!IsValidPhoneNumber(registration.TeacherInfo.PhoneNumber))
                errors.Add("Teacher phone number is invalid. Please enter a valid 10-digit phone number.");
        }

        // Validate country
        if (!string.IsNullOrWhiteSpace(registration.AddressInfo.Country))
        {
            var country = LocationData.GetCountryByName(registration.AddressInfo.Country);
            if (country == null)
            {
                errors.Add("Please select a valid country (United States, Canada, or Mexico).");
            }
        }

        return new RegistrationValidationResult(errors);
    }

    /// <summary>
    /// Validates a registration for submission (stricter validation).
    /// </summary>
    public async Task<RegistrationValidationResult> ValidateForSubmissionAsync(
        Registration registration,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);

        // Personal Info
        if (string.IsNullOrWhiteSpace(registration.PersonalInfo.FirstName))
            errors.Add("First name is required.");
        if (string.IsNullOrWhiteSpace(registration.PersonalInfo.LastName))
            errors.Add("Last name is required.");
        if (registration.PersonalInfo.DateOfBirth == default)
            errors.Add("Date of birth is required.");

        // Address Info
        if (string.IsNullOrWhiteSpace(registration.AddressInfo.Country))
            errors.Add("Country is required.");
        if (string.IsNullOrWhiteSpace(registration.AddressInfo.StateProvince))
            errors.Add("State/Province is required.");
        if (string.IsNullOrWhiteSpace(registration.AddressInfo.City))
            errors.Add("City is required.");

        // Competition Selection
        if (string.IsNullOrWhiteSpace(registration.CompetitionSelection.DivisionId))
            errors.Add("Division is required.");
        if (string.IsNullOrWhiteSpace(registration.CompetitionSelection.CategoryId))
            errors.Add("Category is required.");

        // Parent Info
        if (string.IsNullOrWhiteSpace(registration.ParentInfo.FirstName))
            errors.Add("Parent/Guardian first name is required.");
        if (string.IsNullOrWhiteSpace(registration.ParentInfo.LastName))
            errors.Add("Parent/Guardian last name is required.");
        if (string.IsNullOrWhiteSpace(registration.ParentInfo.PhoneNumber))
            errors.Add("Parent/Guardian phone number is required.");

        // Teacher Info (Required)
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

        // Terms
        if (!registration.TermsAccepted)
            errors.Add("You must accept the terms and conditions.");

        // Run basic validation
        var basicValidation = await ValidateAsync(registration, cancellationToken);
        errors.AddRange(basicValidation.Errors);

        // Category-specific validation
        if (settings != null)
        {
            var category = FindCategory(settings, registration.CompetitionSelection.DivisionId, registration.CompetitionSelection.CategoryId);

            if (category != null)
            {
                // Portion choice validation
                if (category.PortionOption == PortionOption.TopOrBottom &&
                    registration.CompetitionSelection.PortionChoice == null)
                {
                    errors.Add("You must select a portion (Top or Bottom) for this category.");
                }

                // Video requirement (Phase 2 - just check if required for now)
                // if (category.RequiresVideo && registration.FileUploadInfo.Video == null)
                //     errors.Add("A video upload is required for this category.");
            }
        }

        return new RegistrationValidationResult(errors);
    }

    /// <summary>
    /// Validates category eligibility (age, multiple registration rules, etc.).
    /// </summary>
    private List<string> ValidateCategoryEligibility(Registration registration, CompetitionSettings settings)
    {
        var errors = new List<string>();
        var division = settings.FindDivision(registration.CompetitionSelection.DivisionId);

        if (division == null)
        {
            errors.Add("Selected division is not valid.");
            return errors;
        }

        var category = division.FindCategory(registration.CompetitionSelection.CategoryId);

        if (category == null)
        {
            errors.Add("Selected category is not valid.");
            return errors;
        }

        if (!category.IsEnabled)
        {
            errors.Add("The selected category is not currently open for registration.");
        }

        // Age check - use AgeCutoffDate from settings
        if (category.MaxAgeYears.HasValue && registration.PersonalInfo.DateOfBirth != default)
        {
            var age = registration.CalculateAgeAsOf(settings.AgeCutoffDate);
            if (age > category.MaxAgeYears.Value)
            {
                errors.Add($"Competitor's age ({age}) exceeds the maximum age ({category.MaxAgeYears}) for this category as of the cutoff date ({settings.AgeCutoffDate:MMM d, yyyy}).");
            }
        }

        return errors;
    }

    /// <summary>
    /// Checks if a user can register for a category (multiple registration rules).
    /// </summary>
    public async Task<(bool CanRegister, string? Reason)> CanRegisterForCategoryAsync(
        string creatorUserId,
        string divisionId,
        string categoryId,
        int competitionYear,
        string? excludeRegistrationId = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);
        if (settings == null)
            return (false, "Competition settings not found.");

        var division = settings.FindDivision(divisionId);
        if (division == null)
            return (false, "Division not found.");

        var targetCategory = division.FindCategory(categoryId);
        if (targetCategory == null)
            return (false, "Category not found.");

        if (!targetCategory.IsEnabled)
            return (false, "This category is not open for registration.");

        // If the target category is an exception (AllowMultiple = true), always allow
        if (targetCategory.AllowMultipleInDivision)
            return (true, null);

        // Target category is NOT an exception - check for existing "counting" registrations
        var existingInDivision = await _repository.GetByCreatorDivisionAndYearAsync(
            creatorUserId, divisionId, competitionYear, cancellationToken);

        // Find registrations that are NOT exceptions (AllowMultiple = false)
        var countingRegistrations = existingInDivision
            .Where(r => r.Id != excludeRegistrationId)
            .Where(r =>
            {
                var cat = division.FindCategory(r.CompetitionSelection.CategoryId);
                return cat != null && !cat.AllowMultipleInDivision;
            })
            .ToList();

        if (countingRegistrations.Any())
        {
            var existingCategory = division.FindCategory(countingRegistrations.First().CompetitionSelection.CategoryId);
            var existingCategoryName = existingCategory?.Name ?? "another category";
            return (false, $"Already registered for {existingCategoryName} in this division.");
        }

        return (true, null);
    }

    /// <summary>
    /// Gets available divisions for registration based on settings.
    /// </summary>
    public async Task<IReadOnlyList<Division>> GetAvailableDivisionsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);
        if (settings == null || !settings.RegistrationEnabled)
            return Array.Empty<Division>();

        var now = DateTimeOffset.UtcNow;

        return settings.Divisions
            .Where(d => d.IsEnabled)
            .Where(d => d.Categories.Any(c => c.IsEnabled && IsCategoryOpen(c, settings, now)))
            .ToList();
    }

    /// <summary>
    /// Gets available categories for a division based on settings and age.
    /// </summary>
    public async Task<IReadOnlyList<Category>> GetAvailableCategoriesAsync(
        string divisionId,
        DateOnly? competitorDob = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);
        if (settings == null)
            return Array.Empty<Category>();

        var division = settings.FindDivision(divisionId);
        if (division == null || !division.IsEnabled)
            return Array.Empty<Category>();

        var now = DateTimeOffset.UtcNow;

        return division.Categories
            .Where(c => c.IsEnabled && IsCategoryOpen(c, settings, now))
            .Where(c => !competitorDob.HasValue || competitorDob.Value == default ||
                        IsAgeEligible(competitorDob.Value, c, settings.AgeCutoffDate))
            .ToList();
    }

    /// <summary>
    /// Checks if a category is currently open for registration.
    /// </summary>
    private static bool IsCategoryOpen(Category category, CompetitionSettings settings, DateTimeOffset now)
    {
        var start = category.RegistrationStart ?? settings.RegistrationStart;
        var end = category.RegistrationEnd ?? settings.RegistrationEnd;

        if (!start.HasValue || !end.HasValue)
            return true; // No restrictions

        return now >= start.Value && now <= end.Value;
    }

    /// <summary>
    /// Checks if a competitor is age-eligible for a category.
    /// </summary>
    private static bool IsAgeEligible(DateOnly dob, Category category, DateOnly ageCutoffDate)
    {
        if (!category.MaxAgeYears.HasValue)
            return true;

        var age = ageCutoffDate.Year - dob.Year;
        if (ageCutoffDate < dob.AddYears(age))
            age--;

        return age <= category.MaxAgeYears.Value;
    }

    /// <summary>
    /// Validates that a student is registering for themselves (same name/DOB as prior registrations).
    /// Only applies to users with UserType = Student.
    /// </summary>
    public async Task<(bool IsValid, string? Error)> ValidateStudentIdentityAsync(
        string userId,
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken = default)
    {
        // Get any existing registration by this user (any year)
        var existingRegistrations = await _repository.GetByCreatorUserIdAsync(userId, cancellationToken);

        if (!existingRegistrations.Any())
            return (true, null); // First registration, no lock

        var first = existingRegistrations.First();

        // Compare (case-insensitive, trimmed)
        var nameMatches =
            first.PersonalInfo.FirstName.Equals(firstName.Trim(), StringComparison.OrdinalIgnoreCase) &&
            first.PersonalInfo.LastName.Equals(lastName.Trim(), StringComparison.OrdinalIgnoreCase);

        var dobMatches = first.PersonalInfo.DateOfBirth == dateOfBirth;

        if (!nameMatches || !dobMatches)
        {
            return (false, "As a student, you can only register for yourself.");
        }

        return (true, null);
    }

    #endregion

    #region Formatting Helpers

    /// <summary>
    /// Formats and sanitizes all registration fields.
    /// </summary>
    private static void FormatRegistration(Registration registration)
    {
        // Personal Info
        registration.PersonalInfo.FirstName = FormatName(registration.PersonalInfo.FirstName);
        registration.PersonalInfo.MiddleName = FormatName(registration.PersonalInfo.MiddleName);
        registration.PersonalInfo.LastName = FormatName(registration.PersonalInfo.LastName);
        registration.PersonalInfo.PreferredName = FormatName(registration.PersonalInfo.PreferredName);
        registration.PersonalInfo.PhoneNumber = FormatPhoneNumber(registration.PersonalInfo.PhoneNumber);

        // Address Info - don't format country/state as they come from dropdowns
        registration.AddressInfo.City = FormatName(registration.AddressInfo.City);

        // Parent Info
        registration.ParentInfo.FirstName = FormatName(registration.ParentInfo.FirstName);
        registration.ParentInfo.LastName = FormatName(registration.ParentInfo.LastName);
        registration.ParentInfo.PhoneNumber = FormatPhoneNumber(registration.ParentInfo.PhoneNumber);

        // Teacher Info
        if (registration.TeacherInfo != null)
        {
            registration.TeacherInfo.FirstName = FormatName(registration.TeacherInfo.FirstName);
            registration.TeacherInfo.LastName = FormatName(registration.TeacherInfo.LastName);
            registration.TeacherInfo.PhoneNumber = FormatPhoneNumber(registration.TeacherInfo.PhoneNumber);
            registration.TeacherInfo.Institution = FormatName(registration.TeacherInfo.Institution);
        }
    }

    /// <summary>
    /// Formats a name: trims, capitalizes first letter of each word.
    /// </summary>
    private static string FormatName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var words = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var formatted = words.Select(word =>
        {
            if (word.Length == 1)
                return char.ToUpper(word[0]).ToString();

            return char.ToUpper(word[0]) + word[1..].ToLower();
        });

        return string.Join(" ", formatted);
    }

    /// <summary>
    /// Validates a phone number (10 digits for US/Canada/Mexico).
    /// </summary>
    public static bool IsValidPhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return true; // Empty is valid (optional field)

        var digits = PhoneDigitsRegex().Replace(phone, "");

        // Remove leading 1 for US/Canada
        if (digits.Length == 11 && digits.StartsWith('1'))
            digits = digits[1..];

        // Must be exactly 10 digits
        return digits.Length == 10;
    }

    /// <summary>
    /// Formats a phone number to (XXX) XXX-XXXX format.
    /// </summary>
    public static string FormatPhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;

        // Remove all non-digit characters
        var digits = PhoneDigitsRegex().Replace(phone, "");

        // Remove leading 1 for US/Canada
        if (digits.Length == 11 && digits.StartsWith('1'))
            digits = digits[1..];

        // Format as (XXX) XXX-XXXX for 10-digit numbers
        if (digits.Length == 10)
            return $"({digits[..3]}) {digits[3..6]}-{digits[6..]}";

        // Return cleaned digits for other formats
        return digits;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex PhoneDigitsRegex();

    /// <summary>
    /// Finds a category within settings.
    /// </summary>
    private static Category? FindCategory(CompetitionSettings settings, string divisionId, string categoryId)
    {
        var division = settings.FindDivision(divisionId);
        return division?.FindCategory(categoryId);
    }

    #endregion

    #region Status Management

    /// <summary>
    /// Creates and immediately submits a registration (no draft).
    /// </summary>
    public async Task<RegistrationValidationResult> CreateAndSubmitAsync(
        Registration registration,
        CancellationToken cancellationToken = default)
    {
        // Format all fields
        FormatRegistration(registration);

        // Validate everything
        var errors = new List<string>();
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);

        // Personal Info
        if (string.IsNullOrWhiteSpace(registration.PersonalInfo.FirstName))
            errors.Add("First name is required.");
        if (string.IsNullOrWhiteSpace(registration.PersonalInfo.LastName))
            errors.Add("Last name is required.");
        if (registration.PersonalInfo.DateOfBirth == default)
            errors.Add("Date of birth is required.");

        // Address Info
        if (string.IsNullOrWhiteSpace(registration.AddressInfo.Country))
            errors.Add("Country is required.");
        if (string.IsNullOrWhiteSpace(registration.AddressInfo.StateProvince))
            errors.Add("State/Province is required.");
        if (string.IsNullOrWhiteSpace(registration.AddressInfo.City))
            errors.Add("City is required.");

        // Competition Selection
        if (string.IsNullOrWhiteSpace(registration.CompetitionSelection.DivisionId))
            errors.Add("Division is required.");
        if (string.IsNullOrWhiteSpace(registration.CompetitionSelection.CategoryId))
            errors.Add("Category is required.");

        // Parent Info
        if (string.IsNullOrWhiteSpace(registration.ParentInfo.FirstName))
            errors.Add("Parent/Guardian first name is required.");
        if (string.IsNullOrWhiteSpace(registration.ParentInfo.LastName))
            errors.Add("Parent/Guardian last name is required.");
        if (string.IsNullOrWhiteSpace(registration.ParentInfo.PhoneNumber))
            errors.Add("Parent/Guardian phone number is required.");
        else if (!IsValidPhoneNumber(registration.ParentInfo.PhoneNumber))
            errors.Add("Parent/Guardian phone number is invalid.");

        // Teacher Info (Required)
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
            else if (!IsValidPhoneNumber(registration.TeacherInfo.PhoneNumber))
                errors.Add("Teacher phone number is invalid.");
            if (string.IsNullOrWhiteSpace(registration.TeacherInfo.Institution))
                errors.Add("Institution name is required.");
        }

        // Terms
        if (!registration.TermsAccepted)
            errors.Add("You must accept the terms and conditions.");

        // Validate phone numbers
        if (!string.IsNullOrWhiteSpace(registration.PersonalInfo.PhoneNumber) &&
            !IsValidPhoneNumber(registration.PersonalInfo.PhoneNumber))
            errors.Add("Competitor phone number is invalid.");

        // Validate country
        if (!string.IsNullOrWhiteSpace(registration.AddressInfo.Country))
        {
            var country = LocationData.GetCountryByName(registration.AddressInfo.Country);
            if (country == null)
                errors.Add("Please select a valid country (United States, Canada, or Mexico).");
        }

        // Check for duplicates - same CATEGORY (true duplicate)
        if (!string.IsNullOrWhiteSpace(registration.PersonalInfo.FirstName) &&
            !string.IsNullOrWhiteSpace(registration.PersonalInfo.LastName) &&
            registration.PersonalInfo.DateOfBirth != default &&
            !string.IsNullOrEmpty(registration.CompetitionSelection.CategoryId))
        {
            var duplicates = await _repository.FindDuplicatesAsync(
                registration.PersonalInfo.FirstName,
                registration.PersonalInfo.LastName,
                registration.PersonalInfo.DateOfBirth,
                registration.CompetitionYear,
                cancellationToken);

            // Check for same category (true duplicate)
            var sameCategoryDuplicates = duplicates
                .Where(d => d.CompetitionSelection.CategoryId == registration.CompetitionSelection.CategoryId)
                .ToList();

            if (sameCategoryDuplicates.Any())
            {
                errors.Add("This competitor is already registered for this category.");
            }

            // Check AllowMultipleInDivision rule
            if (settings != null && !string.IsNullOrEmpty(registration.CompetitionSelection.DivisionId))
            {
                var division = settings.FindDivision(registration.CompetitionSelection.DivisionId);
                var targetCategory = division?.FindCategory(registration.CompetitionSelection.CategoryId);

                // If the target category is NOT an exception (AllowMultiple = false),
                // check if there are existing "counting" registrations in the division
                if (targetCategory != null && !targetCategory.AllowMultipleInDivision)
                {
                    // Find registrations in same division that are NOT exceptions
                    var countingRegistrations = duplicates
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
            }
        }

        // Category validation
        if (settings != null && !string.IsNullOrEmpty(registration.CompetitionSelection.CategoryId))
        {
            var categoryErrors = ValidateCategoryEligibility(registration, settings);
            errors.AddRange(categoryErrors);

            var category = FindCategory(settings, registration.CompetitionSelection.DivisionId, registration.CompetitionSelection.CategoryId);
            if (category?.PortionOption == PortionOption.TopOrBottom && registration.CompetitionSelection.PortionChoice == null)
            {
                errors.Add("You must select a portion (Top or Bottom) for this category.");
            }
        }

        if (errors.Any())
        {
            return new RegistrationValidationResult(errors);
        }

        // Generate Competitor ID (CID)
        if (settings != null)
        {
            registration.Cid = await GenerateCidAsync(registration, settings, cancellationToken);
        }

        // All good - save with AwaitingReview status
        registration.Status = RegistrationStatus.AwaitingReview;
        registration.SubmittedAt = DateTimeOffset.UtcNow;
        registration.CreatedAt = DateTimeOffset.UtcNow;
        registration.UpdatedAt = DateTimeOffset.UtcNow;
        registration.TermsAcceptedAt = DateTimeOffset.UtcNow;

        await _repository.SaveAsync(registration, cancellationToken);

        return RegistrationValidationResult.Success;
    }

    /// <summary>
    /// Withdraws a registration.
    /// </summary>
    public async Task WithdrawAsync(
        string registrationId,
        string userId,
        string comment,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var registration = await _repository.GetByIdAsync(registrationId, cancellationToken)
            ?? throw new InvalidOperationException("Registration not found.");

        if (!isAdmin && registration.CreatorUserId != userId)
            throw new InvalidOperationException("You do not have permission to withdraw this registration.");

        if (!registration.CanWithdraw)
            throw new InvalidOperationException("This registration cannot be withdrawn.");

        // Check if withdrawal is allowed for this category
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);
        if (settings != null && !isAdmin)
        {
            var division = settings.FindDivision(registration.CompetitionSelection.DivisionId);
            var category = division?.FindCategory(registration.CompetitionSelection.CategoryId);
            if (category != null && !category.AllowWithdraw)
            {
                throw new InvalidOperationException("Withdrawals are not currently allowed for this category.");
            }
        }

        registration.Status = RegistrationStatus.Withdrawn;
        registration.WithdrawComment = comment;
        registration.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.SaveAsync(registration, cancellationToken);
    }

    #endregion

    #region CID Generation

    /// <summary>
    /// Generates a unique Competitor ID (CID) for a registration.
    /// Format: [DivisionLetter][StateCode][3-digit sequence]
    /// Example: M3001 = Memorization (M), MN state (code 3), competitor #1
    /// </summary>
    private async Task<string> GenerateCidAsync(
        Registration registration,
        CompetitionSettings settings,
        CancellationToken cancellationToken = default)
    {
        // Get division letter (first letter of division name, uppercase)
        var division = settings.FindDivision(registration.CompetitionSelection.DivisionId);
        var divisionLetter = division?.Name?.FirstOrDefault().ToString().ToUpperInvariant() ?? "X";

        // Get state code from CID configuration
        var stateCode = settings.CidConfiguration.GetStateCode(registration.AddressInfo.StateProvince);

        // Get the prefix (division letter + state code)
        var prefix = $"{divisionLetter}{stateCode}";

        // Get the next sequence number for this prefix
        var maxSequence = await _repository.GetMaxCidSequenceAsync(
            registration.CompetitionYear,
            prefix,
            cancellationToken);

        var nextSequence = maxSequence + 1;

        // Format as 3-digit sequence (001-999)
        return $"{prefix}{nextSequence:D3}";
    }

    #endregion
}

/// <summary>
/// Result of registration validation.
/// </summary>
public class RegistrationValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<string> Errors { get; }

    public RegistrationValidationResult(IEnumerable<string> errors)
    {
        Errors = errors.ToList();
    }

    public static RegistrationValidationResult Success => new(Array.Empty<string>());
}