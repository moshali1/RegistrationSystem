using RegistrationSystem.Core.Domain.Registrations;

namespace RegistrationSystem.Web.Services;

/// <summary>
/// DTO for persisting registration form data to localStorage.
/// Excludes file bytes (too large) but includes file metadata and temp blob references.
/// </summary>
public class RegistrationFormDraft
{
    // Personal Info
    public string FirstName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PreferredName { get; set; } = string.Empty;
    public int Gender { get; set; } // Stored as int for JSON simplicity
    public string? DateOfBirth { get; set; } // ISO string format
    public string CompetitorPhone { get; set; } = string.Empty;

    // Address
    public string Country { get; set; } = string.Empty;
    public string StateProvince { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    // Competition
    public string DivisionId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public int? PortionChoice { get; set; } // Stored as int for JSON simplicity

    // Parent
    public string ParentFirstName { get; set; } = string.Empty;
    public string ParentLastName { get; set; } = string.Empty;
    public string ParentPhone { get; set; } = string.Empty;

    // Teacher
    public string TeacherFirstName { get; set; } = string.Empty;
    public string TeacherLastName { get; set; } = string.Empty;
    public string TeacherPhone { get; set; } = string.Empty;
    public string TeacherInstitution { get; set; } = string.Empty;

    // Niqab bypass
    public bool NiqabBypassApproved { get; set; }
    public string? NiqabBypassCode { get; set; }

    // File metadata (not bytes - those are in temp blob storage)
    // ID Document
    public string? IdDocumentTempBlobName { get; set; }
    public string? IdDocumentFileName { get; set; }
    public string? IdDocumentContentType { get; set; }
    public long IdDocumentSize { get; set; }
    public bool IdValidated { get; set; }

    // Photo
    public string? PhotoTempBlobName { get; set; }
    public string? PhotoFileName { get; set; }
    public string? PhotoContentType { get; set; }
    public long PhotoSize { get; set; }
    public bool PhotoValidated { get; set; }

    // Video (already uploaded to Azure)
    public string? VideoBlobName { get; set; }
    public string? VideoBlobUri { get; set; }
    public string? VideoFileName { get; set; }
    public string? VideoContentType { get; set; }
    public long VideoSize { get; set; }
    public bool VideoValidated { get; set; }
    public bool VideoUploaded { get; set; }

    // Current step for UX continuity
    public int CurrentStep { get; set; } = 1;

    // Timestamp for staleness check
    public DateTimeOffset SavedAt { get; set; }

    /// <summary>
    /// Check if draft is stale (older than specified hours)
    /// </summary>
    public bool IsStale(int maxAgeHours = 24)
    {
        return DateTimeOffset.UtcNow - SavedAt > TimeSpan.FromHours(maxAgeHours);
    }
}