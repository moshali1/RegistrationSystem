using RegistrationSystem.Core.Application.Auditing;
using RegistrationSystem.Core.Application.Azure;
using RegistrationSystem.Core.Domain.Auditing;
using RegistrationSystem.Core.Domain.Registrations;
using RegistrationSystem.Core.Domain.Users;

namespace RegistrationSystem.Web.Services;

/// <summary>
/// Helper service to capture user context from database for audit logging.
/// Uses CurrentUserService to fetch user from DB rather than claims directly.
/// </summary>
public class BlazorAuditContextProvider
{
    private readonly CurrentUserService _currentUserService;
    private readonly IAuditService _auditService;

    public BlazorAuditContextProvider(
        CurrentUserService currentUserService,
        IAuditService auditService)
    {
        _currentUserService = currentUserService;
        _auditService = auditService;
    }

    /// <summary>
    /// Sets the current user context on the audit service from the database.
    /// Call this at the beginning of operations that need audit logging.
    /// </summary>
    public async Task SetCurrentUserContextAsync(CancellationToken cancellationToken = default)
    {
        var user = await _currentUserService.GetCurrentUserAsync(cancellationToken);

        if (user != null)
        {
            var context = new AuditUserContext
            {
                IsAuthenticated = true,
                UserId = user.ObjectIdentifier,
                Email = user.Email,
                DisplayName = user.DisplayName
            };

            _auditService.SetUserContext(context);
        }
        else
        {
            _auditService.SetUserContext(new AuditUserContext { IsAuthenticated = false });
        }
    }

    /// <summary>
    /// Creates an audit user context from a User entity.
    /// </summary>
    public static AuditUserContext CreateFromUser(User? user)
    {
        if (user == null)
            return new AuditUserContext { IsAuthenticated = false };

        return new AuditUserContext
        {
            IsAuthenticated = true,
            UserId = user.ObjectIdentifier,
            Email = user.Email,
            DisplayName = user.DisplayName
        };
    }
}

/// <summary>
/// Extension methods for easy audit service usage.
/// </summary>
public static class AuditServiceExtensions
{
    /// <summary>
    /// Logs a registration submission.
    /// </summary>
    public static async Task LogRegistrationSubmittedAsync(
        this IAuditService auditService,
        string registrationId,
        string competitorName,
        string divisionCategory)
    {
        await auditService.LogAsync(
            AuditAction.Submitted,
            "Registration",
            registrationId,
            summary: "New registration submitted",
            entityDescription: $"{competitorName} - {divisionCategory}");
    }

    /// <summary>
    /// Logs a registration status change with unified "OldStatus → NewStatus" format.
    /// </summary>
    public static async Task LogStatusChangeAsync(
        this IAuditService auditService,
        string registrationId,
        string competitorName,
        string oldStatus,
        string newStatus,
        string? reason = null,
        string? method = null)
    {
        var summary = $"{oldStatus} → {newStatus}";

        var metadata = new Dictionary<string, string>
        {
            ["OldStatus"] = oldStatus,
            ["NewStatus"] = newStatus
        };
        if (!string.IsNullOrEmpty(method))
            metadata["Method"] = method;
        if (!string.IsNullOrEmpty(reason))
            metadata["Reason"] = reason;

        await auditService.LogAsync(
            AuditAction.StatusChanged,
            "Registration",
            registrationId,
            summary: summary,
            entityDescription: competitorName,
            metadata: metadata);
    }

    /// <summary>
    /// Logs an email being sent.
    /// </summary>
    public static async Task LogEmailSentAsync(
        this IAuditService auditService,
        string entityType,
        string entityId,
        string recipientEmail,
        string emailType,
        string? entityDescription = null)
    {
        await auditService.LogAsync(
            AuditAction.EmailSent,
            entityType,
            entityId,
            summary: $"{emailType} email sent to {recipientEmail}",
            entityDescription: entityDescription);
    }

    /// <summary>
    /// Logs a niqab bypass creation.
    /// </summary>
    public static async Task LogNiqabBypassCreatedAsync(
        this IAuditService auditService,
        string bypassId,
        string competitorName,
        string code)
    {
        await auditService.LogAsync(
            AuditAction.NiqabBypassCreated,
            "NiqabBypass",
            bypassId,
            summary: $"Bypass code {code} created",
            entityDescription: competitorName);
    }

