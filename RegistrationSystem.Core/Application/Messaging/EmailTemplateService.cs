using System.Net;
using System.Text;
using RegistrationSystem.Core.Domain.Messaging;

namespace RegistrationSystem.Core.Application.Messaging;

/// <summary>
/// Data carrier for a single participant row in an account-grouped email.
/// </summary>
public record ParticipantRow(
    string Cid,
    string FullName,
    string DivisionName,
    string CategoryName,
    string? Portion);

public class EmailTemplateService
{
    private readonly IEmailTemplateRepository _repository;

    public EmailTemplateService(IEmailTemplateRepository repository)
    {
        _repository = repository;
    }

    public Task<List<EmailTemplate>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    public Task<EmailTemplate?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public async Task SaveAsync(EmailTemplate template, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(template.Name))
            throw new InvalidOperationException("Template name is required.");

        if (string.IsNullOrWhiteSpace(template.Subject))
            throw new InvalidOperationException("Template subject is required.");

        if (string.IsNullOrWhiteSpace(template.HtmlBody))
            throw new InvalidOperationException("Template HTML body is required.");

        // Sanitize: empty strings → null for optional scope fields
        // (Blazor <select> binds "" for the default option, but MongoDB would try to parse "" as ObjectId)
        if (string.IsNullOrWhiteSpace(template.DivisionId))
            template.DivisionId = null;
        if (string.IsNullOrWhiteSpace(template.CategoryId))
            template.CategoryId = null;

        template.UpdatedAt = DateTimeOffset.UtcNow;

        if (string.IsNullOrEmpty(template.Id))
            template.CreatedAt = DateTimeOffset.UtcNow;

