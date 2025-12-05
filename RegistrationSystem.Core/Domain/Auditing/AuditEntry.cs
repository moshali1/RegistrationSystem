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
        var centralZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
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
/// </summary>
public enum AuditAction
{
    // General CRUD
    Created = 0,
    Updated = 1,
    Deleted = 2,

    // Registration-specific
    Submitted = 10,
    StatusChanged = 11,
    Approved = 12,
    Rejected = 13,
    Withdrawn = 14,
    WithdrawalRequested = 15,
    Verified = 16,

    // File operations
    FileUploaded = 20,
    FileDeleted = 21,

    // Communication
    EmailSent = 30,
    SmsSent = 31,

    // Niqab bypass
    NiqabBypassCreated = 40,
    NiqabBypassClaimed = 41,
    NiqabBypassDeleted = 42,

    // Settings
    SettingsUpdated = 50,
    DivisionUpdated = 51,
    CategoryUpdated = 52,

    // Admin actions
    AdminOverride = 60,
    ManualCorrection = 61,

    // System
    SystemMigration = 90,
    DataImport = 91,
    DataExport = 92
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
        AuditAction.Created => "Created",
        AuditAction.Updated => "Updated",
        AuditAction.Deleted => "Deleted",
        AuditAction.Submitted => "Submitted",
        AuditAction.StatusChanged => "Status Changed",
        AuditAction.Approved => "Approved",
        AuditAction.Rejected => "Rejected",
        AuditAction.Withdrawn => "Withdrawn",
        AuditAction.WithdrawalRequested => "Withdrawal Requested",
        AuditAction.FileUploaded => "File Uploaded",
        AuditAction.FileDeleted => "File Deleted",
        AuditAction.EmailSent => "Email Sent",
        AuditAction.SmsSent => "SMS Sent",
        AuditAction.NiqabBypassCreated => "Niqab Bypass Created",
        AuditAction.NiqabBypassClaimed => "Niqab Bypass Claimed",
        AuditAction.NiqabBypassDeleted => "Niqab Bypass Deleted",
        AuditAction.SettingsUpdated => "Settings Updated",
        AuditAction.DivisionUpdated => "Division Updated",
        AuditAction.CategoryUpdated => "Category Updated",
        AuditAction.AdminOverride => "Admin Override",
        AuditAction.ManualCorrection => "Manual Correction",
        AuditAction.SystemMigration => "System Migration",
        AuditAction.DataImport => "Data Import",
        AuditAction.DataExport => "Data Export",
        _ => action.ToString()
    };

    /// <summary>
    /// Gets the CSS color class for an audit action.
    /// </summary>
    public static string GetColorClass(this AuditAction action) => action switch
    {
        AuditAction.Created or AuditAction.Submitted => "emerald",
        AuditAction.Updated => "cyan",
        AuditAction.Deleted => "red",
        AuditAction.Approved => "emerald",
        AuditAction.Rejected => "red",
        AuditAction.Withdrawn or AuditAction.WithdrawalRequested => "amber",
        AuditAction.StatusChanged => "violet",
        AuditAction.EmailSent or AuditAction.SmsSent => "blue",
        AuditAction.FileUploaded => "cyan",
        AuditAction.FileDeleted => "red",
        AuditAction.NiqabBypassCreated or AuditAction.NiqabBypassClaimed => "violet",
        AuditAction.SettingsUpdated or AuditAction.DivisionUpdated or AuditAction.CategoryUpdated => "slate",
        AuditAction.AdminOverride or AuditAction.ManualCorrection => "amber",
        _ => "slate"
    };
}