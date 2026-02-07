namespace RegistrationSystem.Core.Domain.Settings;

public class CompetitionInfo
{
    public string CompetitionName { get; set; } = string.Empty;
    public int CompetitionYear { get; set; }
    public string PrivacyPolicyUrl { get; set; } = string.Empty;
    public string TermsOfServiceUrl { get; set; } = string.Empty;
    public string RulesUrl { get; set; } = string.Empty;
}
