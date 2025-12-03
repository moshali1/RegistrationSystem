namespace RegistrationSystem.Core.Domain.Consents;

public interface IConsentRepository
{
    /// <summary>
    /// Gets a consent record by ID.
    /// </summary>
    Task<ConsentRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user's consent for a specific competition year.
    /// Returns null if no consent exists.
    /// </summary>
    Task<ConsentRecord?> GetByUserAndYearAsync(string userId, int competitionYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all consent records for a user.
    /// </summary>
    Task<IReadOnlyList<ConsentRecord>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new consent record.
    /// Consent records are immutable - use this only for new records.
    /// </summary>
    Task AddAsync(ConsentRecord consent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has consented for a specific competition year.
    /// </summary>
    Task<bool> HasConsentedAsync(string userId, int competitionYear, CancellationToken cancellationToken = default);
}
