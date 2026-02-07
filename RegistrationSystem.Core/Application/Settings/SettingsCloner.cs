using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Core.Application.Settings;

public static class SettingsCloner
{
    public static CompetitionSettings Clone(CompetitionSettings source)
    {
        return new CompetitionSettings
        {
            Id = source.Id,
            RegistrationEnabled = source.RegistrationEnabled,
            RegistrationStart = source.RegistrationStart,
            RegistrationEnd = source.RegistrationEnd,
            AgeCutoffDate = source.AgeCutoffDate,
            Divisions = source.Divisions.Select(CloneDivision).ToList(),
            CompetitionInfo = CloneCompetitionInfo(source.CompetitionInfo),
            CidConfiguration = source.CidConfiguration
        };
    }

    public static Division CloneDivision(Division source)
    {
        return new Division
        {
            Id = source.Id,
            Name = source.Name,
            IsEnabled = source.IsEnabled,
            Categories = source.Categories.Select(CloneCategory).ToList()
        };
    }

    public static Category CloneCategory(Category source)
    {
        return new Category
        {
            Id = source.Id,
            Name = source.Name,
            AlternateName = source.AlternateName,
            IsEnabled = source.IsEnabled,
            PortionOption = source.PortionOption,
            MaxAgeYears = source.MaxAgeYears,
            RegistrationStart = source.RegistrationStart,
            RegistrationEnd = source.RegistrationEnd,
            RequiresVideo = source.RequiresVideo,
            VideoInstructions = source.VideoInstructions,
            ScreeningRoundEnabled = source.ScreeningRoundEnabled,
            AllowMultipleInDivision = source.AllowMultipleInDivision,
            AllowEdit = source.AllowEdit,
            AllowWithdraw = source.AllowWithdraw
        };
    }

    public static void ApplyDivisionChanges(Division source, Division target)
    {
        target.Name = source.Name;
        target.IsEnabled = source.IsEnabled;
    }

    public static void ApplyCategoryChanges(Category source, Category target)
    {
        target.Name = source.Name;
        target.AlternateName = source.AlternateName;
        target.IsEnabled = source.IsEnabled;
        target.PortionOption = source.PortionOption;
        target.MaxAgeYears = source.MaxAgeYears;
        target.RegistrationStart = source.RegistrationStart;
        target.RegistrationEnd = source.RegistrationEnd;
        target.RequiresVideo = source.RequiresVideo;
        target.VideoInstructions = source.VideoInstructions;
        target.ScreeningRoundEnabled = source.ScreeningRoundEnabled;
        target.AllowMultipleInDivision = source.AllowMultipleInDivision;
        target.AllowEdit = source.AllowEdit;
        target.AllowWithdraw = source.AllowWithdraw;
    }

    private static CompetitionInfo CloneCompetitionInfo(CompetitionInfo? source)
    {
        if (source is null)
            return new CompetitionInfo { CompetitionYear = DateTime.UtcNow.Year };

        return new CompetitionInfo
        {
            CompetitionName = source.CompetitionName,
            CompetitionYear = source.CompetitionYear,
            PrivacyPolicyUrl = source.PrivacyPolicyUrl,
            TermsOfServiceUrl = source.TermsOfServiceUrl,
            RulesUrl = source.RulesUrl
        };
    }
}
