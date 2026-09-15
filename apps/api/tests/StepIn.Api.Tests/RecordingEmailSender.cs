using System.Collections.Concurrent;
using StepIn.Application.Common.Interfaces;

namespace StepIn.Api.Tests;

/// <summary>
/// Stands in for the real SMTP sender in tests: no network sends, but every
/// call is recorded so a test can pull the verification/reset link straight
/// out of the "sent" email instead of reaching into Identity internals.
/// </summary>
public sealed class RecordingEmailSender : IEmailSender
{
    public ConcurrentBag<SentEmail> SentMessages { get; } = [];

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        SentMessages.Add(new SentEmail(toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }

    public SentEmail? LastMessageTo(string email) =>
        SentMessages.Where(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase)).LastOrDefault();

    public sealed record SentEmail(string To, string Subject, string HtmlBody);
}
