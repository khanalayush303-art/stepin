namespace StepIn.Domain.Common;

/// <summary>
/// Base type for aggregate roots and entities.
/// Identity is a GUID v7-style value so rows stay roughly time-ordered in
/// Postgres without leaking a sequential counter to clients.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; protected set; }

    public DateTimeOffset? UpdatedAt { get; protected set; }

    /// <summary>Called by the persistence layer when the row is first written.</summary>
    public void MarkCreated(DateTimeOffset at) => CreatedAt = at;

    /// <summary>Called by the persistence layer on every subsequent write.</summary>
    public void MarkUpdated(DateTimeOffset at) => UpdatedAt = at;
}
