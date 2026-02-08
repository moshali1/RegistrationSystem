namespace RegistrationSystem.Core.Application.Messaging;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string SendGridApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = string.Empty;
    public string SiteUrl { get; set; } = string.Empty;
}
