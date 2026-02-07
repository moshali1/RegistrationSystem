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
            && a.ScreeningRoundEnabled == b.ScreeningRoundEnabled
            && a.AllowMultipleInDivision == b.AllowMultipleInDivision
            && a.AllowEdit == b.AllowEdit
            && a.AllowWithdraw == b.AllowWithdraw;
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
}
