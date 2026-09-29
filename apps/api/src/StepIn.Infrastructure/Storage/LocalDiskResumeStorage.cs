using Microsoft.Extensions.Configuration;
using StepIn.Application.Common.Interfaces;

namespace StepIn.Infrastructure.Storage;

/// <summary>
/// Writes resume files to local disk. This is a Phase 2.4 local-development
/// mechanism only — the root directory is not durable on a stateless
/// container host (e.g. Fly.io's default ephemeral disk survives restarts
/// but not redeploys); it is made persistent for local `docker compose`
/// testing via a named volume (see docker-compose.yml), and that is the
/// extent of what this class claims to guarantee. Making resumes durable in
/// a real production deployment is a separate, later architecture decision,
/// not something this class solves.
/// </summary>
public sealed class LocalDiskResumeStorage(IConfiguration configuration) : IResumeStorage
{
    private readonly string _root = configuration["Storage:ResumeRootPath"]
        ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "resumes");

    public async Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);

        // Server-generated, opaque — never derived from anything the client sent.
        var storageKey = $"{Guid.NewGuid():N}{fileExtension}";
        var path = Path.Combine(_root, storageKey);

        await using var fileStream = File.Create(path);
        await content.CopyToAsync(fileStream, cancellationToken);

        return storageKey;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        // Defense in depth: storageKey is always a value this class generated
        // itself (see SaveAsync), but strip any path component before
        // combining anyway, so this can never escape _root even if that ever changes.
        var path = Path.Combine(_root, Path.GetFileName(storageKey));

        Stream? stream = File.Exists(path) ? File.OpenRead(path) : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_root, Path.GetFileName(storageKey));

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }
}
