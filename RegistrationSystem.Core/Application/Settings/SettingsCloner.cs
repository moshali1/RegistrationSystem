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
            PendingDeadline = source.PendingDeadline,
            AgeCutoffDate = source.AgeCutoffDate,
            Divisions = source.Divisions.Select(CloneDivision).ToList(),
            CompetitionInfo = CloneCompetitionInfo(source.CompetitionInfo),
            CidConfiguration = CloneCidConfiguration(source.CidConfiguration),
            EmailDefaults = CloneEmailDefaults(source.EmailDefaults)
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
            AllowMultipleInDivision = source.AllowMultipleInDivision,
            AllowEdit = source.AllowEdit,
            AllowWithdraw = source.AllowWithdraw,
            Rounds = source.Rounds.Select(CloneRoundDefinition).ToList()
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
        target.AllowMultipleInDivision = source.AllowMultipleInDivision;
        target.AllowEdit = source.AllowEdit;
        target.AllowWithdraw = source.AllowWithdraw;
        target.Rounds = source.Rounds.Select(CloneRoundDefinition).ToList();
    }

    public static RoundDefinition CloneRoundDefinition(RoundDefinition source)
    {
        return new RoundDefinition
        {
            Id = source.Id,
            Order = source.Order,
            Name = source.Name,
            HasSchedule = source.HasSchedule,
            ResultType = source.ResultType,
            AllowBypass = source.AllowBypass,
            HasPlacement = source.HasPlacement,
            PassMessage = source.PassMessage,
            FailMessage = source.FailMessage,
            ScheduleDetails = source.ScheduleDetails
        };
    }

    private static CompetitionInfo CloneCompetitionInfo(CompetitionInfo? source)
    {
        if (source is null)
            return new CompetitionInfo();

        return new CompetitionInfo
        {
            CompetitionName = source.CompetitionName,
            CompetitionYear = source.CompetitionYear,
            PrivacyPolicyUrl = source.PrivacyPolicyUrl,
            TermsOfServiceUrl = source.TermsOfServiceUrl,
            RulesUrl = source.RulesUrl
        };
    }

    private static CidConfiguration CloneCidConfiguration(CidConfiguration? source)
    {
        if (source is null)
            return new CidConfiguration();

        return new CidConfiguration
        {
            StateCodeMapping = new Dictionary<string, string>(source.StateCodeMapping),
            DefaultStateCode = source.DefaultStateCode
        };
    }

    private static EmailDefaults CloneEmailDefaults(EmailDefaults? source)
    {
        if (source is null)
            return new EmailDefaults();

        return new EmailDefaults
        {
            PendingTemplateId = source.PendingTemplateId,
            VerifiedTemplateId = source.VerifiedTemplateId,
            DisqualifiedTemplateId = source.DisqualifiedTemplateId,
            WithdrawnTemplateId = source.WithdrawnTemplateId
        };
    }
}
