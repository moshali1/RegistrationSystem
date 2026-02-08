namespace RegistrationSystem.Core.Application.Messaging;

public interface IEmailService
{
    Task<bool> SendEmailAsync(
        string toEmail,
        string toName,
        string subject,
        string plainTextContent,
        string htmlContent,
        CancellationToken cancellationToken = default);
}
