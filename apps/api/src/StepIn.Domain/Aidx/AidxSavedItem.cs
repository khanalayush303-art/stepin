using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

/// <summary>A signed-in user's bookmark of a published Project or Publication. Mirrors
/// SavedJob's shape exactly: a thin join, binary (exists or doesn't), ownership always
/// resolved server-side.</summary>
public sealed class AidxSavedItem : Entity
{
    public required Guid UserId { get; set; }

    /// <summary>Only Project or Publication are ever valid here — enforced in the endpoint,
    /// not by the type system, since AidxContentType also includes News/Event.</summary>
    public AidxContentType ContentType { get; set; }

    public required Guid ContentId { get; set; }
}
