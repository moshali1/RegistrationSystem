using RegistrationSystem.Core.Domain.Messaging;

namespace RegistrationSystem.Core.Application.Messaging;

public interface IEmailTemplateRepository
{
    Task<List<EmailTemplate>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<EmailTemplate?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task SaveAsync(EmailTemplate template, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
