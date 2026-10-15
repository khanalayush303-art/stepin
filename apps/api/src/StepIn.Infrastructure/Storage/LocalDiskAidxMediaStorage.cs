using Microsoft.Extensions.Configuration;
using StepIn.Application.Common.Interfaces;

namespace StepIn.Infrastructure.Storage;

/// <summary>
/// Local-development mechanism only — see <see cref="LocalDiskResumeStorage"/>'s own doc comment
/// for the same caveat about container-host durability. Separate root directory from resumes.
/// </summary>
public sealed class LocalDiskAidxMediaStorage(IConfiguration configuration) : IAidxMediaStorage
{
    private readonly string _root = configuration["Storage:AidxMediaRootPath"]
        ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "aidx-media");

    public async Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_root);

        var storageKey = $"{Guid.NewGuid():N}{fileExtension}";
        var path = Path.Combine(_root, storageKey);

        await using var fileStream = File.Create(path);
        await content.CopyToAsync(fileStream, cancellationToken);

        return storageKey;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
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
