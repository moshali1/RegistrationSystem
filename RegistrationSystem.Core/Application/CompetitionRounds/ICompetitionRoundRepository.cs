using RegistrationSystem.Core.Domain.CompetitionRounds;

namespace RegistrationSystem.Core.Application.CompetitionRounds;

public interface ICompetitionRoundRepository
{
    Task<CompetitionRound?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<CompetitionRound?> GetByRegistrationIdAsync(string registrationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompetitionRound>> GetByRegistrationIdsAsync(IEnumerable<string> registrationIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompetitionRound>> GetByCompetitionYearAsync(int competitionYear, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompetitionRound>> GetByVideoQualificationStatusAsync(VideoQualificationStatus status, int competitionYear, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompetitionRound>> GetWithPendingAcknowledgmentsAsync(int competitionYear, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompetitionRound>> GetByPreliminaryRoundDateAsync(DateOnly roundDate, int competitionYear, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompetitionRound>> GetByFinalRoundDateAsync(DateOnly roundDate, int competitionYear, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompetitionRound>> GetByPreliminaryRoundDateTimeAsync(DateTimeOffset roundDateTime, int competitionYear, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompetitionRound>> GetByFinalRoundDateTimeAsync(DateTimeOffset roundDateTime, int competitionYear, CancellationToken cancellationToken = default);
    Task SaveAsync(CompetitionRound round, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task DeleteByRegistrationIdAsync(string registrationId, CancellationToken cancellationToken = default);
    Task<int> CountByVideoQualificationStatusAsync(VideoQualificationStatus status, int competitionYear, CancellationToken cancellationToken = default);


    Task<IReadOnlyList<CompetitionRound>> GetByCategoryAsync(
        string categoryId,
        int competitionYear,
        CancellationToken cancellationToken = default);
}
