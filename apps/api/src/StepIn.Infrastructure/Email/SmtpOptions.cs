namespace StepIn.Infrastructure.Email;

/// <summary>
/// Generic SMTP relay configuration. Works unchanged against any real provider's
/// SMTP endpoint (SendGrid, Mailgun, SES, a corporate relay, ...) — switching
/// providers is a config change, never a code change.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public required string Host { get; init; }

    public int Port { get; init; } = 587;

    public string? Username { get; init; }

    public string? Password { get; init; }

    public bool UseStartTls { get; init; } = true;

    public required string FromAddress { get; init; }

    public string FromName { get; init; } = "StepIn";
}
