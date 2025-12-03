using RegistrationSystem.Core.Domain.Settings;
using System.Text;

namespace RegistrationSystem.Core.Application.Settings;

/// <summary>
/// Captures a point-in-time snapshot of CompetitionSettings for change detection.
/// Used to determine if the user has unsaved changes.
/// </summary>
public sealed class SettingsSnapshot
{
    private readonly string _serializedState;
    private readonly DateTime? _globalStart;
    private readonly DateTime? _globalEnd;
    private readonly Dictionary<string, bool> _categoryOverrides;

    public SettingsSnapshot(CompetitionSettings settings, DateTime? globalStart, DateTime? globalEnd)
    {
        _globalStart = globalStart;
        _globalEnd = globalEnd;
        _categoryOverrides = new Dictionary<string, bool>();

        // Track which categories have overrides
        foreach (var div in settings.Divisions)
        {
            foreach (var cat in div.Categories)
            {
                _categoryOverrides[cat.Id] = cat.RegistrationStart.HasValue || cat.RegistrationEnd.HasValue;
            }
        }

        // Create a serialized representation of the settings state
        _serializedState = SerializeSettings(settings, globalStart, globalEnd);
    }

    /// <summary>
    /// Checks if a category had a schedule override at the time this snapshot was taken.
    /// Used to show the SCHED indicator only for saved overrides.
    /// </summary>
    public bool HasCategoryOverride(string categoryId)
    {
        return _categoryOverrides.TryGetValue(categoryId, out var hasOverride) && hasOverride;
    }

    /// <summary>
    /// Compares current settings state against this snapshot.
    /// Returns true if no changes have been made.
    /// </summary>
    public bool Matches(CompetitionSettings? settings, DateTime? globalStart, DateTime? globalEnd)
    {
        if (settings is null) return false;
        if (_globalStart != globalStart || _globalEnd != globalEnd) return false;
        return _serializedState == SerializeSettings(settings, globalStart, globalEnd);
    }

    private static string SerializeSettings(CompetitionSettings settings, DateTime? globalStart, DateTime? globalEnd)
    {
        var sb = new StringBuilder();
        sb.Append($"E:{settings.RegistrationEnabled}|");
        sb.Append($"GS:{globalStart}|GE:{globalEnd}|");
        sb.Append($"AC:{settings.AgeCutoffDate}|");

        foreach (var div in settings.Divisions.OrderBy(d => d.Id))
        {
            sb.Append($"D:{div.Id}:{div.Name}:{div.IsEnabled}:");
            sb.Append($"{div.RegistrationRules.AllowCreate}:{div.RegistrationRules.AllowUpdate}:{div.RegistrationRules.AllowWithdraw}|");

            foreach (var cat in div.Categories.OrderBy(c => c.Id))
            {
                sb.Append($"C:{cat.Id}:{cat.Name}:{cat.AlternateName}:{cat.IsEnabled}:{cat.MaxAgeYears}:{cat.PortionOption}:");
                sb.Append($"{cat.RegistrationStart}:{cat.RegistrationEnd}|");
            }
        }

        return sb.ToString();
    }
}
