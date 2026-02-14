namespace RegistrationSystem.Core.Domain.Settings;

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

    public bool ScreeningRoundEnabled { get; set; }

    public bool AllowMultipleInDivision { get; set; }
    public bool AllowEdit { get; set; } = true;
    public bool AllowWithdraw { get; set; } = true;

    /// <summary>
    /// Ordered list of round definitions for this category's competition pipeline.
    /// Each entry defines a round's name, type, result mode, and messaging.
    /// </summary>
    public List<RoundDefinition> Rounds { get; set; } = new();
}

public enum PortionOption
{
    NotApplicable = 0,
    TopOnly = 1,
    BottomOnly = 2,
    TopOrBottom = 3
}
