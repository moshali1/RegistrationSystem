using RegistrationSystem.Core.Domain.Registrations;

namespace RegistrationSystem.Core.Application.Registrations;

public interface IRegistrationRepository
{
    Task<Registration?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Registration>> GetByCreatorUserIdAsync(string creatorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Registration>> GetByCompetitionYearAsync(int competitionYear, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Registration>> GetByCreatorAndYearAsync(string creatorUserId, int competitionYear, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Registration>> FindDuplicatesAsync(string firstName, string lastName, DateOnly dateOfBirth, int competitionYear, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Registration>> GetByCreatorDivisionAndYearAsync(string creatorUserId, string divisionId, int competitionYear, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Registration>> GetByStatusAsync(RegistrationStatus status, int competitionYear, CancellationToken cancellationToken = default);
    Task SaveAsync(Registration registration, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(string id, RegistrationStatus status, string? statusComment, string? withdrawComment, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<int> CountByStatusAsync(RegistrationStatus status, int competitionYear, CancellationToken cancellationToken = default);
    Task<int> CountByYearAsync(int competitionYear, CancellationToken cancellationToken = default);
    Task<int> GetMaxCidSequenceAsync(int competitionYear, string cidPrefix, CancellationToken cancellationToken = default);
    Task<int> GetNextCidSequenceAsync(int competitionYear, string cidPrefix, CancellationToken cancellationToken = default);
    Task UpdateCidAsync(string id, string newCid, CancellationToken cancellationToken = default);
}
