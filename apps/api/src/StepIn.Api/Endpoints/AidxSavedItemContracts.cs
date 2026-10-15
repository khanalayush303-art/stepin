namespace StepIn.Api.Endpoints;

public sealed record AidxSavedItemResponse(Guid Id, DateTimeOffset CreatedAt, string ContentType, Guid ContentId, string Title);
