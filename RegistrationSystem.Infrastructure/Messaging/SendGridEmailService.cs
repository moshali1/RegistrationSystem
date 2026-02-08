using Microsoft.Extensions.Logging;
using RegistrationSystem.Core.Application.Messaging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace RegistrationSystem.Infrastructure.Messaging;

public class SendGridEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SendGridEmailService> _logger;

    public SendGridEmailService(EmailOptions options, ILogger<SendGridEmailService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(
        string toEmail,
        string toName,
        string subject,
        string plainTextContent,
        string htmlContent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SendGridApiKey))
        {
            _logger.LogWarning("SendGrid API key is not configured. Email to {ToEmail} was not sent.", toEmail);
            return false;
        }

        try
        {
            var client = new SendGridClient(_options.SendGridApiKey);
            var from = new EmailAddress(_options.FromAddress, _options.FromDisplayName);
            var to = new EmailAddress(toEmail, toName);
            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

            var response = await client.SendEmailAsync(msg, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent to {ToEmail}, Subject: {Subject}", toEmail, subject);
                return true;
            }

            var body = await response.Body.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "SendGrid returned {StatusCode} for email to {ToEmail}. Body: {Body}",
                response.StatusCode, toEmail, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail}, Subject: {Subject}", toEmail, subject);
            return false;
        }
    }
}
