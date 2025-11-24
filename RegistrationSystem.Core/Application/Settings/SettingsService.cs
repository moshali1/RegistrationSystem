using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Core.Application.Settings;

public class SettingsService
{
    private readonly ICompetitionSettingsRepository _repository;

    public SettingsService(ICompetitionSettingsRepository repository)
    {
        _repository = repository;
    }

    public Task<CompetitionSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default)
        => _repository.GetAsync(cancellationToken);

    public async Task SetGlobalRegistrationAsync(
        bool enabled,
        DateTimeOffset? start,
        DateTimeOffset? end,
        CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetAsync(cancellationToken);

        if (enabled)
        {
            if (!start.HasValue || !end.HasValue)
                throw new ArgumentException("Start and end dates are required when enabling registration.");

            if (end <= start)
                throw new ArgumentException("Global registration end must be after start.");
        }

        settings.RegistrationEnabled = enabled;
        settings.RegistrationStart = start;
        settings.RegistrationEnd = end;

        await _repository.SaveAsync(settings, cancellationToken);
    }

    public async Task SetAgeCutoffDateAsync(
        DateOnly cutoffDate,
        CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetAsync(cancellationToken);
        settings.AgeCutoffDate = cutoffDate;
        await _repository.SaveAsync(settings, cancellationToken);
    }

    public async Task UpdateCategoryAsync(
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DivisionId))
            throw new ArgumentException("DivisionId is required.", nameof(request.DivisionId));

        if (string.IsNullOrWhiteSpace(request.CategoryId))
            throw new ArgumentException("CategoryId is required.", nameof(request.CategoryId));

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Category name cannot be empty.", nameof(request.Name));

        if (request.MaxAgeYears is < 0)
            throw new ArgumentException("Max age cannot be negative.", nameof(request.MaxAgeYears));

        if (request.RegistrationStart.HasValue &&
            request.RegistrationEnd.HasValue &&
            request.RegistrationEnd <= request.RegistrationStart)
        {
            throw new ArgumentException("Category registration end must be after start.");
        }

        var settings = await _repository.GetAsync(cancellationToken);

        var division = settings.FindDivision(request.DivisionId)
            ?? throw new InvalidOperationException($"Division {request.DivisionId} not found.");

        var category = division.FindCategory(request.CategoryId)
            ?? throw new InvalidOperationException($"Category {request.CategoryId} not found.");

        category.Name = request.Name;
        category.IsEnabled = request.IsEnabled;
        category.MaxAgeYears = request.MaxAgeYears;
        category.RegistrationStart = request.RegistrationStart;
        category.RegistrationEnd = request.RegistrationEnd;
        category.PortionOption = request.PortionOption;

        await _repository.SaveAsync(settings, cancellationToken);
    }

    public async Task<bool> IsRegistrationOpenForCategoryAsync(
        string divisionId,
        string categoryId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetAsync(cancellationToken);

        if (!settings.RegistrationEnabled)
            return false;

        var division = settings.FindDivision(divisionId);
        var category = division?.FindCategory(categoryId);

        if (category is null || !category.IsEnabled)
            return false;

        var start = category.RegistrationStart ?? settings.RegistrationStart;
        var end = category.RegistrationEnd ?? settings.RegistrationEnd;

        if (start.HasValue && now < start.Value)
            return false;

        if (end.HasValue && now > end.Value)
            return false;

        return true;
    }

    public async Task<bool> IsEligibleForCategoryAsync(
        string divisionId,
        string categoryId,
        DateOnly birthDate,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetAsync(cancellationToken);

        var division = settings.FindDivision(divisionId);
        var category = division?.FindCategory(categoryId);

        if (category is null)
            return false;

        // Check registration window first
        if (!await IsRegistrationOpenForCategoryAsync(
                divisionId, categoryId, now, cancellationToken))
            return false;

        if (!category.MaxAgeYears.HasValue)
            return true; // no age limit, just registration rules

        var age = CalculateAgeYears(birthDate, settings.AgeCutoffDate);
        return age <= category.MaxAgeYears.Value;
    }

    private static int CalculateAgeYears(DateOnly birthDate, DateOnly cutoff)
    {
        var age = cutoff.Year - birthDate.Year;
        if (birthDate > cutoff.AddYears(-age))
            age--;
        return age;
    }
}
