namespace RegistrationSystem.Core.Domain.Registrations;

/// <summary>
/// A competitor registration for a specific category in a competition year.
/// One registration = one category entry.
/// </summary>
public class Registration
{
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable Competitor ID. Format: [DivisionLetter][StateCode][Sequence]
    /// Example: M3001 = Memorization, MN (code 3), competitor #1
    /// </summary>
    public string? Cid { get; set; }

    /// <summary>
    /// ObjectIdentifier of the user who created this registration (parent/teacher/student).
    /// </summary>
    public string CreatorUserId { get; set; } = string.Empty;

    /// <summary>
    /// The competition year this registration is for.
    /// </summary>
    public int CompetitionYear { get; set; }

    /// <summary>
    /// Competitor's personal information.
    /// </summary>
    public PersonalInfo PersonalInfo { get; set; } = new();

    /// <summary>
    /// Competitor's address information.
    /// </summary>
    public AddressInfo AddressInfo { get; set; } = new();

    /// <summary>
    /// Division and category selection.
    /// </summary>
    public CompetitionSelection CompetitionSelection { get; set; } = new();

    /// <summary>
    /// Parent/Guardian contact information (required).
    /// </summary>
    public ParentInfo ParentInfo { get; set; } = new();

    /// <summary>
    /// Teacher/Institution information (optional).
    /// </summary>
    public TeacherInfo? TeacherInfo { get; set; }

    /// <summary>
    /// Uploaded file references (ID, Photo, Video).
    /// </summary>
    public FileUploadInfo FileUploadInfo { get; set; } = new();

    /// <summary>
    /// Current registration status.
    /// </summary>
    public RegistrationStatus Status { get; set; } = RegistrationStatus.AwaitingReview;

    /// <summary>
    /// Admin comment explaining status (e.g., why pending or disqualified).
    /// </summary>
    public string? StatusComment { get; set; }

    /// <summary>
    /// Reason for withdrawal (if withdrawn).
    /// </summary>
    public string? WithdrawComment { get; set; }

    /// <summary>
    /// Whether the user accepted terms for this registration.
    /// </summary>
    public bool TermsAccepted { get; set; }

    /// <summary>
    /// When terms were accepted.
    /// </summary>
    public DateTimeOffset? TermsAcceptedAt { get; set; }

    /// <summary>
    /// When this registration was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this registration was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When this registration was submitted.
    /// </summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>
    /// Calculates the competitor's age as of a specific date.
    /// </summary>
    public int CalculateAgeAsOf(DateOnly asOfDate)
    {
        var dob = PersonalInfo.DateOfBirth;
        var age = asOfDate.Year - dob.Year;
        if (asOfDate < dob.AddYears(age))
            age--;
        return age;
    }

    /// <summary>
    /// Checks if the registration can be edited based on status.
    /// </summary>
    public bool CanEdit => Status == RegistrationStatus.AwaitingReview || Status == RegistrationStatus.Pending;

    /// <summary>
    /// Checks if the registration can be withdrawn.
    /// </summary>
    public bool CanWithdraw => Status != RegistrationStatus.Withdrawn && Status != RegistrationStatus.Disqualified;
}

/// <summary>
/// Registration status values.
/// </summary>
public enum RegistrationStatus
{
    /// <summary>
    /// Submitted and waiting for admin review.
    /// </summary>
    AwaitingReview,

    /// <summary>
    /// Admin found issues, sent back to user for corrections.
    /// </summary>
    Pending,

    /// <summary>
    /// Admin approved, no further edits allowed.
    /// </summary>
    Reviewed,

    /// <summary>
    /// Final verification before competition (bulk action).
    /// </summary>
    Verified,

    /// <summary>
    /// User or admin withdrew the registration.
    /// </summary>
    Withdrawn,

    /// <summary>
    /// Disqualified due to eligibility issues (DOB mismatch, etc.).
    /// </summary>
    Disqualified
}

/// <summary>
/// Competitor's personal information.
/// </summary>
public class PersonalInfo
{
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string? PreferredName { get; set; }
    public Gender Gender { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Returns the full name (First Middle Last).
    /// </summary>
    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{FirstName} {LastName}"
        : $"{FirstName} {MiddleName} {LastName}";

    /// <summary>
    /// Returns display name (PreferredName if set, otherwise FirstName).
    /// </summary>
    public string DisplayName => !string.IsNullOrWhiteSpace(PreferredName)
        ? PreferredName
        : FirstName;
}

/// <summary>
/// Gender options.
/// </summary>
public enum Gender
{
    Male,
    Female
}

/// <summary>
/// Competitor's address information.
/// </summary>
public class AddressInfo
{
    public string Country { get; set; } = string.Empty;
    public string? StateProvince { get; set; }
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Returns formatted location string.
    /// </summary>
    public string FormattedLocation => string.IsNullOrWhiteSpace(StateProvince)
        ? $"{City}, {Country}"
        : $"{City}, {StateProvince}, {Country}";
}

/// <summary>
/// Division and category selection for the competition.
/// </summary>
public class CompetitionSelection
{
    public string DivisionId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>
    /// User's portion choice if the category allows TopOrBottom.
    /// </summary>
    public PortionChoice? PortionChoice { get; set; }
}

/// <summary>
/// Portion choice when category allows TopOrBottom.
/// </summary>
public enum PortionChoice
{
    Top,
    Bottom
}

/// <summary>
/// Parent/Guardian contact information.
/// </summary>
public class ParentInfo
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}";
}

/// <summary>
/// Teacher/Institution information.
/// </summary>
public class TeacherInfo
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Institution { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}

/// <summary>
/// References to uploaded files (stored in Azure Blob / Dropbox).
/// </summary>
public class FileUploadInfo
{
    /// <summary>
    /// Government-issued ID document.
    /// </summary>
    public FileMetadata? IdDocument { get; set; }

    /// <summary>
    /// Competitor photo (for face verification).
    /// </summary>
    public FileMetadata? Photo { get; set; }

    /// <summary>
    /// Recitation video (if required by category).
    /// </summary>
    public FileMetadata? Video { get; set; }

    /// <summary>
    /// Whether niqab bypass was requested (females only).
    /// When true, face detection and matching are skipped.
    /// </summary>
    public bool NiqabBypassApproved { get; set; }

    /// <summary>
    /// The niqab bypass code that was used (for audit).
    /// </summary>
    public string? NiqabBypassCode { get; set; }
}

/// <summary>
/// Metadata for an uploaded file.
/// </summary>
public class FileMetadata
{
    /// <summary>
    /// Original file name.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Storage reference (Azure Blob name or Dropbox path).
    /// </summary>
    public string StorageReference { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// File extension (e.g., ".jpg", ".pdf").
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// Content type (MIME type).
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// When the file was uploaded.
    /// </summary>
    public DateTimeOffset UploadedAt { get; set; }

    /// <summary>
    /// Validation result from image analysis / face detection.
    /// </summary>
    public FileValidationResult? ValidationResult { get; set; }
}

/// <summary>
/// Result of file validation (image analysis, face detection, etc.).
/// </summary>
public class FileValidationResult
{
    /// <summary>
    /// Whether the file passed validation.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Method used for validation (e.g., "AzureImageAnalysis", "FaceDetection", "Bypassed").
    /// </summary>
    public string? ValidationMethod { get; set; }

    /// <summary>
    /// Details or reason for validation result.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// When validation was performed.
    /// </summary>
    public DateTimeOffset ValidatedAt { get; set; }
}