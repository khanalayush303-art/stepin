using Microsoft.EntityFrameworkCore;

namespace StepIn.Api.Endpoints;

/// <summary>Small helpers shared by the AIDX public and admin endpoint groups.</summary>
internal static class AidxEndpointHelpers
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 50;

    public static Dictionary<string, string[]> ValidatePaging(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
        {
            AddError(errors, "page", "Page must be 1 or greater.");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            AddError(errors, "pageSize", $"Page size must be between 1 and {MaxPageSize}.");
        }

        return errors;
    }

    public static async Task<(List<T> Items, int TotalCount)> PageAsync<T>(
        IQueryable<T> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public static bool TryParseEnum<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed))
        {
            return true;
        }

        parsed = default;
        return false;
    }

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static void AddError(Dictionary<string, string[]> errors, string key, string message) =>
        errors[key] = [message];
}
