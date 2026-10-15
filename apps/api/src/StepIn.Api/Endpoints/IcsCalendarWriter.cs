using System.Text;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Minimal RFC 5545 (iCalendar) writer for a single VEVENT. Hand-rolled rather than a library
/// dependency — the format needed here is small and the escaping rules are exact and easy to
/// get wrong silently, so they are tested explicitly (see IcsCalendarWriterTests).
/// </summary>
public static class IcsCalendarWriter
{
    /// <summary>Escapes a text value per RFC 5545 §3.3.11: backslash, comma, and semicolon are
    /// escaped, and newlines become the literal two-character sequence "\n". Order matters —
    /// backslash must be escaped first, or the escaping of the other characters would itself
    /// be re-escaped.</summary>
    public static string EscapeText(string value) => value
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n");

    private static string FormatUtc(DateTimeOffset value) => value.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");

    public static string WriteEvent(
        string uid, string title, string description, DateTimeOffset startsAt, DateTimeOffset? endsAt,
        string? location, DateTimeOffset stampedAt)
    {
        var builder = new StringBuilder();
        builder.Append("BEGIN:VCALENDAR\r\n");
        builder.Append("VERSION:2.0\r\n");
        builder.Append("PRODID:-//StepIn//AIDX Lab//EN\r\n");
        builder.Append("CALSCALE:GREGORIAN\r\n");
        builder.Append("BEGIN:VEVENT\r\n");
        builder.Append($"UID:{EscapeText(uid)}\r\n");
        builder.Append($"DTSTAMP:{FormatUtc(stampedAt)}\r\n");
        // Written in UTC (trailing "Z"), not a floating or VTIMEZONE-qualified time — this is
        // deliberately unambiguous across the reader's own timezone, at the cost of not
        // reflecting a specific venue's local civil time. AidxEvent itself stores timestamptz
        // (always UTC internally), so there is no local-timezone information to preserve anyway.
        builder.Append($"DTSTART:{FormatUtc(startsAt)}\r\n");
        if (endsAt is { } end)
        {
            builder.Append($"DTEND:{FormatUtc(end)}\r\n");
        }
        builder.Append($"SUMMARY:{EscapeText(title)}\r\n");
        builder.Append($"DESCRIPTION:{EscapeText(description)}\r\n");
        if (!string.IsNullOrWhiteSpace(location))
        {
            builder.Append($"LOCATION:{EscapeText(location)}\r\n");
        }
        builder.Append("END:VEVENT\r\n");
        builder.Append("END:VCALENDAR\r\n");
        return builder.ToString();
    }
}
