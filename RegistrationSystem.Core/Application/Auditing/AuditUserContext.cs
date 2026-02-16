using RegistrationSystem.Core.Domain.Auditing;
using System.Reflection;

namespace RegistrationSystem.Core.Application.Auditing;

/// <summary>
/// Represents the current user context for audit logging.
/// </summary>
public class AuditUserContext
{
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public bool IsAuthenticated { get; set; }
}

/// <summary>
/// Search criteria for querying audit entries.
/// </summary>
public class AuditSearchCriteria
{
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public AuditAction? Action { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? UserId { get; set; }
    public string? SearchText { get; set; }
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 50;
}

/// <summary>
/// Daily statistics for audit entries.
/// </summary>
public class AuditDailyStats
{
    public DateOnly Date { get; set; }
    public int TotalActions { get; set; }
    public Dictionary<AuditAction, int> ActionCounts { get; set; } = new();
    public Dictionary<string, int> EntityTypeCounts { get; set; } = new();
    public int UniqueUsers { get; set; }
    public int SystemActions { get; set; }
}

/// <summary>
/// Repository interface for audit entries.
/// </summary>
public interface IAuditRepository
{
    Task SaveAsync(AuditEntry entry, CancellationToken cancellationToken = default);
    Task SaveManyAsync(IEnumerable<AuditEntry> entries, CancellationToken cancellationToken = default);
    Task<AuditEntry?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEntry>> GetByEntityAsync(string entityType, string entityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEntry>> SearchAsync(AuditSearchCriteria criteria, CancellationToken cancellationToken = default);
    Task<int> CountAsync(AuditSearchCriteria criteria, CancellationToken cancellationToken = default);
    Task<AuditDailyStats> GetDailyStatsAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditDailyStats>> GetStatsRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<long> DeleteByEntityAsync(string entityType, string entityId, CancellationToken cancellationToken = default);
    Task UpdateSummaryAsync(string id, string newSummary, CancellationToken cancellationToken = default);
    Task DeleteByIdAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for logging audit entries and tracking changes to entities.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Sets the current user context for audit logging.
    /// Call this at the start of each request/operation.
    /// </summary>
    void SetUserContext(AuditUserContext? context);

    /// <summary>
    /// Logs a simple audit entry.
    /// </summary>
    Task LogAsync(
        AuditAction action,
        string entityType,
        string entityId,
        string? summary = null,
        string? entityDescription = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a system action (not triggered by a user).
    /// </summary>
    Task LogSystemActionAsync(
        AuditAction action,
        string entityType,
        string entityId,
        string? summary = null,
        string? entityDescription = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs field-level changes by comparing two objects.
    /// </summary>
    Task LogChangesAsync<T>(
        string entityType,
        string entityId,
        T? oldValue,
        T newValue,
        string? entityDescription = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Logs specific field changes.
    /// </summary>
    Task LogFieldChangesAsync(
        string entityType,
        string entityId,
        IEnumerable<FieldChange> changes,
        AuditAction? action = null,
        string? summary = null,
        string? entityDescription = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all audit entries for a specific entity.
    /// </summary>
    Task<IReadOnlyList<AuditEntry>> GetEntityHistoryAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches audit entries with criteria.
    /// </summary>
    Task<IReadOnlyList<AuditEntry>> SearchAsync(
        AuditSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of entries matching criteria.
    /// </summary>
    Task<int> CountAsync(
        AuditSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets daily statistics for a specific date.
    /// </summary>
    Task<AuditDailyStats> GetDailyStatsAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics for a date range.
    /// </summary>
    Task<IReadOnlyList<AuditDailyStats>> GetStatsRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all audit entries for a specific entity.
    /// Returns the number of entries deleted.
    /// </summary>
    Task<long> DeleteEntityAuditTrailAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the summary of a specific audit entry.
    /// </summary>
    Task UpdateEntrySummaryAsync(
        string entryId,
        string newSummary,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific audit entry by ID.
    /// </summary>
    Task DeleteEntryAsync(
        string entryId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of IAuditService.
/// </summary>
public class AuditService : IAuditService
{
    private readonly IAuditRepository _repository;
    private AuditUserContext? _userContext;

    // Fields to track for common entity types
    private static readonly Dictionary<string, Dictionary<string, string>> TrackedFields = new()
    {
        ["Registration"] = new()
        {
            ["PersonalInfo.FirstName"] = "First Name",
            ["PersonalInfo.MiddleName"] = "Middle Name",
            ["PersonalInfo.LastName"] = "Last Name",
            ["PersonalInfo.PreferredName"] = "Preferred Name",
            ["PersonalInfo.Gender"] = "Gender",
            ["PersonalInfo.DateOfBirth"] = "Date of Birth",
            ["PersonalInfo.PhoneNumber"] = "Competitor Phone",
            ["CompetitionSelection.DivisionId"] = "Division",
            ["CompetitionSelection.CategoryId"] = "Category",
            ["CompetitionSelection.PortionChoice"] = "Portion Choice",
            ["Status"] = "Status",
            ["AddressInfo.City"] = "City",
            ["AddressInfo.StateProvince"] = "State/Province",
            ["AddressInfo.Country"] = "Country",
            ["ParentInfo.FirstName"] = "Parent First Name",
            ["ParentInfo.LastName"] = "Parent Last Name",
            ["ParentInfo.PhoneNumber"] = "Parent Phone",
            ["TeacherInfo.FirstName"] = "Teacher First Name",
            ["TeacherInfo.LastName"] = "Teacher Last Name",
            ["TeacherInfo.PhoneNumber"] = "Teacher Phone",
            ["TeacherInfo.Institution"] = "Institution"
        }
    };

    // Fields to exclude from comparison (internal/computed fields)
    private static readonly HashSet<string> ExcludedFields = new()
    {
        "Id", "CID", "CreatedAt", "UpdatedAt", "SubmittedAt",
        "FileUploadInfo", "WithdrawalInfo"
    };

    public AuditService(IAuditRepository repository)
    {
        _repository = repository;
    }

    public void SetUserContext(AuditUserContext? context)
    {
        _userContext = context;
    }

    public async Task LogAsync(
        AuditAction action,
        string entityType,
        string entityId,
        string? summary = null,
        string? entityDescription = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var entry = CreateEntry(action, entityType, entityId, entityDescription);
        entry.Summary = summary ?? $"{action.GetDisplayName()} {entityType}";
        entry.Metadata = metadata;

        await _repository.SaveAsync(entry, cancellationToken);
    }

    public async Task LogSystemActionAsync(
        AuditAction action,
        string entityType,
        string entityId,
        string? summary = null,
        string? entityDescription = null,
        CancellationToken cancellationToken = default)
    {
        var entry = CreateEntry(action, entityType, entityId, entityDescription);
        entry.Summary = summary ?? $"{action.GetDisplayName()} {entityType}";
        entry.IsSystemAction = true;
        entry.UserId = null;
        entry.UserEmail = null;
        entry.UserDisplayName = null;

        await _repository.SaveAsync(entry, cancellationToken);
    }

    public async Task LogChangesAsync<T>(
        string entityType,
        string entityId,
        T? oldValue,
        T newValue,
        string? entityDescription = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var changes = DetectChanges(oldValue, newValue, entityType);

        if (changes.Count == 0)
            return; // No changes detected

        var action = oldValue == null ? AuditAction.Created : AuditAction.Updated;
        var entry = CreateEntry(action, entityType, entityId, entityDescription);
        entry.Changes = changes;
        entry.Summary = BuildChangeSummary(changes, action);

        await _repository.SaveAsync(entry, cancellationToken);
    }

    public async Task LogFieldChangesAsync(
        string entityType,
        string entityId,
        IEnumerable<FieldChange> changes,
        AuditAction? action = null,
        string? summary = null,
        string? entityDescription = null,
        CancellationToken cancellationToken = default)
    {
        var changeList = changes.ToList();
        if (changeList.Count == 0)
            return;

        var auditAction = action ?? AuditAction.Updated;
        var entry = CreateEntry(auditAction, entityType, entityId, entityDescription);
        entry.Changes = changeList;
        entry.Summary = summary ?? BuildChangeSummary(changeList, auditAction);

        await _repository.SaveAsync(entry, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEntry>> GetEntityHistoryAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetByEntityAsync(entityType, entityId, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEntry>> SearchAsync(
        AuditSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        return await _repository.SearchAsync(criteria, cancellationToken);
    }

    public async Task<int> CountAsync(
        AuditSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        return await _repository.CountAsync(criteria, cancellationToken);
    }

    public async Task<AuditDailyStats> GetDailyStatsAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetDailyStatsAsync(date, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditDailyStats>> GetStatsRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetStatsRangeAsync(from, to, cancellationToken);
    }

    public async Task<long> DeleteEntityAuditTrailAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.DeleteByEntityAsync(entityType, entityId, cancellationToken);
    }

    public async Task UpdateEntrySummaryAsync(
        string entryId,
        string newSummary,
        CancellationToken cancellationToken = default)
    {
        await _repository.UpdateSummaryAsync(entryId, newSummary, cancellationToken);
    }

    public async Task DeleteEntryAsync(
        string entryId,
        CancellationToken cancellationToken = default)
    {
        await _repository.DeleteByIdAsync(entryId, cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    private AuditEntry CreateEntry(AuditAction action, string entityType, string entityId, string? entityDescription)
    {
        return new AuditEntry
        {
            Id = Guid.NewGuid().ToString(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityDescription = entityDescription,
            Timestamp = DateTimeOffset.UtcNow,
            UserId = _userContext?.UserId,
            UserEmail = _userContext?.Email,
            UserDisplayName = _userContext?.DisplayName,
            IsSystemAction = _userContext == null || !_userContext.IsAuthenticated
        };
    }

    private List<FieldChange> DetectChanges<T>(T? oldValue, T newValue, string entityType) where T : class
    {
        var changes = new List<FieldChange>();

        if (newValue == null)
            return changes;

        // Get field mappings for this entity type
        var fieldMappings = TrackedFields.GetValueOrDefault(entityType) ?? new Dictionary<string, string>();

        // Compare using reflection
        CompareObjects(oldValue, newValue, "", fieldMappings, changes);

        return changes;
    }

    private void CompareObjects(object? oldObj, object? newObj, string prefix,
        Dictionary<string, string> fieldMappings, List<FieldChange> changes)
    {
        if (newObj == null)
            return;

        var type = newObj.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && !ExcludedFields.Contains(p.Name));

        foreach (var prop in properties)
        {
            var fieldPath = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

            var newVal = prop.GetValue(newObj);
            var oldVal = oldObj != null ? prop.GetValue(oldObj) : null;

            // Skip null-to-null
            if (oldVal == null && newVal == null)
                continue;

            // Handle nested objects (but not collections or complex types)
            if (prop.PropertyType.IsClass &&
                prop.PropertyType != typeof(string) &&
                !prop.PropertyType.IsArray &&
                !typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType))
            {
                CompareObjects(oldVal, newVal, fieldPath, fieldMappings, changes);
                continue;
            }

            // Compare values
            var oldStr = FormatValue(oldVal);
            var newStr = FormatValue(newVal);

            if (oldStr != newStr)
            {
                // Only track if we have a display name mapping, or it's a simple property
                var displayName = fieldMappings.GetValueOrDefault(fieldPath)
                    ?? fieldMappings.GetValueOrDefault(prop.Name)
                    ?? SplitCamelCase(prop.Name);

                changes.Add(new FieldChange
                {
                    FieldName = fieldPath,
                    DisplayName = displayName,
                    OldValue = oldStr,
                    NewValue = newStr
                });
            }
        }
    }

    private static string? FormatValue(object? value)
    {
        if (value == null)
            return null;

        return value switch
        {
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss"),
            DateOnly d => d.ToString("yyyy-MM-dd"),
            bool b => b ? "Yes" : "No",
            Enum e => e.ToString(),
            _ => value.ToString()
        };
    }

    private static string SplitCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new System.Text.StringBuilder();
        result.Append(input[0]);

        for (int i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]))
                result.Append(' ');
            result.Append(input[i]);
        }

        return result.ToString();
    }

    private static string BuildChangeSummary(List<FieldChange> changes, AuditAction action)
    {
        if (changes.Count == 0)
            return action.GetDisplayName();

        if (changes.Count == 1)
            return changes[0].GetDescription();

        if (changes.Count <= 3)
            return string.Join("; ", changes.Select(c => c.GetDescription()));

        return $"Updated {changes.Count} fields: {string.Join(", ", changes.Take(3).Select(c => c.DisplayName))}...";
    }
}
