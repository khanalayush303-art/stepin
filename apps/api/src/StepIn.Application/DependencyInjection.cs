using Microsoft.Extensions.DependencyInjection;

namespace StepIn.Application;

/// <summary>
/// Composition root for the application layer.
/// Phase 0 registers nothing beyond the seam itself — handlers, validators and
/// use cases are added here as each phase lands, so <c>Program.cs</c> never has
/// to learn about them.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
