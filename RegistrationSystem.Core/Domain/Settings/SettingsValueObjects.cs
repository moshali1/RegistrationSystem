namespace RegistrationSystem.Core.Domain.Settings;

public class CompetitionInfo
{
    public string CompetitionName { get; set; } = "North America Imam Al-Shatibi Qur'an Competition";
    public int CompetitionYear { get; set; } = DateTime.UtcNow.Year;
    public string PrivacyPolicyUrl { get; set; } = "https://imamshatibi.org/privacy-policy";
    public string TermsOfServiceUrl { get; set; } = "https://imamshatibi.org/terms-of-service";
    public string RulesUrl { get; set; } = "https://imamshatibi.org/rules";
}

public class CidConfiguration
{
    public Dictionary<string, string> StateCodeMapping { get; set; } = new()
    {
        { "MN", "3" },
        { "TN", "5" },
        { "TX", "7" }
    };

    public string DefaultStateCode { get; set; } = "9";

    public string GetStateCode(string? stateAbbreviation)
    {
        if (string.IsNullOrWhiteSpace(stateAbbreviation))
            return DefaultStateCode;

        var normalized = stateAbbreviation.Trim().ToUpperInvariant();
        return StateCodeMapping.TryGetValue(normalized, out var code) ? code : DefaultStateCode;
    }

    public IEnumerable<string> GetAllStateCodes()
    {
        return StateCodeMapping.Values.Distinct().Append(DefaultStateCode).Distinct();
    }
}
