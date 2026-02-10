using RegistrationSystem.Core.Application.Settings;
using RegistrationSystem.Core.Domain.Registrations;
using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Core.Application.Registrations;

/// <summary>
/// Provides operations for creating, updating, validating, and managing competition registrations, including user
/// eligibility checks and access to available divisions and categories.
/// </summary>
/// <remarks>The RegistrationService coordinates registration workflows by interacting with the underlying
/// repository, competition settings, and validation logic. It enforces business rules such as edit and withdrawal
/// permissions, category eligibility, and student identity validation. All methods are asynchronous and support
/// cancellation via CancellationToken. This service is not thread-safe; callers should not share instances across
/// threads without proper synchronization.</remarks>
public class RegistrationService
{
    private readonly IRegistrationRepository _repository;
    private readonly SettingsService _settingsService;
    private readonly RegistrationValidator _validator;

    public RegistrationService(
        IRegistrationRepository repository,
        SettingsService settingsService)
    {
        _repository = repository;
        _settingsService = settingsService;
        _validator = new RegistrationValidator(repository, settingsService);
    }

    public Task<Registration?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Registration>> GetUserRegistrationsAsync(
        string creatorUserId,
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _repository.GetByCreatorAndYearAsync(creatorUserId, competitionYear, cancellationToken);

    public Task<IReadOnlyList<Registration>> GetAllByYearAsync(
        int competitionYear,
        CancellationToken cancellationToken = default)
        => _repository.GetByCompetitionYearAsync(competitionYear, cancellationToken);

    public async Task<RegistrationValidationResult> CreateAndSubmitAsync(
        Registration registration,
        CancellationToken cancellationToken = default)
    {
        RegistrationFormatter.Format(registration);

        var validationResult = await _validator.ValidateForSubmissionAsync(registration, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult;

        var settings = await _settingsService.GetSettingsAsync(cancellationToken);
        if (settings != null)
        {
            registration.Cid = await GenerateCidAsync(registration, settings, cancellationToken);
        }

        registration.Status = RegistrationStatus.AwaitingReview;

        await _repository.SaveAsync(registration, cancellationToken);

        return RegistrationValidationResult.Success;
    }

    public async Task<RegistrationValidationResult> UpdateAsync(
        Registration registration,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(registration.Id, cancellationToken)
            ?? throw new InvalidOperationException("Registration not found.");

        if (!await CanEditAsync(existing, cancellationToken))
            throw new InvalidOperationException("This registration cannot be edited.");

        RegistrationFormatter.Format(registration);

        var validationResult = await _validator.ValidateAsync(registration, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult;

        // Check if division changed - regenerate CID
        var divisionChanged = existing.CompetitionSelection.DivisionId != registration.CompetitionSelection.DivisionId;
        if (divisionChanged)
        {
            var settings = await _settingsService.GetSettingsAsync(cancellationToken);
            if (settings != null)
            {
                registration.Cid = await GenerateCidAsync(registration, settings, cancellationToken);
            }
        }

        await _repository.SaveAsync(registration, cancellationToken);

        return validationResult;
    }

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

        var validationResult = await _validator.ValidateForSubmissionAsync(registration, cancellationToken);
        if (!validationResult.IsValid)
            return validationResult;

        registration.Status = RegistrationStatus.AwaitingReview;

        await _repository.SaveAsync(registration, cancellationToken);

        return validationResult;
    }

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

        if (!await CanWithdrawAsync(registration, cancellationToken))
            throw new InvalidOperationException("This registration cannot be withdrawn.");

        registration.Status = RegistrationStatus.Withdrawn;
        registration.WithdrawComment = comment;

        await _repository.SaveAsync(registration, cancellationToken);
    }

    /// <summary>
    /// Updates registration status for admin operations, bypassing edit-eligibility checks.
    /// </summary>
    public async Task AdminUpdateStatusAsync(
        Registration registration,
        CancellationToken cancellationToken = default)
    {
        _ = await _repository.GetByIdAsync(registration.Id, cancellationToken)
            ?? throw new InvalidOperationException("Registration not found.");

        await _repository.SaveAsync(registration, cancellationToken);
    }

    public async Task<bool> CanEditAsync(
        Registration registration,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);
        if (settings == null) return false;

        var category = FindCategory(settings,
            registration.CompetitionSelection.DivisionId,
            registration.CompetitionSelection.CategoryId);

        if (category == null || !category.AllowEdit)
            return false;

        return registration.Status == RegistrationStatus.AwaitingReview
            || registration.Status == RegistrationStatus.Pending;
    }

    public async Task<bool> CanWithdrawAsync(
        Registration registration,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);
        if (settings == null) return false;

        var category = FindCategory(settings,
            registration.CompetitionSelection.DivisionId,
            registration.CompetitionSelection.CategoryId);

        if (category == null || !category.AllowWithdraw)
            return false;

        return registration.Status != RegistrationStatus.Withdrawn
            && registration.Status != RegistrationStatus.Disqualified;
    }

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

        if (targetCategory.AllowMultipleInDivision)
            return (true, null);

        var existingInDivision = await _repository.GetByCreatorDivisionAndYearAsync(
            creatorUserId, divisionId, competitionYear, cancellationToken);

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

    public async Task<IReadOnlyList<Division>> GetAvailableDivisionsAsync(
        CancellationToken cancellationToken = default)
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

    public async Task<(bool IsValid, string? Error)> ValidateStudentIdentityAsync(
        string userId,
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        CancellationToken cancellationToken = default)
    {
        var existingRegistrations = await _repository.GetByCreatorUserIdAsync(userId, cancellationToken);

        if (!existingRegistrations.Any())
            return (true, null);

        var first = existingRegistrations.First();

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

    private async Task<string> GenerateCidAsync(
        Registration registration,
        CompetitionSettings settings,
        CancellationToken cancellationToken = default)
    {
        var division = settings.FindDivision(registration.CompetitionSelection.DivisionId);
        var divisionLetter = division?.Name?.FirstOrDefault().ToString().ToUpperInvariant() ?? "X";

        var stateCode = settings.CidConfiguration.GetStateCode(registration.AddressInfo.StateProvince);

        var prefix = $"{divisionLetter}{stateCode}";

        var maxSequence = await _repository.GetMaxCidSequenceAsync(
            registration.CompetitionYear,
            prefix,
            cancellationToken);

        var nextSequence = maxSequence + 1;

        return $"{prefix}{nextSequence:D3}";
    }

    // Helper methods

    private static bool IsCategoryOpen(Category category, CompetitionSettings settings, DateTimeOffset now)
    {
        var start = category.RegistrationStart ?? settings.RegistrationStart;
        var end = category.RegistrationEnd ?? settings.RegistrationEnd;

        if (!start.HasValue || !end.HasValue)
            return true;

        return now >= start.Value && now <= end.Value;
    }

    private static bool IsAgeEligible(DateOnly dob, Category category, DateOnly ageCutoffDate)
    {
        if (!category.MaxAgeYears.HasValue)
            return true;

        var age = ageCutoffDate.Year - dob.Year;
        if (ageCutoffDate < dob.AddYears(age))
            age--;

        return age <= category.MaxAgeYears.Value;
    }

    private static Category? FindCategory(CompetitionSettings settings, string divisionId, string categoryId)
    {
        var division = settings.FindDivision(divisionId);
        return division?.FindCategory(categoryId);
    }
}
