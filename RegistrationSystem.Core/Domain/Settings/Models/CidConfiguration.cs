namespace RegistrationSystem.Core.Domain.Settings;

public class CidConfiguration
{
    public Dictionary<string, string> StateCodeMapping { get; set; } = new();
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
        return StateCodeMapping.Values.Append(DefaultStateCode).Distinct();
    }
}