        await _repository.SaveAsync(template, cancellationToken);
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, cancellationToken);

    // ═══════════════════════════════════════════════════════════════════════════
    // TEMPLATE RENDERING
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Renders a template by replacing placeholders with actual values.
    /// Uses the same placeholder dictionary for subject, plain text, and HTML.
    /// </summary>
    public (string Subject, string PlainText, string Html) RenderTemplate(
        EmailTemplate template,
        Dictionary<string, string> placeholders)
    {
        var subject = ReplacePlaceholders(template.Subject, placeholders);
        var plainText = ReplacePlaceholders(template.PlainTextBody, placeholders);
        var html = ReplacePlaceholders(template.HtmlBody, placeholders);

        return (subject, plainText, html);
    }

    /// <summary>
    /// Renders a template with separate placeholder dictionaries for HTML and plain text.
    /// Needed for account-grouped emails where {{ParticipantsTable}} renders differently.
    /// </summary>
    public (string Subject, string PlainText, string Html) RenderTemplate(
        EmailTemplate template,
        Dictionary<string, string> htmlPlaceholders,
        Dictionary<string, string> plainTextPlaceholders)
    {
        var subject = ReplacePlaceholders(template.Subject, htmlPlaceholders);
        var plainText = ReplacePlaceholders(template.PlainTextBody, plainTextPlaceholders);
        var html = ReplacePlaceholders(template.HtmlBody, htmlPlaceholders);

        return (subject, plainText, html);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PER-REGISTRATION PLACEHOLDERS (original)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds a placeholder dictionary for a single registration.
    /// </summary>
    public static Dictionary<string, string> BuildPlaceholders(
        string competitorName,
        string? cid,
        string divisionName,
        string categoryName,
        string parentName,
        string? statusComment,
        string siteUrl,
        string? editUrl,
        string? deadline = null)
    {
        return new Dictionary<string, string>
        {
            ["{{CompetitorName}}"] = competitorName,
            ["{{CID}}"] = cid ?? "N/A",
            ["{{DivisionName}}"] = divisionName,
            ["{{CategoryName}}"] = categoryName,
            ["{{ParentName}}"] = parentName,
            ["{{StatusComment}}"] = statusComment ?? "",
            ["{{SiteUrl}}"] = siteUrl,
            ["{{EditUrl}}"] = editUrl ?? siteUrl,
            ["{{Deadline}}"] = deadline ?? "as soon as possible",
            ["{{ParticipantsTable}}"] = ""
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ACCOUNT-LEVEL PLACEHOLDERS (grouped emails)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds placeholder dictionaries for account-grouped emails.
    /// Returns separate HTML and plain-text placeholder maps.
    /// </summary>
    public static (Dictionary<string, string> Html, Dictionary<string, string> PlainText) BuildAccountPlaceholders(
        string parentName,
        string siteUrl,
        List<ParticipantRow> participants)
    {
        var tableHtml = BuildParticipantsTableHtml(participants);
        var tablePlainText = BuildParticipantsTablePlainText(participants);

        var htmlPlaceholders = new Dictionary<string, string>
        {
            ["{{ParentName}}"] = WebUtility.HtmlEncode(parentName),
            ["{{SiteUrl}}"] = siteUrl,
            ["{{ParticipantsTable}}"] = tableHtml,
            // Empty strings for per-registration placeholders so they don't render as raw tags
            ["{{CompetitorName}}"] = "",
            ["{{CID}}"] = "",
            ["{{DivisionName}}"] = "",
            ["{{CategoryName}}"] = "",
            ["{{StatusComment}}"] = "",
            ["{{Deadline}}"] = "",
            ["{{EditUrl}}"] = siteUrl
        };

        var plainTextPlaceholders = new Dictionary<string, string>
        {
            ["{{ParentName}}"] = parentName,
            ["{{SiteUrl}}"] = siteUrl,
            ["{{ParticipantsTable}}"] = tablePlainText,
            ["{{CompetitorName}}"] = "",
            ["{{CID}}"] = "",
            ["{{DivisionName}}"] = "",
            ["{{CategoryName}}"] = "",
            ["{{StatusComment}}"] = "",
            ["{{Deadline}}"] = "",
            ["{{EditUrl}}"] = siteUrl
        };

        return (htmlPlaceholders, plainTextPlaceholders);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PARTICIPANTS TABLE BUILDERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds an HTML table of participants with inline CSS for email rendering.
    /// </summary>
    public static string BuildParticipantsTableHtml(List<ParticipantRow> participants)
    {
        var sb = new StringBuilder();
        sb.Append("<table style=\"width: 100%; border-collapse: collapse; font-family: Arial, sans-serif; font-size: 14px; margin: 16px 0;\">");

        // Header row
        sb.Append("<thead>");
        sb.Append("<tr style=\"background-color: #0e7490;\">");
        sb.Append("<th style=\"padding: 10px 12px; text-align: left; color: white; font-weight: 600; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px;\">CID</th>");
        sb.Append("<th style=\"padding: 10px 12px; text-align: left; color: white; font-weight: 600; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px;\">Full Name</th>");
        sb.Append("<th style=\"padding: 10px 12px; text-align: left; color: white; font-weight: 600; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px;\">Division</th>");
        sb.Append("<th style=\"padding: 10px 12px; text-align: left; color: white; font-weight: 600; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px;\">Category</th>");
        sb.Append("<th style=\"padding: 10px 12px; text-align: left; color: white; font-weight: 600; font-size: 12px; text-transform: uppercase; letter-spacing: 0.5px;\">Portion</th>");
        sb.Append("</tr>");
        sb.Append("</thead>");

        // Body rows
        sb.Append("<tbody>");
        for (var i = 0; i < participants.Count; i++)
        {
            var p = participants[i];
            var bgColor = i % 2 == 0 ? "#ffffff" : "#f8fafc";
            var borderStyle = "border-bottom: 1px solid #e2e8f0;";

            sb.Append($"<tr style=\"background-color: {bgColor};\">");
            sb.Append($"<td style=\"padding: 10px 12px; {borderStyle}\"><code style=\"background-color: #f1f5f9; padding: 2px 6px; border-radius: 4px; font-size: 13px;\">{WebUtility.HtmlEncode(p.Cid)}</code></td>");
            sb.Append($"<td style=\"padding: 10px 12px; {borderStyle} font-weight: 500;\">{WebUtility.HtmlEncode(p.FullName)}</td>");
            sb.Append($"<td style=\"padding: 10px 12px; {borderStyle}\">{WebUtility.HtmlEncode(p.DivisionName)}</td>");
            sb.Append($"<td style=\"padding: 10px 12px; {borderStyle}\">{WebUtility.HtmlEncode(p.CategoryName)}</td>");
            sb.Append($"<td style=\"padding: 10px 12px; {borderStyle}\">{WebUtility.HtmlEncode(p.Portion ?? "\u2014")}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody>");
        sb.Append("</table>");

        return sb.ToString();
    }

    /// <summary>
    /// Builds a plain-text formatted table of participants.
    /// </summary>
    public static string BuildParticipantsTablePlainText(List<ParticipantRow> participants)
    {
        var sb = new StringBuilder();
        sb.AppendLine();

        // Header
        sb.AppendLine($"  {"CID",-16} {"Full Name",-28} {"Division",-18} {"Category",-18} {"Portion",-10}");
        sb.AppendLine($"  {new string('-', 16)} {new string('-', 28)} {new string('-', 18)} {new string('-', 18)} {new string('-', 10)}");

        // Rows
        foreach (var p in participants)
        {
            var portion = p.Portion ?? "---";
            sb.AppendLine($"  {p.Cid,-16} {p.FullName,-28} {p.DivisionName,-18} {p.CategoryName,-18} {portion,-10}");
        }

        sb.AppendLine();
        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEMPLATE SCOPE FILTERING
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Filters templates that match the scope of the given registrations.
    /// Returns active templates whose DivisionId/CategoryId scope matches at least one registration.
    /// </summary>
    public static List<EmailTemplate> FilterTemplatesForRegistrations(
        List<EmailTemplate> allTemplates,
        IReadOnlyCollection<(string DivisionId, string CategoryId)> registrationScopes)
    {
        return allTemplates
            .Where(t => t.IsActive && TemplateMatchesScope(t, registrationScopes))
            .ToList();
    }

    private static bool TemplateMatchesScope(
        EmailTemplate template,
        IReadOnlyCollection<(string DivisionId, string CategoryId)> scopes)
    {
        // Global template (no scoping) always matches
        if (string.IsNullOrEmpty(template.DivisionId) && string.IsNullOrEmpty(template.CategoryId))
            return true;

        // Division-only scoped: matches if any registration is in that division
        if (!string.IsNullOrEmpty(template.DivisionId) && string.IsNullOrEmpty(template.CategoryId))
            return scopes.Any(s => s.DivisionId == template.DivisionId);

        // Division+Category scoped: matches if any registration matches both
        if (!string.IsNullOrEmpty(template.DivisionId) && !string.IsNullOrEmpty(template.CategoryId))
            return scopes.Any(s => s.DivisionId == template.DivisionId && s.CategoryId == template.CategoryId);

        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    private static string ReplacePlaceholders(string content, Dictionary<string, string> placeholders)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        foreach (var (placeholder, value) in placeholders)
        {
            content = content.Replace(placeholder, value, StringComparison.OrdinalIgnoreCase);
        }

        return content;
    }
}
