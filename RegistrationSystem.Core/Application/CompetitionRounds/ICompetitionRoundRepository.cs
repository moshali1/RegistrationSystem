using RegistrationSystem.Core.Domain.CompetitionRounds;

namespace RegistrationSystem.Core.Application.CompetitionRounds;

public interface ICompetitionRoundRepository
{
    /// <summary>
    /// Gets a competition round by ID.
    /// </summary>
    Task<CompetitionRound?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompetitionRound>> GetByRegistrationIdsAsync(
    IEnumerable<string> registrationIds,
    CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a competition round by registration ID.
    /// </summary>
    Task<CompetitionRound?> GetByRegistrationIdAsync(string registrationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all competition rounds for a competition year.
    /// </summary>
    Task<IReadOnlyList<CompetitionRound>> GetByCompetitionYearAsync(
        int competitionYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets competition rounds by video qualification status.
    /// </summary>
    Task<IReadOnlyList<CompetitionRound>> GetByVideoQualificationStatusAsync(
        VideoQualificationStatus status,
        int competitionYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets competition rounds with pending acknowledgments.
    /// </summary>
    Task<IReadOnlyList<CompetitionRound>> GetWithPendingAcknowledgmentsAsync(
        int competitionYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets competition rounds assigned to a specific preliminary round date.
    /// </summary>
    Task<IReadOnlyList<CompetitionRound>> GetByPreliminaryRoundDateAsync(
        DateOnly roundDate,
        int competitionYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets competition rounds assigned to a specific final round date.
    /// </summary>
    Task<IReadOnlyList<CompetitionRound>> GetByFinalRoundDateAsync(
        DateOnly roundDate,
        int competitionYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets competition rounds assigned to a specific preliminary round date/time.
    /// </summary>
    Task<IReadOnlyList<CompetitionRound>> GetByPreliminaryRoundDateTimeAsync(
        DateTimeOffset roundDateTime,
        int competitionYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets competition rounds assigned to a specific final round date/time.
    /// </summary>
    Task<IReadOnlyList<CompetitionRound>> GetByFinalRoundDateTimeAsync(
        DateTimeOffset roundDateTime,
        int competitionYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a competition round (insert or update).
    /// </summary>
    Task SaveAsync(CompetitionRound round, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a competition round by ID.
    /// </summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a competition round by registration ID.
    /// </summary>
    Task DeleteByRegistrationIdAsync(string registrationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts competition rounds by video qualification status.
    /// </summary>
    Task<int> CountByVideoQualificationStatusAsync(
        VideoQualificationStatus status,
        int competitionYear,
        CancellationToken cancellationToken = default);
}