namespace RegistrationSystem.Core.Domain.Settings;

public class CompetitionSettings
{
    public string Id { get; set; } = string.Empty;
    public bool RegistrationEnabled { get; set; }
    public DateTimeOffset? RegistrationStart { get; set; }
    public DateTimeOffset? RegistrationEnd { get; set; }
    public DateOnly AgeCutoffDate { get; set; } = new DateOnly(DateTime.UtcNow.Year, 1, 1);
    public List<Division> Divisions { get; set; } = new();

    /// <summary>
    /// Competition information and legal policy URLs.
    /// </summary>
    public CompetitionInfo CompetitionInfo { get; set; } = new();

    /// <summary>
    /// Configuration for Competitor ID (CID) generation.
    /// </summary>
    public CidConfiguration CidConfiguration { get; set; } = new();

    // Helper methods
    public Division? FindDivision(string divisionId) =>
        Divisions.FirstOrDefault(d => d.Id == divisionId);
}

public class Division
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<Category> Categories { get; set; } = new();

    // Helper methods
    public Category? FindCategory(string categoryId) =>
        Categories.FirstOrDefault(c => c.Id == categoryId);
}

public class Category
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string? AlternateName { get; set; }
    public bool IsEnabled { get; set; }
    public int? MaxAgeYears { get; set; }
    public DateTimeOffset? RegistrationStart { get; set; }
    public DateTimeOffset? RegistrationEnd { get; set; }
    public PortionOption PortionOption { get; set; } = PortionOption.NotApplicable;

    /// <summary>
    /// Whether this category requires a video upload.
    /// </summary>
    public bool RequiresVideo { get; set; }

    /// <summary>
    /// Instructions for the video upload (e.g., "Record Surah Al-Baqarah verses 1-20").
    /// </summary>
    public string? VideoInstructions { get; set; }

    /// <summary>
    /// Whether a competitor can register for multiple categories within the same division.
    /// Defaults to false (one category per division).
    /// </summary>
    public bool AllowMultipleInDivision { get; set; }

    /// <summary>
    /// Whether registrations in this category can be edited after submission.
    /// Only applies when status is Pending (sent back for corrections).
    /// </summary>
    public bool AllowEdit { get; set; } = true;

    /// <summary>
    /// Whether registrations in this category can be withdrawn.
    /// </summary>
    public bool AllowWithdraw { get; set; } = true;
}

public enum PortionOption
{
    NotApplicable = 0,
    TopOnly = 1,
    BottomOnly = 2,
    TopOrBottom = 3
}

/// <summary>
/// Competition information and legal policy URLs.
/// </summary>
public class CompetitionInfo
{
    /// <summary>
    /// The full name of the competition.
    /// </summary>
    public string CompetitionName { get; set; } = "North America Imam Al-Shatibi Qur'an Competition";

    /// <summary>
    /// The competition year.
    /// </summary>
    public int CompetitionYear { get; set; } = DateTime.UtcNow.Year;

    /// <summary>
    /// URL to the Privacy Policy page.
    /// </summary>
    public string PrivacyPolicyUrl { get; set; } = "https://imamshatibi.org/privacy-policy";

    /// <summary>
    /// URL to the Terms of Service page.
    /// </summary>
    public string TermsOfServiceUrl { get; set; } = "https://imamshatibi.org/terms-of-service";

    /// <summary>
    /// URL to the Rules and Regulations page.
    /// </summary>
    public string RulesUrl { get; set; } = "https://imamshatibi.org/rules";
}

/// <summary>
/// Configuration for Competitor ID (CID) generation.
/// CID Format: [DivisionLetter][StateCode][3-digit sequence]
/// Example: M3001 = Memorization (M), MN state (code 3), competitor #1
/// </summary>
public class CidConfiguration
{
    /// <summary>
    /// Mapping of state abbreviations to their CID code.
    /// States with high competitor counts get dedicated codes.
    /// Default code (for unmapped states) is defined in DefaultStateCode.
    /// </summary>
    public Dictionary<string, string> StateCodeMapping { get; set; } = new()
    {
        // High-volume states get dedicated codes
        { "MN", "3" },  // Minnesota
        { "TN", "5" },  // Tennessee  
        { "TX", "7" },  // Texas
        // Add more state mappings as needed
    };

    /// <summary>
    /// Default state code for states not in the StateCodeMapping.
    /// </summary>
    public string DefaultStateCode { get; set; } = "9";

    /// <summary>
    /// Gets the state code for a given state abbreviation.
    /// </summary>
    public string GetStateCode(string? stateAbbreviation)
    {
        if (string.IsNullOrWhiteSpace(stateAbbreviation))
            return DefaultStateCode;

        var normalized = stateAbbreviation.Trim().ToUpperInvariant();
        return StateCodeMapping.TryGetValue(normalized, out var code) ? code : DefaultStateCode;
    }

    /// <summary>
    /// Gets all unique state codes (for sequence tracking).
    /// </summary>
    public IEnumerable<string> GetAllStateCodes()
    {
        return StateCodeMapping.Values.Distinct().Append(DefaultStateCode).Distinct();
    }
}