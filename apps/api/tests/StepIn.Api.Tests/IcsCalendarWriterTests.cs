using FluentAssertions;
using StepIn.Api.Endpoints;

namespace StepIn.Api.Tests;

/// <summary>Pure unit tests — no database. RFC 5545 §3.3.11 TEXT escaping.</summary>
public sealed class IcsCalendarWriterTests
{
    [Fact]
    public void Commas_semicolons_and_backslashes_are_escaped()
    {
        IcsCalendarWriter.EscapeText("A, B; C\\D").Should().Be("A\\, B\\; C\\\\D");
    }

    [Fact]
    public void Newlines_become_the_literal_backslash_n_sequence()
    {
        IcsCalendarWriter.EscapeText("Line one\nLine two").Should().Be("Line one\\nLine two");
        IcsCalendarWriter.EscapeText("Line one\r\nLine two").Should().Be("Line one\\nLine two");
    }

    [Fact]
    public void Backslash_is_escaped_before_the_other_characters_so_escaping_is_not_itself_re_escaped()
    {
        // If ';' were escaped before '\\', the result would wrongly become "\\\\;" (four
        // backslashes) instead of the correct "\\\\\\;" — order matters.
        IcsCalendarWriter.EscapeText("\\;").Should().Be("\\\\\\;");
    }

    [Fact]
    public void WriteEvent_produces_a_well_formed_VEVENT_with_UTC_timestamps()
    {
        var starts = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.FromHours(10)); // UTC+10
        var ends = starts.AddHours(2);
        var stamped = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

        var ics = IcsCalendarWriter.WriteEvent(
            "uid-1", "Title, with a comma", "Line one\nLine two", starts, ends, "Room A; Building B", stamped);

        ics.Should().Contain("BEGIN:VCALENDAR").And.Contain("BEGIN:VEVENT").And.Contain("END:VEVENT").And.Contain("END:VCALENDAR");
        ics.Should().Contain("SUMMARY:Title\\, with a comma");
        ics.Should().Contain("DESCRIPTION:Line one\\nLine two");
        ics.Should().Contain("LOCATION:Room A\\; Building B");
        // 09:00 at UTC+10 is 23:00 UTC the previous day.
        ics.Should().Contain("DTSTART:20260531T230000Z");
        ics.Should().Contain("DTEND:20260601T010000Z");
        ics.Should().Contain("DTSTAMP:20260501T000000Z");
    }

    [Fact]
    public void WriteEvent_omits_DTEND_when_no_end_time_is_given()
    {
        var ics = IcsCalendarWriter.WriteEvent("uid-2", "T", "D", DateTimeOffset.UtcNow, null, null, DateTimeOffset.UtcNow);

        ics.Should().NotContain("DTEND");
        ics.Should().NotContain("LOCATION");
    }
}
