using FluentAssertions;
using Microsoft.Extensions.Configuration;
using StepIn.Infrastructure.Storage;

namespace StepIn.Api.Tests;

/// <summary>
/// Exercises <see cref="LocalDiskResumeStorage"/> in isolation against a
/// temporary directory — never the real production or development
/// filesystem. Added during the Phase 3.1 production-storage investigation:
/// the production defect found was a missing <c>Storage__ResumeRootPath</c>
/// secret causing the app to fall back to an unwritable path inside the
/// container (see the Phase 3.1 checkpoint report), not a bug in this class.
/// These tests pin down that both the configured-path and fallback-path
/// branches behave correctly and deterministically, so that distinction is
/// never in doubt again.
/// </summary>
public sealed class LocalDiskResumeStorageTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"stepin-resume-tests-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private LocalDiskResumeStorage CreateConfiguredStorage()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:ResumeRootPath"] = _tempRoot })
            .Build();

        return new LocalDiskResumeStorage(config);
    }

    [Fact]
    public async Task Save_then_open_read_round_trips_the_exact_bytes_under_the_configured_root()
    {
        var ct = TestContext.Current.CancellationToken;
        var storage = CreateConfiguredStorage();
        var original = "test resume content"u8.ToArray();

        string key;
        await using (var source = new MemoryStream(original))
        {
            key = await storage.SaveAsync(source, ".pdf", ct);
        }

        key.Should().EndWith(".pdf");
        File.Exists(Path.Combine(_tempRoot, key)).Should().BeTrue("SaveAsync must write under the configured root, not the fallback");

        await using var read = await storage.OpenReadAsync(key, ct);
        read.Should().NotBeNull();
        using var buffer = new MemoryStream();
        await read!.CopyToAsync(buffer, ct);
        buffer.ToArray().Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task OpenReadAsync_returns_null_for_a_key_that_was_never_saved()
    {
        var ct = TestContext.Current.CancellationToken;
        var storage = CreateConfiguredStorage();

        var result = await storage.OpenReadAsync($"{Guid.NewGuid():N}.pdf", ct);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_removes_a_saved_file_and_is_a_no_op_if_already_gone()
    {
        var ct = TestContext.Current.CancellationToken;
        var storage = CreateConfiguredStorage();
        string key;
        await using (var source = new MemoryStream("x"u8.ToArray()))
        {
            key = await storage.SaveAsync(source, ".pdf", ct);
        }

        await storage.DeleteAsync(key, ct);
        (await storage.OpenReadAsync(key, ct)).Should().BeNull();

        // Deleting an already-gone file must not throw — this is the race-
        // loser cleanup path ApplicationEndpoints relies on.
        var act = async () => await storage.DeleteAsync(key, ct);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OpenReadAsync_strips_any_path_component_from_the_storage_key_before_combining_with_the_root()
    {
        var ct = TestContext.Current.CancellationToken;
        var storage = CreateConfiguredStorage();

        // storageKey is always server-generated in production, but this
        // proves the defense-in-depth Path.GetFileName() strip actually
        // works: a key smuggling a parent-directory traversal can never
        // escape the configured root.
        var result = await storage.OpenReadAsync("../../etc/passwd", ct);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Without_configuration_the_root_falls_back_to_AppContext_BaseDirectory_App_Data_resumes()
    {
        // Pins down LocalDiskResumeStorage.cs's exact fallback formula
        // (`?? Path.Combine(AppContext.BaseDirectory, "App_Data", "resumes")`)
        // so a future change to it can't go unnoticed. This is the path
        // actually active in production whenever Storage:ResumeRootPath is
        // unset — Phase 3.1 found that exact path unwritable by the
        // production container's non-root user (root-owned /app, mode 755).
        // This test runs as a normal local/CI user with no such restriction,
        // so it can only prove the formula is deterministic, not that the
        // path is writable in every host — that fact was verified separately
        // against the live container (see the Phase 3.1 report) and cannot
        // be reproduced in a unit test.
        var ct = TestContext.Current.CancellationToken;
        var expectedRoot = Path.Combine(AppContext.BaseDirectory, "App_Data", "resumes");
        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var storage = new LocalDiskResumeStorage(config);

        string key;
        await using (var source = new MemoryStream("x"u8.ToArray()))
        {
            key = await storage.SaveAsync(source, ".pdf", ct);
        }

        var savedPath = Path.Combine(expectedRoot, key);
        try
        {
            File.Exists(savedPath).Should().BeTrue();
        }
        finally
        {
            File.Delete(savedPath);
        }
    }
}
