using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// <c>DateTimeOffset.Now</c>, <c>LocalDateTime</c> and <c>ToLocalTime</c>, and a <c>DateTime</c>'s own
/// conversions, answer as .NET does for the machine's time zone (#626, reported by the Falei.pt app):
/// <c>Now</c> carried offset zero with the local clock, so it named the wrong instant anywhere east or
/// west of UTC, <c>LocalDateTime</c> read the value's own clock, and <c>ToLocalTime</c> did not exist.
/// <para>
/// Both sides read the zone from <c>TZ</c>: .NET once its cached zone is cleared, and the engine from
/// the environment it inherits. Each case runs in a zone without daylight saving and a half-hour
/// offset (Asia/Kolkata), one west of UTC (America/Sao_Paulo), and one with daylight saving
/// (Europe/Lisbon), so a case that passed in UTC alone proves nothing. Windows takes its zone from the
/// system and not from <c>TZ</c>, so these cases run on Linux and macOS.
/// </para>
/// </summary>
[Collection(LocalTimeZoneCollection.Name)]
public class LocalTimeConformanceTests
{
    public static TheoryData<string, string> Cases()
    {
        string[] zones = ["Asia/Kolkata", "America/Sao_Paulo", "Europe/Lisbon"];
        string[] expressions =
        [
            // Now is the instant UtcNow is, at the zone's offset for it.
            "Math.Abs((DateTimeOffset.Now - DateTimeOffset.UtcNow).TotalSeconds) < 5",
            "Math.Abs((DateTimeOffset.Now.UtcDateTime - DateTime.UtcNow).TotalSeconds) < 5",
            "DateTimeOffset.Now.Offset == new DateTimeOffset(DateTime.Now).Offset",
            "Math.Abs((DateTime.Now.ToUniversalTime() - DateTime.UtcNow).TotalSeconds) < 5",
            // The local clock of an instant, in winter and in summer.
            "new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.FromHours(-3)).LocalDateTime.ToString()",
            "new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.FromHours(-3)).LocalDateTime.ToString()",
            "new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.FromHours(-3)).LocalDateTime.Kind",
            "new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.FromHours(-3)).ToLocalTime().ToString()",
            "new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero).ToLocalTime().Offset.ToString()",
            "new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero).ToLocalTime().Offset.ToString()",
            "new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero).ToLocalTime().UtcTicks",
            // A DateTime's conversions by its kind: Unspecified is taken as local on the way to UTC.
            "new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc).ToLocalTime().ToString()",
            "new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc).ToLocalTime().Kind",
            "new DateTime(2026, 7, 1, 12, 0, 0).ToUniversalTime().ToString()",
            "new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Local).ToUniversalTime().Kind",
            "new DateTime(2026, 1, 15, 12, 0, 0).ToLocalTime().ToString()",
            // A local value's JSON carries the zone's offset, and Parse moves a written zone to local time.
            "new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Local)",
            "DateTime.Parse(\"2026-07-01T12:00:00Z\").ToString()",
            "DateTime.Parse(\"2026-07-01T12:00:00Z\").Kind",
            "DateTime.Parse(\"2026-01-15T12:00:00-03:00\").ToString()",
            // A DateTimeOffset of a DateTime that is not UTC takes the zone's offset for it.
            "new DateTimeOffset(new DateTime(2026, 7, 1, 12, 0, 0)).Offset.ToString()",
            "new DateTimeOffset(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Local)).Offset.ToString()",
            // Lisbon's clocks repeat 01:00 to 01:59 on 2026-10-25: a local time made from an instant in
            // the first of them goes back to that instant, through arithmetic and its JSON, until
            // SpecifyKind drops what it knew. It went back to the standard occurrence, an hour off.
            "new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc).ToLocalTime().ToUniversalTime().ToString()",
            "new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc).ToLocalTime()",
            "new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc).ToLocalTime().AddMinutes(10).ToUniversalTime().ToString()",
            "DateTime.SpecifyKind(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc).ToLocalTime(), DateTimeKind.Local).ToUniversalTime().ToString()",
            "new DateTimeOffset(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc).ToLocalTime()).Offset.ToString()",
            "new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero).LocalDateTime.ToUniversalTime().ToString()",
            "DateTime.Parse(\"2026-10-25T00:30:00Z\").ToUniversalTime().ToString()",
            "new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc).ToLocalTime().ToString(\"o\")",
            // A date's text by its kind: o and K end a UTC time with Z and a local one with its offset,
            // z writes the zone's offset for anything not UTC, and U moves only what is not UTC.
            "new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc).ToString(\"o\")",
            "new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Local).ToString(\"o\")",
            "new DateTime(2026, 7, 1, 12, 0, 0).ToString(\"o\")",
            "new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc).ToString(\"yyyy-MM-ddTHH:mm:ssK\")",
            "new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Local).ToString(\"HH:mm zzz\")",
            "new DateTime(2026, 7, 1, 12, 0, 0).ToString(\"%z\")",
            "new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc).ToString(\"U\")",
            "new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Local).ToString(\"U\")",
        ];
        var data = new TheoryData<string, string>();
        foreach (var zone in zones)
        {
            foreach (var expression in expressions) data.Add(zone, expression);
        }
        return data;
    }

    [SkippableTheory]
    [MemberData(nameof(Cases))]
    public void LocalTime_MatchesDotNet(string zone, string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        Skip.If(OperatingSystem.IsWindows(), "Windows takes its time zone from the system, not from TZ.");
        var previous = Environment.GetEnvironmentVariable("TZ");
        Environment.SetEnvironmentVariable("TZ", zone);
        TimeZoneInfo.ClearCachedData();
        try
        {
            Assert.Equal(zone, TimeZoneInfo.Local.Id);
            ConformanceRunner.AssertSameAsDotNet(expression);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TZ", previous);
            TimeZoneInfo.ClearCachedData();
        }
    }
}
