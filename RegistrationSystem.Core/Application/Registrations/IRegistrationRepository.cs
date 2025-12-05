namespace RegistrationSystem.Core.Domain.Registrations;

/// <summary>
/// Repository interface for Registration entities.
/// </summary>
public interface IRegistrationRepository
{
    /// <summary>
    /// Gets a registration by its ID.
    /// </summary>
    Task<Registration?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all registrations created by a specific user.
    /// </summary>
    Task<IReadOnlyList<Registration>> GetByCreatorUserIdAsync(string creatorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all registrations for a specific competition year.
    /// </summary>
    Task<IReadOnlyList<Registration>> GetByCompetitionYearAsync(int competitionYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets registrations by creator and competition year.
    /// </summary>
    Task<IReadOnlyList<Registration>> GetByCreatorAndYearAsync(string creatorUserId, int competitionYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds registrations matching the competitor's first name, last name, and date of birth.
    /// Used for duplicate detection.
    /// </summary>
    Task<IReadOnlyList<Registration>> FindDuplicatesAsync(
        string firstName,
        string lastName,
        DateOnly dateOfBirth,
        int competitionYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets registrations by creator, division, and year.
    /// Used to check multiple registration rules.
    /// </summary>
    Task<IReadOnlyList<Registration>> GetByCreatorDivisionAndYearAsync(
        string creatorUserId,
        string divisionId,
        int competitionYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets registrations by status for a competition year.
    /// </summary>
    Task<IReadOnlyList<Registration>> GetByStatusAsync(
        RegistrationStatus status,
        int competitionYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a registration (insert or update).
    /// </summary>
    Task SaveAsync(Registration registration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a registration by ID.
    /// </summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts registrations by status for a competition year.
    /// </summary>
    Task<int> CountByStatusAsync(RegistrationStatus status, int competitionYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets total registration count for a competition year.
    /// </summary>
    Task<int> CountByYearAsync(int competitionYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the maximum sequence number from existing CIDs with the given prefix.
    /// Used for CID generation. Returns 0 if no matching CIDs exist.
    /// </summary>
    /// <param name="competitionYear">Competition year to search.</param>
    /// <param name="cidPrefix">CID prefix (e.g., "M3" for Memorization/MN).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Maximum sequence number, or 0 if none exist.</returns>
    Task<int> GetMaxCidSequenceAsync(int competitionYear, string cidPrefix, CancellationToken cancellationToken = default);
}