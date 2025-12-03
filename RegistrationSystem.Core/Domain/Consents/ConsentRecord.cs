namespace RegistrationSystem.Core.Domain.Consents;

/// <summary>
/// Records a user's consent for the competition.
/// Immutable once created - consent cannot be modified.
/// </summary>
public class ConsentRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The account (user) who gave this consent.
    /// For parent/guardian or teacher, this is their login account.
    /// For adult participants, this is their own account.
    /// </summary>
    public string UserId { get; init; } = default!;

    /// <summary>
    /// The competition year this consent applies to.
    /// </summary>
    public int CompetitionYear { get; init; }

    /// <summary>
    /// How this person is acting when giving consent
    /// (parent/guardian, teacher, or adult student 18+).
    /// </summary>
    public ConsentRole Role { get; init; }

    /// <summary>
    /// When the consent was recorded (UTC).
    /// </summary>
    public DateTimeOffset ConsentedAt { get; init; }

    /// <summary>
    /// The name they typed as their "signature".
    /// </summary>
    public string SignedName { get; init; } = default!;

    /// <summary>
    /// Logical version of consent text (e.g. "parent-v1-2026", "teacher-v1-2026", "adult-v1-2026").
    /// Lets you know which phrasing they saw.
    /// </summary>
    public string ConsentTextVersion { get; init; } = default!;

    /// <summary>
    /// Full snapshot of the consent text they agreed to at the time.
    /// This protects you if the text changes later.
    /// </summary>
    public string ConsentTextSnapshot { get; init; } = default!;

    /// <summary>
    /// Indicates they acknowledged the Privacy Policy.
    /// </summary>
    public bool AcknowledgedPrivacyPolicy { get; init; }

    /// <summary>
    /// Indicates they acknowledged the Terms of Service.
    /// </summary>
    public bool AcknowledgedTermsOfService { get; init; }

    // Structured flags for what this consent covers.
    // These mirror the Privacy Policy / ToS and make it easier to reason about consents in code.

    public bool AllowsDataCollectionForRegistration { get; init; }
    public bool AllowsIdAndPhotoUploadForVerification { get; init; }
    public bool AllowsLivestreamAndRecording { get; init; }
    public bool AllowsUseInPromotionalMaterial { get; init; }
    public bool AllowsWinnerAnnouncementAndUmrahProcessing { get; init; }

    // Audit trail fields

    /// <summary>
    /// IP address of the user when consent was given.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// Browser/device user agent string when consent was given.
    /// </summary>
    public string? UserAgent { get; init; }
}