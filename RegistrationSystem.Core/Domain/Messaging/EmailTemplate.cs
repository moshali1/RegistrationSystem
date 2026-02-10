namespace RegistrationSystem.Core.Domain.Messaging;

/// <summary>
/// Represents a reusable email template with placeholder support.
/// Placeholders: {{CompetitorName}}, {{CID}}, {{DivisionName}}, {{CategoryName}},
/// {{ParentName}}, {{StatusComment}}, {{SiteUrl}}, {{EditUrl}}, {{ParticipantsTable}}
/// </summary>
public class EmailTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string PlainTextBody { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional division scope. Null = applies to all divisions (global).
    /// </summary>
    public string? DivisionId { get; set; }

    /// <summary>
    /// Optional category scope. Null = applies to all categories within the division.
    /// Only meaningful when DivisionId is also set.
    /// </summary>
    public string? CategoryId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
