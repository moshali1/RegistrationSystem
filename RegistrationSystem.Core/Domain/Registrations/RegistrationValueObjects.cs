namespace RegistrationSystem.Core.Domain.Registrations;

public class PersonalInfo
{
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string? PreferredName { get; set; }
    public Gender Gender { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }

    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{FirstName} {LastName}"
        : $"{FirstName} {MiddleName} {LastName}";

    public string DisplayName => !string.IsNullOrWhiteSpace(PreferredName)
        ? PreferredName
        : FirstName;
}

public enum Gender
{
    Male,
    Female
}

public class AddressInfo
{
    public string Country { get; set; } = string.Empty;
    public string? StateProvince { get; set; }
    public string City { get; set; } = string.Empty;

    public string FormattedLocation => string.IsNullOrWhiteSpace(StateProvince)
        ? $"{City}, {Country}"
        : $"{City}, {StateProvince}, {Country}";
}

public class CompetitionSelection
{
    public string DivisionId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public PortionChoice? PortionChoice { get; set; }
}

public enum PortionChoice
{
    Top,
    Bottom
}

public class ParentInfo
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}";
}

public class TeacherInfo
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Institution { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}

public class FileUploadInfo
{
    public FileMetadata? IdDocument { get; set; }
    public FileMetadata? Photo { get; set; }
    public FileMetadata? Video { get; set; }
    public bool NiqabBypassApproved { get; set; }
    public string? NiqabBypassCode { get; set; }
}

public class FileMetadata
{
    public string FileName { get; set; } = string.Empty;
    public string StorageReference { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Extension { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public FileValidationResult? ValidationResult { get; set; }
}

public class FileValidationResult
{
    public bool IsValid { get; set; }
    public string? Details { get; set; }
}
