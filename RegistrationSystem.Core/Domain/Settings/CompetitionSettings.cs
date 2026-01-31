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

public class Division
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<Category> Categories { get; set; } = new();

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
    public PortionOption PortionOption { get; set; } = PortionOption.NotApplicable;

    public DateTimeOffset? RegistrationStart { get; set; }
    public DateTimeOffset? RegistrationEnd { get; set; }

    public bool RequiresVideo { get; set; }
    public string? VideoInstructions { get; set; }

    public bool AllowMultipleInDivision { get; set; }
    public bool AllowEdit { get; set; } = true;
    public bool AllowWithdraw { get; set; } = true;
}

public enum PortionOption
{
    NotApplicable = 0,
    TopOnly = 1,
    BottomOnly = 2,
    TopOrBottom = 3
}
