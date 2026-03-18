using RegistrationSystem.Core.Domain.CompetitionRounds;

namespace RegistrationSystem.Core.Application.CompetitionRounds;

public interface ICompetitionProgressRepository
{
    Task<CompetitionProgress?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<CompetitionProgress?> GetByRegistrationIdAsync(string registrationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompetitionProgress>> GetByRegistrationIdsAsync(IEnumerable<string> registrationIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompetitionProgress>> GetByCompetitionYearAsync(int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompetitionProgress>> GetByCategoryAsync(string categoryId, int year, CancellationToken cancellationToken = default);
    Task SaveAsync(CompetitionProgress progress, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task DeleteByRegistrationIdAsync(string registrationId, CancellationToken cancellationToken = default);
    Task<int> DeleteByCategoryAsync(string categoryId, int year, CancellationToken cancellationToken = default);
}
