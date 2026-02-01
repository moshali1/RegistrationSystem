using RegistrationSystem.Core.Domain.Registrations;

namespace RegistrationSystem.Web.Components.Pages.Registration;

public class RegistrationFormState
{
    public string FirstName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PreferredName { get; set; } = string.Empty;
    public Gender Gender { get; set; } = Gender.Male;
    public DateOnly DateOfBirth { get; set; }
    public string CompetitorPhone { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;
    public string StateProvince { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    public byte[]? IdDocumentBytes { get; set; }
    public string? IdDocumentFileName { get; set; }
    public string? IdDocumentContentType { get; set; }
    public long IdDocumentSize { get; set; }
    public bool IdValidated { get; set; }

    public byte[]? PhotoBytes { get; set; }
    public string? PhotoFileName { get; set; }
    public string? PhotoContentType { get; set; }
    public long PhotoSize { get; set; }
    public bool PhotoValidated { get; set; }

    public bool NiqabBypassApproved { get; set; }
    public string? NiqabBypassCode { get; set; }

    public string? VideoFileName { get; set; }
    public string? VideoContentType { get; set; }
    public long VideoSize { get; set; }
    public string? VideoBlobUri { get; set; }
    public string? VideoBlobName { get; set; }
    public bool VideoValidated { get; set; }
    public bool VideoUploaded { get; set; }

    public string DivisionId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public PortionChoice? PortionChoice { get; set; }

    public string ParentFirstName { get; set; } = string.Empty;
    public string ParentLastName { get; set; } = string.Empty;
    public string ParentPhone { get; set; } = string.Empty;

    public string TeacherFirstName { get; set; } = string.Empty;
    public string TeacherLastName { get; set; } = string.Empty;
    public string TeacherPhone { get; set; } = string.Empty;
    public string TeacherInstitution { get; set; } = string.Empty;

    public bool TermsAccepted { get; set; }

    public bool HasIdDocument => IdDocumentBytes != null && IdDocumentBytes.Length > 0;
    public bool HasPhoto => PhotoBytes != null && PhotoBytes.Length > 0;
    public bool HasVideo => VideoUploaded && !string.IsNullOrEmpty(VideoBlobUri);

    public string GetFormattedFullName()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(FirstName)) parts.Add(FirstName.Trim());
        if (!string.IsNullOrWhiteSpace(MiddleName)) parts.Add(MiddleName.Trim());
        if (!string.IsNullOrWhiteSpace(LastName)) parts.Add(LastName.Trim());
        return string.Join(" ", parts);
    }

    public void ClearIdFile()
    {
        IdDocumentBytes = null;
        IdDocumentFileName = null;
        IdDocumentContentType = null;
        IdDocumentSize = 0;
        IdValidated = false;
    }

    public void ClearPhotoFile()
    {
        PhotoBytes = null;
        PhotoFileName = null;
        PhotoContentType = null;
        PhotoSize = 0;
        PhotoValidated = false;
    }

    public void ClearVideoFile()
    {
        VideoFileName = null;
        VideoContentType = null;
        VideoSize = 0;
        VideoBlobUri = null;
        VideoBlobName = null;
        VideoValidated = false;
        VideoUploaded = false;
    }
}
