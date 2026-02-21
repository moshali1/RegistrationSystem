namespace RegistrationSystem.Core.Domain.Settings;

/// <summary>
/// Default email template IDs for each registration status transition.
/// Template IDs reference documents in the EmailTemplates collection.
/// Null means no default configured (no email sent for that status change).
/// </summary>
public class EmailDefaults
{
    public string? PendingTemplateId { get; set; }
    public string? VerifiedTemplateId { get; set; }
    public string? DisqualifiedTemplateId { get; set; }
    public string? WithdrawnTemplateId { get; set; }
}
