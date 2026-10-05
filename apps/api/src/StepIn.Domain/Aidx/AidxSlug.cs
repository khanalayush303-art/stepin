using System.Text;
using System.Text.RegularExpressions;

namespace StepIn.Domain.Aidx;

/// <summary>
/// URL slugs for AIDX content. Slugs are lowercase ASCII words joined by single hyphens,
/// so they are safe in public URLs without encoding.
/// </summary>
public static partial class AidxSlug
{
    public const int MaxLength = 200;

    public static string FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder();
        var lastWasHyphen = true;

        foreach (var c in text.Trim().ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(c);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length <= MaxLength ? slug : slug[..MaxLength].TrimEnd('-');
    }

    public static bool IsValid(string? slug) =>
        !string.IsNullOrEmpty(slug) && slug.Length <= MaxLength && SlugPattern().IsMatch(slug);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
