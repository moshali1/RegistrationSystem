namespace RegistrationSystem.Core.Domain.Settings;

public class CompetitionSettings
{
    public string Id { get; set; } = "default-competition-settings";

    public bool RegistrationEnabled { get; set; }
    public DateTimeOffset? RegistrationStart { get; set; } // Normal default period for most categories
    public DateTimeOffset? RegistrationEnd { get; set; }

    public DateOnly AgeCutoffDate { get; set; } = new DateOnly(DateTime.UtcNow.Year, 1, 1);

    public List<Division> Divisions { get; set; } = new();

    // Helper methods
    public Division? FindDivision(string divisionId) =>
       Divisions.FirstOrDefault(d => d.Id == divisionId);
}

public class Division
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public DivisionRegistrationRules RegistrationRules { get; set; } = new();

    public List<Category> Categories { get; set; } = new();

    // Helper methods
    public Category? FindCategory(string categoryId) =>
        Categories.FirstOrDefault(c => c.Id == categoryId);
}

public class DivisionRegistrationRules
{
    public bool AllowCreate { get; set; }
    public bool AllowUpdate { get; set; }
    public bool AllowWithdraw { get; set; }
}

public class Category
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = false; // Default: category is disabled until explicitly turned on
    public int? MaxAgeYears { get; set; } // No MinAgeYears by default

    public DateTimeOffset? RegistrationStart { get; set; } // optional category specific such as special 3 Juz Category in Memorization division 
    public DateTimeOffset? RegistrationEnd { get; set; }

    public PortionOption PortionOption { get; set; } = PortionOption.NotApplicable;
}

public enum PortionOption
{
    NotApplicable = 0,
    TopOnly = 1,
    BottomOnly = 2,
    TopOrBottom = 3
}

