namespace StepIn.Application.Common.Interfaces;

/// <summary>Sends transactional email. Infrastructure decides how (SMTP today).</summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
