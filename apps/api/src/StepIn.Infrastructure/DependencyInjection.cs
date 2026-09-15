using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StepIn.Application.Common.Interfaces;
using StepIn.Infrastructure.Email;
using StepIn.Infrastructure.Identity;
using StepIn.Infrastructure.Persistence;
using StepIn.Infrastructure.Services;

namespace StepIn.Infrastructure;

/// <summary>
/// Composition root for the infrastructure layer: PostgreSQL, EF Core, Identity
/// and the concrete implementations of the application layer's abstractions.
/// </summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "Postgres";

    private const string EmailConfirmationTokenProviderName = "EmailConfirmation";
    private const string PasswordResetTokenProviderName = "PasswordReset";

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

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", ApplicationDbContext.Schema);
                npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
            });
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                name: "postgres",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready", "db"]);

        AddIdentity(services);

        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        return services;
    }

    private static void AddIdentity(IServiceCollection services)
    {
        services.Configure<EmailConfirmationTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(24));
        services.Configure<PasswordResetTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(1));

        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;

                options.Tokens.EmailConfirmationTokenProvider = EmailConfirmationTokenProviderName;
                options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProviderName;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<EmailConfirmationTokenProvider<ApplicationUser>>(EmailConfirmationTokenProviderName)
            .AddTokenProvider<PasswordResetTokenProvider<ApplicationUser>>(PasswordResetTokenProviderName);
    }
}
