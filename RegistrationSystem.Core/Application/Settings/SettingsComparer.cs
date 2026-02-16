using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Core.Application.Settings;

public static class SettingsComparer
{
    public static bool AreEqual(CompetitionSettings? a, CompetitionSettings? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a.RegistrationEnabled != b.RegistrationEnabled) return false;
        if (a.RegistrationStart != b.RegistrationStart) return false;
        if (a.RegistrationEnd != b.RegistrationEnd) return false;
        if (a.AgeCutoffDate != b.AgeCutoffDate) return false;
        if (a.Divisions.Count != b.Divisions.Count) return false;
        for (int i = 0; i < a.Divisions.Count; i++)
        {
            if (!AreDivisionsEqual(a.Divisions[i], b.Divisions[i])) return false;
        }
        if (!AreCompetitionInfoEqual(a.CompetitionInfo, b.CompetitionInfo)) return false;
        if (!AreCidConfigurationEqual(a.CidConfiguration, b.CidConfiguration)) return false;
        return true;
    }

    public static bool AreDivisionsEqual(Division a, Division b)
    {
        if (a.Id != b.Id) return false;
        if (a.Name != b.Name) return false;
        if (a.IsEnabled != b.IsEnabled) return false;
        if (a.Categories.Count != b.Categories.Count) return false;

        for (int i = 0; i < a.Categories.Count; i++)
        {
            if (!AreCategoriesEqual(a.Categories[i], b.Categories[i])) return false;
        }
        return true;
    }

    public static bool AreCategoriesEqual(Category a, Category b)
    {
        return a.Id == b.Id
            && a.Name == b.Name
            && a.AlternateName == b.AlternateName
            && a.IsEnabled == b.IsEnabled
            && a.PortionOption == b.PortionOption
            && a.MaxAgeYears == b.MaxAgeYears
            && a.RegistrationStart == b.RegistrationStart
            && a.RegistrationEnd == b.RegistrationEnd
            && a.RequiresVideo == b.RequiresVideo
            && a.VideoInstructions == b.VideoInstructions
            && a.AllowMultipleInDivision == b.AllowMultipleInDivision
            && a.AllowEdit == b.AllowEdit
            && a.AllowWithdraw == b.AllowWithdraw
            && AreRoundsEqual(a.Rounds, b.Rounds);
    }

    private static bool AreRoundsEqual(List<RoundDefinition> a, List<RoundDefinition> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!AreRoundDefinitionsEqual(a[i], b[i])) return false;
        }
        return true;
    }

    private static bool AreRoundDefinitionsEqual(RoundDefinition a, RoundDefinition b)
    {
        return a.Id == b.Id
            && a.Order == b.Order
            && a.Name == b.Name
            && a.HasSchedule == b.HasSchedule
            && a.ResultType == b.ResultType
            && a.AllowBypass == b.AllowBypass
            && a.HasPlacement == b.HasPlacement
            && a.PassMessage == b.PassMessage
            && a.FailMessage == b.FailMessage
            && a.ScheduleDetails == b.ScheduleDetails;
    }

    private static bool AreCompetitionInfoEqual(CompetitionInfo? a, CompetitionInfo? b)
    {
        if (a is null || b is null) return a is null && b is null;
        return a.CompetitionName == b.CompetitionName
            && a.CompetitionYear == b.CompetitionYear
            && a.PrivacyPolicyUrl == b.PrivacyPolicyUrl
            && a.TermsOfServiceUrl == b.TermsOfServiceUrl
            && a.RulesUrl == b.RulesUrl;
    }

    private static bool AreCidConfigurationEqual(CidConfiguration? a, CidConfiguration? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a.DefaultStateCode != b.DefaultStateCode) return false;
        if (a.StateCodeMapping.Count != b.StateCodeMapping.Count) return false;

        foreach (var kvp in a.StateCodeMapping)
        {
            if (!b.StateCodeMapping.TryGetValue(kvp.Key, out var bValue) || kvp.Value != bValue)
                return false;
        }

        return true;
    }
}
