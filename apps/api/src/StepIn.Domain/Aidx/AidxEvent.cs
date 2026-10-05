using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

/// <summary>Timestamps are stored as timestamptz. Display timezone belongs to the presentation layer.</summary>
public sealed class AidxEvent : Entity
{
    public required string Slug { get; set; }

    public required string Title { get; set; }

    public required string Description { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public string? Location { get; set; }

    public string? RegistrationUrl { get; set; }

    public string? SpeakerName { get; set; }

    public string? ImageKey { get; set; }

    public AidxContentStatus Status { get; set; } = AidxContentStatus.Draft;
}
