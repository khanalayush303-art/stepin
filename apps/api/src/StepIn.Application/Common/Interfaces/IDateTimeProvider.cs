namespace StepIn.Application.Common.Interfaces;

/// <summary>
/// Abstracts the clock so time-dependent behaviour stays testable.
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
