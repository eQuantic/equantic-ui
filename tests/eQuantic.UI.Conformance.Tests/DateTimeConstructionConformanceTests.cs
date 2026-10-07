using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Every constructor of <c>DateTime</c> and <c>DateTimeOffset</c> the compiler lets through builds the
/// value .NET builds (#606). The runtime's factories took their components by how many arguments they
/// got, so every overload past the second read its arguments in the wrong places: a kind was read as
/// the millisecond, a millisecond as the offset, and the microsecond was dropped. A <c>DateTime</c>
/// carries its <c>Kind</c> now, as .NET's does: equality, ordering and the hash read the ticks alone, and
/// arithmetic keeps the kind. None of these cases reads the machine's time zone: the local ones are
/// <see cref="LocalTimeConformanceTests"/>.
/// </summary>
public class DateTimeConstructionConformanceTests
{
    [SkippableTheory]
    // The components with a kind: the kind was read as the millisecond, and bun exited.
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).Second")]                          // 5
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).Kind")]                            // Utc
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Local).Kind.ToString()")]               // "Local"
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, 6, DateTimeKind.Utc).Millisecond")]                  // 6
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, 6, DateTimeKind.Utc).Kind")]                         // Utc
    // The microsecond, which was dropped, and the members that read it.
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, 6, 7).Ticks")]                                       // 639029198450060070
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, 6, 7).Microsecond")]                                 // 7
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, 6, 7).Millisecond")]                                 // 6
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, 6, 7, DateTimeKind.Utc).Kind")]                      // Utc
    [InlineData("new DateTime(639029198450060071L).Nanosecond")]                                        // 100
    [InlineData("new DateTime(639029198450060071L).Microsecond")]                                       // 7
    // The ticks with a kind, and a date with a time.
    [InlineData("new DateTime(639029198450060070L, DateTimeKind.Utc).Kind")]                            // Utc
    [InlineData("new DateTime(new DateOnly(2026, 1, 2), new TimeOnly(3, 4, 5)).Ticks")]                 // 639029198450000000
    [InlineData("new DateTime(new DateOnly(2026, 1, 2), new TimeOnly(3, 4, 5), DateTimeKind.Local).Kind")] // Local
    // Arguments named out of order land on their parameters.
    [InlineData("new DateTime(day: 2, month: 1, year: 2026).Ticks")]                                    // 639029088000000000
    [InlineData("new DateTime(kind: DateTimeKind.Utc, ticks: 639029198450060070L).Kind")]               // Utc
    // A kind the value is born with, and one it keeps.
    [InlineData("new DateTime(2026, 1, 2).Kind")]                                                       // Unspecified
    [InlineData("default(DateTime).Kind")]                                                              // Unspecified
    [InlineData("DateTime.MinValue.Kind")]                                                              // Unspecified
    [InlineData("DateTime.UtcNow.Kind")]                                                                // Utc
    [InlineData("DateTime.Now.Kind")]                                                                   // Local
    [InlineData("DateTime.Today.Kind")]                                                                 // Local
    [InlineData("DateTime.SpecifyKind(new DateTime(2026, 1, 2), DateTimeKind.Utc).Kind")]               // Utc
    [InlineData("new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc).AddDays(1).Kind")]                 // Utc
    [InlineData("new DateTime(2026, 1, 2, 3, 0, 0, DateTimeKind.Utc).Date.Kind")]                       // Utc
    [InlineData("(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Local) + TimeSpan.FromHours(1)).Kind")] // Local
    [InlineData("new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc).AddMonths(1).Kind")]              // Utc
    // The kind takes no part in equality, ordering or the hash.
    [InlineData("new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc) == new DateTime(2026, 1, 2)")]     // true
    [InlineData("new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc).Equals(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Local))")] // true
    [InlineData("new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc).CompareTo(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Local))")] // 0
    [InlineData("new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc).GetHashCode() == new DateTime(2026, 1, 2).GetHashCode()")] // true
    // A kind that stays where it is converts to nothing.
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).ToUniversalTime().Ticks")]         // 639029198450000000
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Local).ToLocalTime().Ticks")]           // 639029198450000000
    // The DateTimeOffset constructors past the second: the millisecond was read as the offset.
    [InlineData("new DateTimeOffset(2026, 1, 2, 3, 4, 5, 6, TimeSpan.Zero).Millisecond")]               // 6
    [InlineData("new DateTimeOffset(2026, 1, 2, 3, 4, 5, 6, 7, TimeSpan.Zero).Microsecond")]            // 7
    [InlineData("new DateTimeOffset(2026, 1, 2, 3, 4, 5, 6, 7, TimeSpan.FromHours(2)).UtcTicks")]       // 639029126450060070
    [InlineData("new DateTimeOffset(2026, 1, 2, 3, 4, 5, 6, 7, TimeSpan.Zero).Nanosecond")]             // 0
    [InlineData("new DateTimeOffset(new DateOnly(2026, 1, 2), new TimeOnly(3, 4, 5), TimeSpan.FromHours(-3)).ToString()")] // "01/02/2026 03:04:05 -03:00"
    [InlineData("new DateTimeOffset(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)).Offset.ToString()")] // "00:00:00"
    [InlineData("new DateTimeOffset(new DateTime(2026, 1, 2, 3, 4, 5, 6, 7), TimeSpan.FromHours(1)).Microsecond")] // 7
    [InlineData("new DateTimeOffset(offset: TimeSpan.FromHours(1), year: 2026, month: 1, day: 2, hour: 3, minute: 4, second: 5).ToString()")] // "01/02/2026 03:04:05 +01:00"
    [InlineData("new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)).UtcDateTime.Kind")]     // Utc
    [InlineData("new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)).DateTime.Kind")]        // Unspecified
    public void Construction_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }

    /// <summary>
    /// A constructor refuses what .NET's refuses, in .NET's words: a component out of its range, a kind
    /// that is no <c>DateTimeKind</c>, ticks off the calendar, and an offset that is not whole minutes, is
    /// past fourteen hours, or is not zero for a UTC <c>DateTime</c>. The runtime built a value from
    /// any of them.
    /// </summary>
    [SkippableTheory]
    [InlineData("new DateTime(2026, 13, 1)")]                                                            // "Year, Month, and Day parameters describe an un-representable DateTime."
    [InlineData("new DateTime(2026, 2, 29)")]                                                            // the same: 2026 is no leap year
    [InlineData("new DateTime(2026, 1, 2, 24, 0, 0)")]                                                   // "Hour, Minute, and Second parameters describe an un-representable DateTime."
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, 1000)")]                                              // "Valid values are between 0 and 999, inclusive. (Parameter 'millisecond')"
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, 6, 1000)")]                                           // "… (Parameter 'microsecond')"
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, (DateTimeKind)7)")]                                   // "Invalid DateTimeKind value. (Parameter 'kind')"
    [InlineData("new DateTime(-1L)")]                                                                    // "Ticks must be between DateTime.MinValue.Ticks and DateTime.MaxValue.Ticks. (Parameter 'ticks')"
    [InlineData("new DateTime(3155378976000000000L, DateTimeKind.Utc)")]                                 // the same
    [InlineData("new DateTimeOffset(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), TimeSpan.FromHours(1))")] // "The UTC Offset for Utc DateTime instances must be 0. (Parameter 'offset')"
    [InlineData("new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromMinutes(1.5))")]                   // "Offset must be specified in whole minutes. (Parameter 'offset')"
    [InlineData("new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(15))")]                      // "Offset must be within plus or minus 14 hours. (Parameter 'offset')"
    [InlineData("new DateTimeOffset(2026, 1, 2, 3, 4, 5, 1000, TimeSpan.Zero)")]                         // "… (Parameter 'millisecond')"
    public void Construction_RefusesAsDotNet(string construction)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            $"try {{ var value = {construction}; return \"built\"; }} "
            + "catch (ArgumentOutOfRangeException e) { return \"out of range: \" + e.Message; } "
            + "catch (ArgumentException e) { return \"argument: \" + e.Message; }");
    }

    /// <summary>
    /// Arguments are evaluated in the order they are written, as C# evaluates a constructor's, wherever
    /// a name puts them: the factory takes them by parameter, the call keeps the written order.
    /// </summary>
    [SkippableFact]
    public void NamedArguments_AreEvaluatedInTheOrderWritten()
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            "var log = \"\"; Func<int, int> f = v => { log += v + \",\"; return v; }; " +
            "var d = new DateTime(day: f(2), month: f(1), year: f(2026)); return log + d.ToString();"); // "2,1,2026,01/02/2026 00:00:00"
    }
}
