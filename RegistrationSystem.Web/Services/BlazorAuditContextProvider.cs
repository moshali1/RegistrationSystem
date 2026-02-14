using RegistrationSystem.Core.Application.Auditing;
using RegistrationSystem.Core.Domain.Auditing;
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
    /// Logs a registration status change.
    /// </summary>
    public static async Task LogStatusChangeAsync(
        this IAuditService auditService,
        string registrationId,
        string competitorName,
        string oldStatus,
        string newStatus,
        string? reason = null)
    {
        var summary = $"Status changed from {oldStatus} to {newStatus}";
        if (!string.IsNullOrEmpty(reason))
            summary += $" ({reason})";

        await auditService.LogAsync(
            AuditAction.StatusChanged,
            "Registration",
            registrationId,
            summary: summary,
            entityDescription: competitorName);
    }

    /// <summary>
    /// Logs a registration withdrawal request.
    /// </summary>
    public static async Task LogWithdrawalRequestedAsync(
        this IAuditService auditService,
        string registrationId,
        string competitorName,
        string reason)
    {
        await auditService.LogAsync(
            AuditAction.WithdrawalRequested,
            "Registration",
            registrationId,
            summary: $"Withdrawal requested: {reason}",
            entityDescription: competitorName);
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
}