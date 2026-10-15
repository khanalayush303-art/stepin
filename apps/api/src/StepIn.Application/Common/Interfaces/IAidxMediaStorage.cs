namespace StepIn.Application.Common.Interfaces;

/// <summary>
/// Abstracts where AIDX media (researcher profile photos, news/event images, publication PDFs)
/// physically lives. Deliberately separate from <see cref="IResumeStorage"/> — a different bucket,
/// a different retention/privacy story (public once the owning content is published; private,
/// proxy-only while it's a draft or an unmerged revision) — even though the storage mechanics are
/// nearly identical. Mirrors <see cref="IResumeStorage"/>'s shape on purpose: swapping either one
/// for a real object store later means implementing its interface again, not touching an endpoint.
/// </summary>
public interface IAidxMediaStorage
{
    /// <summary>Persists the stream under a new, server-generated key and returns that key.</summary>
    Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken);

    /// <summary>Opens a previously-saved file for reading, or null if it's missing.</summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>Removes a file — used when a key is replaced (e.g. a new image upload) and the old
    /// key is no longer referenced by any historical snapshot or revision.</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
