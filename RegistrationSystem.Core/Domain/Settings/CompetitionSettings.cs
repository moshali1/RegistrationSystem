namespace RegistrationSystem.Core.Domain.Settings;

public class CompetitionSettings
{
    public const string SingletonId = "default-competition-settings";

    public string Id { get; set; } = SingletonId;

    public bool RegistrationEnabled { get; set; }
    public DateTimeOffset? RegistrationStart { get; set; }
    public DateTimeOffset? RegistrationEnd { get; set; }
    public DateOnly AgeCutoffDate { get; set; } = new DateOnly(DateTime.UtcNow.Year, 1, 1);

    public List<Division> Divisions { get; set; } = new();
    public CompetitionInfo CompetitionInfo { get; set; } = new();
    public CidConfiguration CidConfiguration { get; set; } = new();

    public Division? FindDivision(string divisionId) =>
        Divisions.FirstOrDefault(d => d.Id == divisionId);
}
