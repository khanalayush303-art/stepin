namespace StepIn.Api.Endpoints;

/// <summary><paramref name="Slug"/> is set for a Project (its public URL is slug-addressed) and
/// null for a Publication (addressed by id alone).</summary>
public sealed record AidxSavedItemResponse(Guid Id, DateTimeOffset CreatedAt, string ContentType, Guid ContentId, string Title, string? Slug = null);
