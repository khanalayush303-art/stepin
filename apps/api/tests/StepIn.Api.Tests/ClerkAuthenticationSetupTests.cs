using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Infrastructure;

namespace StepIn.Api.Tests;

/// <summary>
/// A unit test, not an integration one — no Postgres, no TestServer. This exercises
/// <see cref="ClerkAuthenticationSetup.AddClerkAuthentication"/> directly against a hand-built
/// <see cref="IConfiguration"/>, because the failure under test (an empty/missing
/// Clerk:AuthorizedParties) must throw before the host, or any database connection, even exists.
/// </summary>
public sealed class ClerkAuthenticationSetupTests
{
    private static IConfiguration BuildConfiguration(string? authority, params string[] authorizedParties)
    {
        var values = new Dictionary<string, string?>();
        if (authority is not null)
        {
            values["Clerk:Authority"] = authority;
        }

        for (var i = 0; i < authorizedParties.Length; i++)
        {
            values[$"Clerk:AuthorizedParties:{i}"] = authorizedParties[i];
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void Missing_authority_fails_startup()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(authority: null, "http://localhost:3000");

        var act = () => services.AddClerkAuthentication(configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Clerk:Authority*");
    }

    [Fact]
    public void Missing_authorized_parties_fails_startup_rather_than_silently_trusting_every_origin()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration("https://test.clerk.accounts.dev");

        var act = () => services.AddClerkAuthentication(configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage("*AuthorizedParties*");
    }

    [Fact]
    public void An_authorized_parties_list_of_only_blank_entries_also_fails_startup()
    {
        // The configuration binder can hand back ["", "  "] rather than a genuinely empty
        // array — e.g. an environment variable set to an empty string rather than unset. This
        // must fail exactly the same way a truly empty list does, not bind to "no allow-list"
        // silently.
        var services = new ServiceCollection();
        var configuration = BuildConfiguration("https://test.clerk.accounts.dev", "", "   ");

        var act = () => services.AddClerkAuthentication(configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage("*AuthorizedParties*");
    }

    [Fact]
    public void A_non_empty_authorized_parties_list_succeeds()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration("https://test.clerk.accounts.dev", "http://localhost:3000");

        var act = () => services.AddClerkAuthentication(configuration);

        act.Should().NotThrow();
    }
}
