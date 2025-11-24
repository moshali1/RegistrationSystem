using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Core.Application.Settings;

public interface ICompetitionSettingsRepository
{
    Task<CompetitionSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CompetitionSettings settings, CancellationToken cancellationToken = default);
}
