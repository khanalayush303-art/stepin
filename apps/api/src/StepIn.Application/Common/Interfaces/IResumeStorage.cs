namespace StepIn.Application.Common.Interfaces;

/// <summary>
/// Abstracts where resume files physically live. The Phase 2.4 implementation
/// is local disk (see <c>LocalDiskResumeStorage</c>) — a local-development
/// mechanism only, not a production-ready one; swapping in a real object
/// store later means implementing this interface again, not touching any
/// endpoint. Deliberately has no ASP.NET Core (<c>IFormFile</c>) or EF Core
/// types in its signature, matching the rest of this layer's boundary.
/// </summary>
public interface IResumeStorage
{
    /// <summary>Persists the stream under a new, server-generated key and returns that key.</summary>
    Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken);

    /// <summary>Opens a previously-saved file for reading, or null if it's missing.</summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>Removes a file — used only to clean up the losing side of a create-time race (see JobApplicationEndpoints).</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
