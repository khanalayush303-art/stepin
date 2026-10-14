using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StepIn.Application.Common.Interfaces;
using StepIn.Infrastructure.Aidx;
using StepIn.Infrastructure.Persistence;
using StepIn.Infrastructure.Services;
using StepIn.Infrastructure.Storage;

namespace StepIn.Infrastructure;

/// <summary>
/// Composition root for the infrastructure layer: PostgreSQL, EF Core, and the
/// concrete implementations of the application layer's abstractions.
/// </summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "Postgres";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Failing at startup with a named cause beats a null-reference on the
            // first request that happens to touch the database.
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. " +
                "Set ConnectionStrings__Postgres in the environment or appsettings.");
        }

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddResumeStorage(configuration);

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", ApplicationDbContext.Schema);
                npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
            });
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        // Resolves the AIDX system owner. Read-only for requests: it never creates records.
        services.AddScoped<AidxSystemOwnershipService>();

        // The one path that mutates AIDX editorial-workflow content — see its own doc comment.
        services.AddScoped<Aidx.AidxContentEditingService>();

        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                name: "postgres",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready", "db"]);

        return services;
    }

    /// <summary>
    /// Selects the resume storage backend from <c>Storage:Provider</c> ("Local", the default, or
    /// "Supabase"). Supabase's required configuration is validated here, at startup, rather than
    /// lazily on the first upload — a misconfigured deployment should fail to start, not fail a
    /// candidate's first application.
    /// </summary>
    private static IServiceCollection AddResumeStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Storage:Provider"];

        if (!string.Equals(provider, "Supabase", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IResumeStorage, LocalDiskResumeStorage>();
            return services;
        }

        var url = configuration["Storage:Supabase:Url"];
        var serviceKey = configuration["Storage:Supabase:ServiceKey"];
        var bucket = configuration["Storage:Supabase:Bucket"];

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(serviceKey) || string.IsNullOrWhiteSpace(bucket))
        {
            throw new InvalidOperationException(
                "Storage:Provider is 'Supabase' but Storage:Supabase:Url, :ServiceKey and :Bucket must all be " +
                "set. Set them via Storage__Supabase__Url, Storage__Supabase__ServiceKey and " +
                "Storage__Supabase__Bucket in the environment, or the equivalent appsettings keys.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("Storage:Supabase:Url must be an absolute URL, e.g. https://<project>.supabase.co.");
        }

        // A trailing slash here is required: HttpClient resolves a relative request URI against
        // BaseAddress by replacing everything after the last '/', so without it the bucket
        // segment would be silently dropped.
        var objectBaseUri = new Uri($"{baseUri.ToString().TrimEnd('/')}/storage/v1/object/{bucket.Trim('/')}/");

        services.AddHttpClient<IResumeStorage, SupabaseResumeStorage>(client =>
        {
            client.BaseAddress = objectBaseUri;
            client.Timeout = TimeSpan.FromSeconds(30);
            // The service-role key authorizes every request this client ever makes. It is set
            // once, here, and never logged, returned to a caller, or sent to the frontend.
            client.DefaultRequestHeaders.Add("apikey", serviceKey);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceKey);
        });

        return services;
    }
}
