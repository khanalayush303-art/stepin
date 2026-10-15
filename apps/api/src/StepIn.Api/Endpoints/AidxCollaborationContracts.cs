namespace StepIn.Api.Endpoints;

public sealed record SubmitCollaborationEnquiryRequest(string Name, string Email, string Message);

public sealed record AidxCollaborationEnquiryResponse(
    Guid Id, DateTimeOffset CreatedAt, string Name, string Email, string Message, string Status);

/// <summary><paramref name="Status"/> must be "Reviewed" or "Closed" — an inbox viewer can never
/// set an enquiry back to "New" (that state is only reachable by actually submitting one).</summary>
public sealed record UpdateAidxCollaborationEnquiryStatusRequest(string Status);