    /// <summary>
    /// Logs a niqab bypass claim.
    /// </summary>
    public static async Task LogNiqabBypassClaimedAsync(
        this IAuditService auditService,
        string bypassId,
        string competitorName)
    {
        await auditService.LogAsync(
            AuditAction.NiqabBypassClaimed,
            "NiqabBypass",
            bypassId,
            summary: "Bypass code claimed",
            entityDescription: competitorName);
    }

    /// <summary>
    /// Logs a niqab bypass deletion.
    /// </summary>
    public static async Task LogNiqabBypassDeletedAsync(
        this IAuditService auditService,
        string bypassId,
        string competitorName,
        string code)
    {
        await auditService.LogAsync(
            AuditAction.NiqabBypassDeleted,
            "NiqabBypass",
            bypassId,
            summary: $"Bypass code {code} deleted",
            entityDescription: competitorName);
    }

    /// <summary>
    /// Logs a registration deletion with full snapshot of the registration data.
    /// This preserves all relevant info since the registration will be permanently deleted.
    /// </summary>
    public static async Task LogRegistrationDeletedAsync(
        this IAuditService auditService,
        Registration registration,
        string divisionName,
        string categoryName,
        string? creatorEmail = null,
        bool auditTrailDeleted = false,
        bool isBulkOperation = false)
    {
        var name = registration.PersonalInfo.FullName;
        var cid = registration.Cid ?? "N/A";

        var metadata = new Dictionary<string, string>
        {
            ["CompetitorName"] = name,
            ["CID"] = cid,
            ["DateOfBirth"] = registration.PersonalInfo.DateOfBirth.ToString("yyyy-MM-dd"),
            ["Division"] = divisionName,
            ["Category"] = categoryName,
            ["Status"] = registration.Status.ToString(),
            ["AuditTrailDeleted"] = auditTrailDeleted.ToString()
        };

        if (registration.CompetitionSelection.PortionChoice.HasValue)
            metadata["Portion"] = registration.CompetitionSelection.PortionChoice.Value.ToString();

        if (!string.IsNullOrEmpty(creatorEmail))
            metadata["CreatorEmail"] = creatorEmail;

        if (isBulkOperation)
            metadata["BulkOperation"] = "true";

        await auditService.LogAsync(
            AuditAction.Deleted,
            "Registration",
            registration.Id,
            summary: $"Registration deleted: {name} ({cid})",
            entityDescription: $"{name} ({cid})",
            metadata: metadata);
    }

    /// <summary>
    /// Logs a settings update.
    /// </summary>
    public static async Task LogSettingsUpdatedAsync(
        this IAuditService auditService,
        string settingsId,
        string section,
        string? details = null)
    {
        await auditService.LogAsync(
            AuditAction.SettingsUpdated,
            "CompetitionSettings",
            settingsId,
            summary: $"{section} settings updated" + (details != null ? $": {details}" : ""),
            entityDescription: $"Competition {DateTime.Now.Year}");
    }

    /// <summary>
    /// Logs an ID verification result. Skips logging for skipped/errored results.
    /// </summary>
    public static async Task LogIdVerificationAsync(
        this IAuditService auditService,
        IdVerificationResult result,
        string entityDescription)
    {
        if (result.IsSkipped || result.HasError) return;

        var outcomeLabel = result.Outcome switch
        {
            IdVerificationOutcome.Pass => "Pass",
            IdVerificationOutcome.Flag => "Flag",
            _ => "Needs Review"
        };

        var metadata = new Dictionary<string, string>
        {
            ["Outcome"] = outcomeLabel,
            ["DocumentType"] = result.DocumentType,
            ["IssuingCountry"] = result.IssuingCountry,
            ["FirstNameMatch"] = result.FirstNameMatch.ToString(),
            ["LastNameMatch"] = result.LastNameMatch.ToString(),
            ["DateOfBirthMatch"] = result.DateOfBirthMatch.ToString(),
            ["Reasoning"] = result.Reasoning
        };

        await auditService.LogAsync(
            AuditAction.IdVerification,
            "Registration",
            result.RegistrationId,
            summary: $"ID Verification: {outcomeLabel}",
            entityDescription: entityDescription,
            metadata: metadata);
    }
}