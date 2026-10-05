using FluentAssertions;
using StepIn.Domain.Aidx;

namespace StepIn.Api.Tests;

/// <summary>Pure slug rules. No database or container, so these run without Docker.</summary>
public sealed class AidxSlugTests
{
    [Theory]
    [InlineData("Machine Learning for Coral Reefs", "machine-learning-for-coral-reefs")]
    [InlineData("  Spaces   and -- dashes  ", "spaces-and-dashes")]
    [InlineData("Ünïcödé & symbols!", "n-c-d-symbols")]
    [InlineData("2026 Lab Report", "2026-lab-report")]
    public void FromText_produces_lowercase_hyphenated_ascii(string input, string expected)
    {
        AidxSlug.FromText(input).Should().Be(expected);
    }

    [Fact]
    public void FromText_with_no_letters_or_digits_returns_empty_so_validation_can_reject_it()
    {
        AidxSlug.FromText("!!! ???").Should().BeEmpty();
    }

    [Fact]
    public void FromText_truncates_to_the_maximum_length_without_a_trailing_hyphen()
    {
        var slug = AidxSlug.FromText(string.Join(" ", Enumerable.Repeat("word", 100)));

        slug.Length.Should().BeLessThanOrEqualTo(AidxSlug.MaxLength);
        slug.Should().NotEndWith("-");
        AidxSlug.IsValid(slug).Should().BeTrue();
    }

    [Theory]
    [InlineData("valid-slug-1")]
    [InlineData("a")]
    [InlineData("ai-and-ml")]
    public void IsValid_accepts_lowercase_hyphenated_slugs(string slug)
    {
        AidxSlug.IsValid(slug).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Upper-Case")]
    [InlineData("double--hyphen")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("has space")]
    [InlineData("slash/path")]
    public void IsValid_rejects_anything_that_is_not_url_safe(string? slug)
    {
        AidxSlug.IsValid(slug).Should().BeFalse();
    }
}
