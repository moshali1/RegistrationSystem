using RegistrationSystem.Core.Domain.Consents;

namespace RegistrationSystem.Core.Application.Consents;

public class ConsentService
{
    private readonly IConsentRepository _repository;

    public ConsentService(IConsentRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Records a user's consent for the competition.
    /// </summary>
    public async Task RecordConsentAsync(
        string userId,
        ConsentRole role,
        int competitionYear,
        string competitionName,
        string signedName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        // Check if already consented for this year
        var existingConsent = await _repository.GetByUserAndYearAsync(userId, competitionYear, cancellationToken);
        if (existingConsent is not null)
        {
            throw new InvalidOperationException($"Consent has already been recorded for competition year {competitionYear}.");
        }

        var consent = new ConsentRecord
        {
            UserId = userId,
            CompetitionYear = competitionYear,
            Role = role,
            ConsentedAt = DateTimeOffset.UtcNow,
            SignedName = signedName.Trim(),
            ConsentTextVersion = ConsentTexts.GetVersion(role, competitionYear),
            ConsentTextSnapshot = ConsentTexts.GetConsentText(role, competitionName, competitionYear),
            AcknowledgedPrivacyPolicy = true,
            AcknowledgedTermsOfService = true,
            AllowsDataCollectionForRegistration = true,
            AllowsIdAndPhotoUploadForVerification = true,
            AllowsLivestreamAndRecording = true,
            AllowsUseInPromotionalMaterial = true,
            AllowsWinnerAnnouncementAndUmrahProcessing = true,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        await _repository.AddAsync(consent, cancellationToken);
    }

    /// <summary>
    /// Gets a user's consent for a specific competition year.
    /// </summary>
    public Task<ConsentRecord?> GetConsentAsync(string userId, int competitionYear, CancellationToken cancellationToken = default)
        => _repository.GetByUserAndYearAsync(userId, competitionYear, cancellationToken);

    /// <summary>
    /// Checks if a user has consented for a specific competition year.
    /// </summary>
    public Task<bool> HasConsentedAsync(string userId, int competitionYear, CancellationToken cancellationToken = default)
        => _repository.HasConsentedAsync(userId, competitionYear, cancellationToken);

    /// <summary>
    /// Gets all consent records for a user.
    /// </summary>
    public Task<IReadOnlyList<ConsentRecord>> GetUserConsentsAsync(string userId, CancellationToken cancellationToken = default)
        => _repository.GetByUserIdAsync(userId, cancellationToken);

    /// <summary>
    /// Determines the consent role based on user type.
    /// For Students, they must be 18+ (adult student).
    /// </summary>
    public static ConsentRole GetConsentRoleFromUserType(string userType) => userType.ToLowerInvariant() switch
    {
        "parent" => ConsentRole.ParentGuardian,
        "teacher" => ConsentRole.Teacher,
        "student" => ConsentRole.AdultStudent,
        _ => throw new ArgumentException($"Invalid user type: {userType}", nameof(userType))
    };
}