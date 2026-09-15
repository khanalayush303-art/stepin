using StepIn.Application.Common.Interfaces;

namespace StepIn.Infrastructure.Services;

/// <inheritdoc />
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
