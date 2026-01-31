namespace RegistrationSystem.Core.Domain.Registrations;

public class Registration
{
    public string Id { get; set; } = string.Empty;
    public string? Cid { get; set; }
    public string CreatorUserId { get; set; } = string.Empty;
    public int CompetitionYear { get; set; }

    public PersonalInfo PersonalInfo { get; set; } = new();
    public AddressInfo AddressInfo { get; set; } = new();
    public CompetitionSelection CompetitionSelection { get; set; } = new();
    public ParentInfo ParentInfo { get; set; } = new();
    public TeacherInfo? TeacherInfo { get; set; }
    public FileUploadInfo FileUploadInfo { get; set; } = new();

    public RegistrationStatus Status { get; set; } = RegistrationStatus.AwaitingReview;
    public string? StatusComment { get; set; }
    public string? WithdrawComment { get; set; }
    public bool TermsAccepted { get; set; }

    public int CalculateAgeAsOf(DateOnly asOfDate)
    {
        var dob = PersonalInfo.DateOfBirth;
        var age = asOfDate.Year - dob.Year;
        if (asOfDate < dob.AddYears(age))
            age--;
        return age;
    }
}

public enum RegistrationStatus
{
    AwaitingReview,
    Pending,
    Reviewed,
    Verified,
    Withdrawn,
    Disqualified
}
