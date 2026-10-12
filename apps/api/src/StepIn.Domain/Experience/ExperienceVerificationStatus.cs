namespace StepIn.Domain.Experience;

/// <summary>
/// The full state machine is enforced server-side in StepIn.Api.Endpoints.ExperienceEndpoints
/// and StepIn.Api.Endpoints.ExperienceAdminEndpoints — never by this enum alone and never by the
/// frontend. Valid transitions:
///
///   Draft       --Submit(candidate)-->      Submitted
///   Submitted   --BeginReview(admin)-->      UnderReview
///   Submitted   --Withdraw(candidate)-->     Withdrawn
///   UnderReview --Approve(admin)-->          Verified
///   UnderReview --Reject(admin)-->           Rejected
///   UnderReview --Withdraw(candidate)-->     Withdrawn
///   Verified    --Withdraw(candidate)-->     Withdrawn   (the correction pathway: withdraw, edit, resubmit)
///   Rejected    --Resubmit(candidate)-->     Submitted
///   Withdrawn   --Resubmit(candidate)-->     Submitted
///
/// A record is only ever editable by its owner while Draft, Rejected, or Withdrawn — never while
/// Submitted, UnderReview, or Verified. Every transition above writes exactly one
/// ExperienceVerificationAudit row in the same database transaction as the status change.
/// </summary>
public enum ExperienceVerificationStatus
{
    Draft,
    Submitted,
    UnderReview,
    Verified,
    Rejected,
    Withdrawn,
}
