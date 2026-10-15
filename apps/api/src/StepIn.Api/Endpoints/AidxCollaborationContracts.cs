namespace StepIn.Api.Endpoints;

public sealed record SubmitCollaborationEnquiryRequest(string Name, string Email, string Message);

public sealed record AidxCollaborationEnquiryResponse(
    Guid Id, DateTimeOffset CreatedAt, string Name, string Email, string Message, string Status);
