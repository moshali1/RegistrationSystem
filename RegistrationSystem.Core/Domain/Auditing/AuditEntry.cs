namespace RegistrationSystem.Core.Domain.Auditing;

/// <summary>
/// Represents an audit log entry tracking changes to entities in the system.
/// </summary>
public class AuditEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // ═══════════════════════════════════════════════════════════════════════════
    // WHAT WAS AFFECTED
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The type of entity that was affected (e.g., "Registration", "NiqabBypass", "CompetitionSettings").
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// The unique identifier of the affected entity.
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// A human-readable description of the entity for quick reference.
    /// Example: "John Doe - Memorization Cat A" or "CID: M3001"
    /// </summary>
    public string? EntityDescription { get; set; }

    // ═══════════════════════════════════════════════════════════════════════════
    // WHAT HAPPENED
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The type of action that was performed.
    /// </summary>
    public AuditAction Action { get; set; }

    /// <summary>
    /// A human-readable summary of what happened.
    /// Example: "Changed status from AwaitingReview to Approved"
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Detailed field-level changes (for update actions).
    /// </summary>
    public List<FieldChange> Changes { get; set; } = new();

    // ═══════════════════════════════════════════════════════════════════════════
    // WHO DID IT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The unique identifier of the user who performed the action (from Entra External ID).
    /// Null for system/anonymous actions.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// The email address of the user who performed the action.
    /// </summary>
    public string? UserEmail { get; set; }

    /// <summary>
    /// The display name of the user who performed the action.
    /// </summary>
    public string? UserDisplayName { get; set; }

    /// <summary>
    /// Indicates whether this was an automated system action (not triggered by a user).
    /// </summary>
    public bool IsSystemAction { get; set; }

    // ═══════════════════════════════════════════════════════════════════════════
    // WHEN
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The UTC timestamp when the action occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    // ═══════════════════════════════════════════════════════════════════════════
    // ADDITIONAL CONTEXT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Optional metadata for additional context (IP address, browser, request ID, etc.).
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets the timestamp in Central Time for display.
    /// </summary>
    public DateTime GetCentralTime()
    {
        var centralZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        return TimeZoneInfo.ConvertTimeFromUtc(Timestamp.UtcDateTime, centralZone);
    }

    /// <summary>
    /// Gets the formatted timestamp in Central Time.
    /// </summary>
    public string GetFormattedTimestamp(string format = "MMM d, yyyy h:mm:ss tt")
    {
        return GetCentralTime().ToString(format) + " CT";
    }

    /// <summary>
    /// Gets the actor description (user name or "System").
    /// </summary>
    public string GetActorDescription()
    {
        if (IsSystemAction)
            return "System";

        if (!string.IsNullOrEmpty(UserDisplayName))
            return UserDisplayName;

        if (!string.IsNullOrEmpty(UserEmail))
            return UserEmail;

        return "Anonymous";
    }
}

/// <summary>
/// Represents a single field change within an audit entry.
/// </summary>
public class FieldChange
{
    /// <summary>
    /// The technical name of the field (e.g., "FirstName", "Status").
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// The human-readable display name of the field (e.g., "First Name", "Status").
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The previous value (null if new).
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// The new value (null if deleted).
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Gets a human-readable description of the change.
    /// </summary>
    public string GetDescription()
    {
        if (string.IsNullOrEmpty(OldValue) && !string.IsNullOrEmpty(NewValue))
            return $"Set {DisplayName} to \"{NewValue}\"";

        if (!string.IsNullOrEmpty(OldValue) && string.IsNullOrEmpty(NewValue))
            return $"Cleared {DisplayName} (was \"{OldValue}\")";

        return $"Changed {DisplayName} from \"{OldValue}\" to \"{NewValue}\"";
    }
}

/// <summary>
/// Types of audit actions that can be logged.
/// Integer values must match the MongoDB migration script (scripts/migrate-audit-actions.js).
/// </summary>
public enum AuditAction
{
    // Registration lifecycle
    Submitted = 1,
    Updated = 2,
    Deleted = 3,

    // Status changes
    StatusChanged = 10,
    Withdrawn = 11,
    Disqualified = 12,

    // Communication
    EmailSent = 20,
    SmsSent = 21,

    // Niqab bypass
    NiqabBypassCreated = 30,
    NiqabBypassClaimed = 31,
    NiqabBypassDeleted = 32,
    NiqabBypassReversed = 33,

    // Settings
    SettingsUpdated = 40,

    // Admin actions
    ManualCorrection = 50,

    // Data operations
    DataImport = 60,
    DataExport = 61,

    // ID Verification
    IdVerification = 70
}

/// <summary>
/// Extension methods for AuditAction.
/// </summary>
public static class AuditActionExtensions
{
    /// <summary>
    /// Gets the display name for an audit action.
    /// </summary>
    public static string GetDisplayName(this AuditAction action) => action switch
    {
        AuditAction.Submitted => "Submitted",
        AuditAction.Updated => "Updated",
        AuditAction.Deleted => "Deleted",
        AuditAction.StatusChanged => "Status Changed",
        AuditAction.Withdrawn => "Withdrawn",
        AuditAction.Disqualified => "Disqualified",
        AuditAction.EmailSent => "Email Sent",
        AuditAction.SmsSent => "SMS Sent",
        AuditAction.NiqabBypassCreated => "Niqab Bypass Created",
        AuditAction.NiqabBypassClaimed => "Niqab Bypass Claimed",
        AuditAction.NiqabBypassDeleted => "Niqab Bypass Deleted",
        AuditAction.NiqabBypassReversed => "Niqab Bypass (Reverse)",
        AuditAction.SettingsUpdated => "Settings Updated",
        AuditAction.ManualCorrection => "Manual Correction",
        AuditAction.DataImport => "Data Import",
        AuditAction.DataExport => "Data Export",
        AuditAction.IdVerification => "ID Verification",
        _ => action.ToString()
    };

    /// <summary>
    /// Gets the CSS color class for an audit action.
    /// </summary>
    public static string GetColorClass(this AuditAction action) => action switch
    {
        AuditAction.Submitted => "emerald",
        AuditAction.Updated => "cyan",
        AuditAction.Deleted or AuditAction.Disqualified => "red",
        AuditAction.Withdrawn => "amber",
        AuditAction.StatusChanged => "violet",
        AuditAction.EmailSent or AuditAction.SmsSent => "blue",
        AuditAction.NiqabBypassCreated or AuditAction.NiqabBypassClaimed or AuditAction.NiqabBypassDeleted or AuditAction.NiqabBypassReversed => "violet",
        AuditAction.SettingsUpdated => "slate",
        AuditAction.ManualCorrection => "amber",
        AuditAction.DataImport or AuditAction.DataExport => "slate",
        AuditAction.IdVerification => "cyan",
        _ => "slate"
    };
}