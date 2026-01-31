namespace RegistrationSystem.Core.Domain.Settings;

/// <summary>
/// Root aggregate for competition-wide settings. Singleton - only one instance exists.
/// </summary>
public class CompetitionSettings
{
    public const string SingletonId = "default-competition-settings";

    public string Id { get; set; } = SingletonId;

    // === Global Registration Control ===

    public bool RegistrationEnabled { get; set; } // Master switch for entire competition
    public DateTimeOffset? RegistrationStart { get; set; } // Default start - used when category has no override
    public DateTimeOffset? RegistrationEnd { get; set; } // Default end - used when category has no override
    public DateOnly AgeCutoffDate { get; set; } = new DateOnly(DateTime.UtcNow.Year, 1, 1); // Age calculated as of this date

    // === Competition Structure ===

    public List<Division> Divisions { get; set; } = new();

    // === Competition Metadata ===

    public CompetitionInfo CompetitionInfo { get; set; } = new();
    public CidConfiguration CidConfiguration { get; set; } = new();

    // Helper methods
    public Division? FindDivision(string divisionId) =>
        Divisions.FirstOrDefault(d => d.Id == divisionId);
}

public class Division
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } // When disabled, all categories within are unavailable
    public List<Category> Categories { get; set; } = new();

    public Category? FindCategory(string categoryId) =>
        Categories.FirstOrDefault(c => c.Id == categoryId);
}

public class Category
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    // === Basic Information ===

    public string Name { get; set; } = string.Empty;
    public string? AlternateName { get; set; }
    public bool IsEnabled { get; set; }

    // === Eligibility Rules ===

    public int? MaxAgeYears { get; set; } // Null = no age restriction
    public PortionOption PortionOption { get; set; } = PortionOption.NotApplicable;

    // === Schedule Override ===

    public DateTimeOffset? RegistrationStart { get; set; } // Null = use global start
    public DateTimeOffset? RegistrationEnd { get; set; } // Null = use global end

    // === Video Requirements ===

    public bool RequiresVideo { get; set; }
    public string? VideoInstructions { get; set; } // e.g., "Record Surah Al-Baqarah verses 1-20"

    // === Competitor Permissions ===

    public bool AllowMultipleInDivision { get; set; } // Allow registration in multiple categories within same division
    public bool AllowEdit { get; set; } = true; // Allow edits when status is Pending
    public bool AllowWithdraw { get; set; } = true;
}

public enum PortionOption
{
    NotApplicable = 0,
    TopOnly = 1,
    BottomOnly = 2,
    TopOrBottom = 3
}

public class CompetitionInfo
{
    // Configurable in the Competition Info tab
    // Defaults provided for convenience
    public string CompetitionName { get; set; } = "North America Imam Al-Shatibi Qur'an Competition";
    public int CompetitionYear { get; set; } = DateTime.UtcNow.Year;
    public string PrivacyPolicyUrl { get; set; } = "https://imamshatibi.org/privacy-policy";
    public string TermsOfServiceUrl { get; set; } = "https://imamshatibi.org/terms-of-service";
    public string RulesUrl { get; set; } = "https://imamshatibi.org/rules";
}

/// <summary>
/// CID Format: [DivisionLetter][StateCode][3-digit sequence]
/// Example: M3001 = Memorization (M), MN state (code 3), competitor #1
/// </summary>
public class CidConfiguration
{
    // TODO: Make state mappings configurable in UI
    // Current mappings are for North America Imam Al-Shatibi Competition
    // States with high competitor counts get dedicated codes
    public Dictionary<string, string> StateCodeMapping { get; set; } = new()
    {
        { "MN", "3" },  // Minnesota
        { "TN", "5" },  // Tennessee  
        { "TX", "7" },  // Texas
    };

    public string DefaultStateCode { get; set; } = "9"; // For unmapped states

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
