using Microsoft.Extensions.DependencyInjection;
using StepIn.Domain.Aidx;
using StepIn.Infrastructure.Aidx;

namespace StepIn.Api.Operations;

/// <summary>
/// One-off operator command that creates the AIDX system owner:
///
///     dotnet StepIn.Api.dll aidx-init-owner
///
/// It is the only way the owner is created. It is not an HTTP endpoint, needs no Clerk login, and is
/// not run by startup, the container entrypoint, or migrations. It calls
/// <see cref="AidxSystemOwnershipService"/> and does nothing else: no migrations, no other records.
///
/// Exit codes: 0 = initialised or already consistent, 2 = inconsistent state (nothing written),
/// 1 = failure (nothing printed except the exception type, so no connection string or secret leaks).
/// </summary>
public static class AidxOwnerInitCommand
{
    public const string Name = "aidx-init-owner";

    public const int ExitOk = 0;
    public const int ExitFailed = 1;
    public const int ExitInconsistent = 2;

    public static async Task<int> RunAsync(IServiceProvider services, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(output);

        await using var scope = services.CreateAsyncScope();
        var ownership = scope.ServiceProvider.GetRequiredService<AidxSystemOwnershipService>();

        try
        {
            // The read-only check comes first, so a consistent owner is reported without any write.
            if (await ownership.FindAsync(cancellationToken) is not null)
            {
                await output.WriteLineAsync("AIDX system ownership already exists and is consistent.");
                return ExitOk;
            }

            // Creates only what is missing. A partially existing owner is completed; a conflicting one is refused.
            await ownership.EnsureAsync(cancellationToken);

            await output.WriteLineAsync("AIDX system ownership initialized successfully.");
            await output.WriteLineAsync($"System identity: {AidxSystemIdentity.ClerkUserId}");
            await output.WriteLineAsync($"Company: {AidxSystemIdentity.CompanyName}");
            await output.WriteLineAsync("Status: ready");
            return ExitOk;
        }
        catch (InvalidOperationException)
        {
            // The service throws InvalidOperationException only for its own refusals, and checks them before writing.
            await output.WriteLineAsync("AIDX system ownership is inconsistent.");
            await output.WriteLineAsync("No changes were made.");
            return ExitInconsistent;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await output.WriteLineAsync($"AIDX system ownership initialisation failed ({ex.GetType().Name}). Check database connectivity and the logs.");
            return ExitFailed;
        }
    }
}
