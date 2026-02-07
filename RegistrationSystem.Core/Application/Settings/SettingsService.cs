using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Core.Application.Settings;
/// <summary>
/// Provides services for managing and evaluating competition registration settings, including saving settings and
/// computing registration status for divisions and categories.
/// </summary>
/// <remarks>The SettingsService coordinates validation, persistence, and status computation for competition
/// registration settings. It offers methods to determine registration availability and eligibility at the global,
/// division, and category levels, as well as to check age-based eligibility. This service is intended for use in
/// administrative dashboards and registration workflows that require up-to-date status and validation logic.</remarks>
public class SettingsService
{
    private readonly ICompetitionSettingsRepository _repository;
    private readonly CompetitionSettingsValidator _validator;

    public SettingsService(ICompetitionSettingsRepository repository)
    {
        _repository = repository;
        _validator = new CompetitionSettingsValidator();
    }

    public async Task SaveSettingsAsync(
        CompetitionSettings settings,
        CancellationToken cancellationToken = default)
    {
        _validator.Validate(settings).ThrowIfInvalid();
        await _repository.SaveAsync(settings, cancellationToken);
    }

    #region CRUD Operations

    public Task<CompetitionSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default)
        => _repository.GetAsync(cancellationToken);

    #endregion

    #region Status Computation

    /// <summary>
    /// Get complete registration status for the entire competition.
    /// Used for the admin status dashboard.
    /// </summary>
    public GlobalRegistrationStatus GetGlobalStatus(
        CompetitionSettings settings,
        DateTimeOffset now)
    {
        var isWithinGlobalWindow = IsWithinDateWindow(
            settings.RegistrationStart,
            settings.RegistrationEnd,
            now);

        var divisions = settings.Divisions
            .Select(d => GetDivisionStatus(settings, d, now))
            .ToList();

        var totalCategories = divisions.Sum(d => d.TotalCategoryCount);
        var totalOpen = divisions.Sum(d => d.OpenCategoryCount);
        var totalEnabled = divisions.Sum(d => d.EnabledCategoryCount);
        var totalOverrides = divisions.Sum(d => d.OverrideCategoryCount);

        var summary = BuildGlobalSummary(settings.RegistrationEnabled, totalOpen, totalCategories, now);

        return new GlobalRegistrationStatus
        {
            IsGloballyEnabled = settings.RegistrationEnabled,
            GlobalStart = settings.RegistrationStart,
            GlobalEnd = settings.RegistrationEnd,
            AgeCutoffDate = settings.AgeCutoffDate,
            IsWithinGlobalDateWindow = isWithinGlobalWindow,
            TotalOpenCategories = totalOpen,
            TotalCategories = totalCategories,
            TotalEnabledCategories = totalEnabled,
            CategoriesWithOverrides = totalOverrides,
            TotalDivisions = settings.Divisions.Count,
            EnabledDivisions = settings.Divisions.Count(d => d.IsEnabled),
            Divisions = divisions,
            SummaryMessage = summary
        };
    }

    /// <summary>
    /// Get registration status for a specific division.
    /// </summary>
    public DivisionRegistrationStatus GetDivisionStatus(
        CompetitionSettings settings,
        Division division,
        DateTimeOffset now)
    {
        var categories = division.Categories
            .Select(c => GetCategoryStatusInternal(settings, division, c, now))
            .ToList();

        return new DivisionRegistrationStatus
        {
            DivisionId = division.Id,
            DivisionName = division.Name,
            IsManuallyEnabled = division.IsEnabled,
            OpenCategoryCount = categories.Count(c => c.IsOpen),
            TotalCategoryCount = categories.Count,
            EnabledCategoryCount = categories.Count(c => c.IsManuallyEnabled),
            OverrideCategoryCount = categories.Count(c => c.HasOverride),
            Categories = categories
        };
    }

    /// <summary>
    /// Get registration status for a specific category.
    /// Used for both admin dashboard and registration form validation.
    /// </summary>
    public CategoryRegistrationStatus GetCategoryStatus(
        CompetitionSettings settings,
        string divisionId,
        string categoryId,
        DateTimeOffset now)
    {
        var division = settings.FindDivision(divisionId);
        if (division is null)
        {
            return new CategoryRegistrationStatus
            {
                CategoryId = categoryId,
                CategoryName = "Unknown",
                DivisionId = divisionId,
                DivisionName = "Unknown",
                IsOpen = false,
                Reason = RegistrationClosedReason.DivisionDisabled,
                StatusMessage = "Division not found.",
                StatusLabel = "Not Found"
            };
        }

        var category = division.FindCategory(categoryId);
        if (category is null)
        {
            return new CategoryRegistrationStatus
            {
                CategoryId = categoryId,
                CategoryName = "Unknown",
                DivisionId = divisionId,
                DivisionName = division.Name,
                IsOpen = false,
                Reason = RegistrationClosedReason.CategoryDisabled,
                StatusMessage = "Category not found.",
                StatusLabel = "Not Found"
            };
        }

        return GetCategoryStatusInternal(settings, division, category, now);
    }

    /// <summary>
    /// Get registration status for a specific category (internal overload).
    /// </summary>
    private CategoryRegistrationStatus GetCategoryStatusInternal(
        CompetitionSettings settings,
        Division division,
        Category category,
        DateTimeOffset now)
    {
        var hasOverride = category.RegistrationStart.HasValue || category.RegistrationEnd.HasValue;
        var effectiveStart = category.RegistrationStart ?? settings.RegistrationStart;
        var effectiveEnd = category.RegistrationEnd ?? settings.RegistrationEnd;

        // Check hierarchy from top to bottom
        var (isOpen, reason) = EvaluateRegistrationStatus(
            settings.RegistrationEnabled,
            division.IsEnabled,
            category.IsEnabled,
            effectiveStart,
            effectiveEnd,
            now);

        var (message, label) = BuildStatusMessageAndLabel(
            reason, effectiveStart, effectiveEnd, now);

        return new CategoryRegistrationStatus
        {
            CategoryId = category.Id,
            CategoryName = category.Name,
            DivisionId = division.Id,
            DivisionName = division.Name,
            IsOpen = isOpen,
            Reason = reason,
            EffectiveStart = effectiveStart,
            EffectiveEnd = effectiveEnd,
            HasOverride = hasOverride,
            IsManuallyEnabled = category.IsEnabled,
            StatusMessage = message,
            StatusLabel = label
        };
    }

    /// <summary>
    /// Simple boolean check for whether a category is open.
    /// Convenience method for registration form.
    /// </summary>
    public bool IsCategoryOpenForRegistration(
        CompetitionSettings settings,
        string divisionId,
        string categoryId,
        DateTimeOffset now)
    {
        var status = GetCategoryStatus(settings, divisionId, categoryId, now);
        return status.IsOpen;
    }

    /// <summary>
    /// Check if enabling a category is allowed given the current hierarchy state.
    /// Returns (allowed, reason) for UI feedback.
    /// </summary>
    public (bool Allowed, string? BlockingReason) CanEnableCategory(
        CompetitionSettings settings,
        string divisionId)
    {
        if (!settings.RegistrationEnabled)
        {
            return (false, "Global registration is disabled. Enable global registration first.");
        }

        var division = settings.FindDivision(divisionId);
        if (division is null)
        {
            return (false, "Division not found.");
        }

        if (!division.IsEnabled)
        {
            return (false, $"The '{division.Name}' division is disabled. Enable the division first.");
        }

        return (true, null);
    }

    /// <summary>
    /// Check if enabling a division is allowed given the current hierarchy state.
    /// Returns (allowed, reason) for UI feedback.
    /// </summary>
    public (bool Allowed, string? BlockingReason) CanEnableDivision(
        CompetitionSettings settings)
    {
        if (!settings.RegistrationEnabled)
        {
            return (false, "Global registration is disabled. Enable global registration first.");
        }

        return (true, null);
    }

    #endregion

    #region Age Eligibility

    /// <summary>
    /// Check if a person is eligible for a category based on age.
    /// </summary>
    public bool IsEligibleForCategory(
        CompetitionSettings settings,
        string divisionId,
        string categoryId,
        DateOnly birthDate,
        DateTimeOffset now)
    {
        var division = settings.FindDivision(divisionId);
        var category = division?.FindCategory(categoryId);

        if (category is null)
            return false;

        if (!IsCategoryOpenForRegistration(settings, divisionId, categoryId, now))
            return false;

        if (!category.MaxAgeYears.HasValue)
            return true;

        var age = CalculateAgeYears(birthDate, settings.AgeCutoffDate);
        return age <= category.MaxAgeYears.Value;
    }

    public int CalculateAgeYears(DateOnly birthDate, DateOnly cutoff)
    {
        var age = cutoff.Year - birthDate.Year;
        if (birthDate > cutoff.AddYears(-age))
            age--;
        return age;
    }

    #endregion

    #region Private Helpers

    private static (bool IsOpen, RegistrationClosedReason Reason) EvaluateRegistrationStatus(
        bool globalEnabled,
        bool divisionEnabled,
        bool categoryEnabled,
        DateTimeOffset? effectiveStart,
        DateTimeOffset? effectiveEnd,
        DateTimeOffset now)
    {
        // 1. Global toggle (admin control)
        if (!globalEnabled)
            return (false, RegistrationClosedReason.GloballyDisabled);

        // 2. Division enabled
        if (!divisionEnabled)
            return (false, RegistrationClosedReason.DivisionDisabled);

        // 3. Category enabled
        if (!categoryEnabled)
            return (false, RegistrationClosedReason.CategoryDisabled);

        // 4. Date window
        if (effectiveStart.HasValue && now < effectiveStart.Value)
            return (false, RegistrationClosedReason.NotStarted);

        if (effectiveEnd.HasValue && now > effectiveEnd.Value)
            return (false, RegistrationClosedReason.Ended);

        return (true, RegistrationClosedReason.Open);
    }

    private static (string Message, string Label) BuildStatusMessageAndLabel(
    RegistrationClosedReason reason,
    DateTimeOffset? effectiveStart,
    DateTimeOffset? effectiveEnd,
    DateTimeOffset now)
    {
        return reason switch
        {
            RegistrationClosedReason.Open =>
                (BuildOpenMessage(effectiveEnd, now), "Open"),

            RegistrationClosedReason.GloballyDisabled =>
                ("Registration is disabled globally.", "Closed"),

            RegistrationClosedReason.DivisionDisabled =>
                ("Division is disabled.", "Disabled"),

            RegistrationClosedReason.CategoryDisabled =>
                ("Category is disabled.", "Disabled"),

            RegistrationClosedReason.NotStarted =>
                (BuildNotStartedMessage(effectiveStart, now), "Not Started"),

            RegistrationClosedReason.Ended =>
                (BuildEndedMessage(effectiveEnd), "Ended"),

            _ => ("Unknown status.", "Unknown")
        };
    }

    private static string BuildOpenMessage(DateTimeOffset? effectiveEnd, DateTimeOffset now)
    {
        if (!effectiveEnd.HasValue)
            return "Registration is open.";

        var daysRemaining = (effectiveEnd.Value - now).Days;
        if (daysRemaining <= 0)
            return "Registration closes today.";
        if (daysRemaining == 1)
            return "Registration closes tomorrow.";
        if (daysRemaining <= 7)
            return $"Closes in {daysRemaining} days.";

        return $"Closes {effectiveEnd.Value:MMM d, yyyy}.";
    }

    private static string BuildNotStartedMessage(DateTimeOffset? effectiveStart, DateTimeOffset now)
    {
        if (!effectiveStart.HasValue)
            return "Registration has not started.";

        var daysUntil = (effectiveStart.Value - now).Days;
        if (daysUntil <= 0)
            return "Opens today.";
        if (daysUntil == 1)
            return "Opens tomorrow.";
        if (daysUntil <= 7)
            return $"Opens in {daysUntil} days.";

        return $"Opens {effectiveStart.Value:MMM d, yyyy}.";
    }

    private static string BuildEndedMessage(DateTimeOffset? effectiveEnd)
    {
        if (!effectiveEnd.HasValue)
            return "Registration has ended.";

        return $"Ended {effectiveEnd.Value:MMM d, yyyy}.";
    }

    private static string BuildGlobalSummary(
        bool isEnabled,
        int openCategories,
        int totalCategories,
        DateTimeOffset now)
    {
        if (!isEnabled)
            return "Global registration is currently disabled. No categories are accepting registrations.";

        if (openCategories == 0)
            return "Global registration is enabled, but no categories are currently open for registration.";

        if (openCategories == totalCategories)
            return $"All {totalCategories} categories are open for registration.";

        return $"{openCategories} of {totalCategories} categories are currently open for registration.";
    }

    private static bool IsWithinDateWindow(
        DateTimeOffset? start,
        DateTimeOffset? end,
        DateTimeOffset now)
    {
        if (start.HasValue && now < start.Value)
            return false;
        if (end.HasValue && now > end.Value)
            return false;
        return true;
    }
    #endregion
}
