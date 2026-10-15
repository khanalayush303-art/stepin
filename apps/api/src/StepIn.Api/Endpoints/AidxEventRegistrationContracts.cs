namespace StepIn.Api.Endpoints;

public sealed record RegisterForAidxEventRequest(string? Name, string? Email);

/// <summary>CancellationToken is present only for an anonymous registration, and only in this
/// one response — it is never persisted in plaintext and never returned again.</summary>
public sealed record AidxEventRegistrationResponse(Guid Id, string Status, string? CancellationToken);

public sealed record CancelAidxEventRegistrationRequest(string? CancellationToken);

public sealed record AidxEventRegistrationSummaryResponse(int RegisteredCount, int? Capacity, bool IsFull);

public sealed record AidxEventRegistrationAdminResponse(Guid Id, DateTimeOffset CreatedAt, string Name, string Email, string Status, bool IsSignedIn);
