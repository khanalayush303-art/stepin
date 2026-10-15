using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

/// <summary>A public visitor's enquiry about collaborating on a project. Minimal personal
/// information by design: name, email, and a free-text message — nothing else is collected.</summary>
public sealed class AidxCollaborationEnquiry : Entity
{
    public required Guid ProjectId { get; set; }

    public required string Name { get; set; }

    public required string Email { get; set; }

    public required string Message { get; set; }

    public AidxCollaborationEnquiryStatus Status { get; set; } = AidxCollaborationEnquiryStatus.New;
}
