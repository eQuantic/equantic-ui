using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for .NET <c>DateTime</c> — the tick-precise compat type. JS <c>Date</c> formats and
/// computes nothing like .NET; these cases prove construction, components, Add* arithmetic, subtraction
/// (into a TimeSpan), and comparison match .NET exactly. Fixed values only (DateTime.Now is
/// non-deterministic). Results are compared via ToString()/numeric property/bool.
/// </summary>
public class DateTimeConformanceTests
{
    [SkippableTheory]
    // Construction + default invariant ToString (MM/dd/yyyy HH:mm:ss)
    [InlineData("new DateTime(2024, 1, 15).ToString()")]              // "01/15/2024 00:00:00"
    [InlineData("new DateTime(2024, 1, 5, 9, 3, 7).ToString()")]      // "01/05/2024 09:03:07"
    // Add* arithmetic
    [InlineData("new DateTime(2024, 1, 15).AddDays(20).ToString()")]  // "02/04/2024 00:00:00"
    [InlineData("new DateTime(2024, 1, 15).AddHours(25).ToString()")] // "01/16/2024 01:00:00"
    [InlineData("new DateTime(2024, 1, 31).AddMonths(1).ToString()")] // "02/29/2024 00:00:00" (clamp, leap)
    [InlineData("new DateTime(2023, 1, 31).AddMonths(1).ToString()")] // "02/28/2023 00:00:00" (clamp)
    [InlineData("new DateTime(2024, 1, 15).AddYears(1).ToString()")]  // "01/15/2025 00:00:00"
    // Calendar component properties (int)
    [InlineData("new DateTime(2024, 2, 29).Year")]                    // 2024
    [InlineData("new DateTime(2024, 3, 15).Month")]                   // 3
    [InlineData("new DateTime(2024, 3, 15).Day")]                     // 15
    [InlineData("new DateTime(2024, 1, 5, 9, 3, 7).Hour")]            // 9
    [InlineData("new DateTime(2024, 3, 1).DayOfYear")]                // 61 (leap: 31 + 29 + 1)
    // Subtraction -> TimeSpan
    [InlineData("(new DateTime(2024, 1, 20) - new DateTime(2024, 1, 15)).ToString()")] // "5.00:00:00"
    // Static helpers
    [InlineData("DateTime.DaysInMonth(2024, 2)")]                     // 29
    [InlineData("DateTime.IsLeapYear(2023)")]                         // false
    // Comparison (bool)
    [InlineData("new DateTime(2024, 1, 15) < new DateTime(2024, 1, 16)")]   // true
    [InlineData("new DateTime(2024, 1, 15) == new DateTime(2024, 1, 15)")]  // true
    [InlineData("new DateTime(2024, 1, 16) > new DateTime(2024, 1, 15)")]   // true
    public void DateTime_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }

    /// <summary>
    /// A fractional <c>Add*</c> lands on the tick .NET 7 and later land on: the whole units and the
    /// fraction become ticks apart and the fraction truncates toward zero, as <c>DateTime.AddUnits</c>
    /// does. A count past what a date can move by, and a result outside the calendar, throw in .NET's
    /// words. Ticks are compared as text: a long is a bigint on the web (#422).
    /// </summary>
    [SkippableTheory]
    [InlineData("var d = new DateTime(2026, 1, 1); return (d.AddSeconds(0.00001).Ticks - d.Ticks).ToString();")]                                   // "100", where rounding to the millisecond gave 0
    [InlineData("var d = new DateTime(2026, 1, 1); return (d.AddSeconds(-0.00001).Ticks - d.Ticks).ToString();")]                                  // "-100"
    [InlineData("var d = new DateTime(2026, 1, 1); return (d.AddMilliseconds(0.5).Ticks - d.Ticks).ToString();")]                                  // "5000", where a whole millisecond gave 10000
    [InlineData("var d = new DateTime(2026, 1, 1); return (d.AddMilliseconds(-0.5).Ticks - d.Ticks).ToString();")]                                 // "-5000"
    [InlineData("var d = new DateTime(2026, 1, 1); return (d.AddDays(1.23456789).Ticks - d.Ticks).ToString();")]                                   // "1066666656959": the fraction truncates
    [InlineData("var d = new DateTime(2026, 1, 1); return (d.AddHours(-1.0000001).Ticks - d.Ticks).ToString();")]                                  // "-36000003600"
    [InlineData("var d = new DateTime(2026, 1, 1); return (d.AddMinutes(0.123456789).Ticks - d.Ticks).ToString();")]                               // "74074073"
    [InlineData("var d = new DateTime(2026, 1, 1); return (d.AddMicroseconds(0.25).Ticks - d.Ticks).ToString();")]                                 // "2"
    [InlineData("var d = new DateTime(2026, 1, 1); return (d.AddMicroseconds(1.99).Ticks - d.Ticks).ToString();")]                                 // "19"
    [InlineData("var d = new DateTime(2026, 1, 1); return d.AddDays(2.5).ToString();")]                                                            // "01/03/2026 12:00:00"
    [InlineData("var d = new DateTime(2026, 1, 1); return d.AddDays(double.NaN) == d;")]                                                           // true: NaN adds nothing
    [InlineData("try { new DateTime(2026, 1, 1).AddDays(1e10); return \"no\"; } catch (Exception e) { return e.Message; }")]                       // "Value to add was out of range. (Parameter 'value')"
    [InlineData("try { new DateTime(2026, 1, 1).AddSeconds(double.PositiveInfinity); return \"no\"; } catch (Exception e) { return e.Message; }")] // "Value to add was out of range. (Parameter 'value')"
    [InlineData("try { new DateTime(2026, 1, 1).AddMilliseconds(-1e15); return \"no\"; } catch (Exception e) { return e.Message; }")]              // "Value to add was out of range. (Parameter 'value')"
    [InlineData("try { new DateTime(2026, 1, 1).AddDays(3652058.0); return \"no\"; } catch (Exception e) { return e.Message; }")]                  // "The added or subtracted value results in an un-representable DateTime. (Parameter 'value')": a count in range, a result past it
    [InlineData("try { DateTime.MaxValue.AddSeconds(1); return \"no\"; } catch (Exception e) { return e.Message; }")]                              // "The added or subtracted value results in an un-representable DateTime. (Parameter 'value')"
    [InlineData("try { DateTime.MinValue.AddMilliseconds(-0.0001); return \"no\"; } catch (Exception e) { return e.Message; }")]                   // "The added or subtracted value results in an un-representable DateTime. (Parameter 'value')"
    [InlineData("try { DateTime.MaxValue.AddTicks(1); return \"no\"; } catch (Exception e) { return e.Message; }")]                                // "The added or subtracted value results in an un-representable DateTime. (Parameter 'value')"
    [InlineData("return DateTime.MinValue.AddMilliseconds(-0.00001) == DateTime.MinValue;")]                                                       // true: the fraction truncates to no tick
    // At each unit's limit the count is checked against the whole units, as a double, as .NET does.
    [InlineData("try { return DateTime.MinValue.AddDays(3652058.0).Ticks.ToString(); } catch (Exception e) { return e.Message; }")] // "3155378112000000000": the most whole days
    [InlineData("try { return DateTime.MinValue.AddDays(3652058.5).Ticks.ToString(); } catch (Exception e) { return e.Message; }")] // "Value to add was out of range.": refused by the count, although the date would fit
    [InlineData("try { return DateTime.MaxValue.AddDays(-3652058.5).Ticks.ToString(); } catch (Exception e) { return e.Message; }")] // "Value to add was out of range."
    [InlineData("try { return DateTime.MinValue.AddMilliseconds(315537897599999.5).Ticks.ToString(); } catch (Exception e) { return e.Message; }")] // "Value to add was out of range."
    [InlineData("try { return DateTime.MinValue.AddMicroseconds(315537897599999999.0).Ticks.ToString(); } catch (Exception e) { return e.Message; }")] // "...un-representable DateTime.": the limit is a double too, so the count passes
    public void DateTimeAdd_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>
    /// Every arithmetic that leaves the calendar is refused in .NET's words, naming the parameter of
    /// the member that refuses it: <c>value</c> for <c>Add</c>, <c>Subtract</c> and <c>AddYears</c>,
    /// <c>months</c> for <c>AddMonths</c>, and <c>t</c> for the operators. The twin built the invalid
    /// date without a word (#424).
    /// </summary>
    [SkippableTheory]
    [InlineData("try { DateTime.MaxValue.Add(TimeSpan.FromTicks(1)); return \"no\"; } catch (Exception e) { return e.Message; }")]      // "...un-representable DateTime. (Parameter 'value')"
    [InlineData("try { DateTime.MinValue.Subtract(TimeSpan.FromTicks(1)); return \"no\"; } catch (Exception e) { return e.Message; }")] // "...(Parameter 'value')"
    [InlineData("try { var d = DateTime.MaxValue + TimeSpan.FromTicks(1); return \"no\"; } catch (Exception e) { return e.Message; }")] // "...(Parameter 't')"
    [InlineData("try { var d = DateTime.MinValue; d -= TimeSpan.FromTicks(1); return \"no\"; } catch (Exception e) { return e.Message; }")] // "...(Parameter 't')"
    [InlineData("try { DateTime.MaxValue.AddMonths(1); return \"no\"; } catch (Exception e) { return e.Message; }")]                     // "...(Parameter 'months')"
    [InlineData("try { DateTime.MaxValue.AddYears(1); return \"no\"; } catch (Exception e) { return e.Message; }")]                      // "...(Parameter 'value')"
    [InlineData("try { new DateTime(2026, 1, 1).AddMonths(120001); return \"no\"; } catch (Exception e) { return e.Message; }")]        // "Months value must be between +/-120000. (Parameter 'months')"
    [InlineData("try { new DateTime(2026, 1, 1).AddYears(-10001); return \"no\"; } catch (Exception e) { return e.Message; }")]         // "Years value must be between +/-10000. (Parameter 'value')"
    [InlineData("try { var o = DateTimeOffset.MaxValue + TimeSpan.FromTicks(1); return \"no\"; } catch (Exception e) { return e.Message; }")] // "...(Parameter 't')"
    [InlineData("try { DateTimeOffset.MaxValue.Add(TimeSpan.FromTicks(1)); return \"no\"; } catch (Exception e) { return e.Message; }")] // "...(Parameter 'value')"
    [InlineData("try { DateTimeOffset.MaxValue.AddYears(1); return \"no\"; } catch (Exception e) { return e.Message; }")]              // "...(Parameter 'value')": the offset's years are its clock's
    [InlineData("try { new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddYears(10001); return \"no\"; } catch (Exception e) { return e.Message; }")] // "Years value must be between +/-10000. (Parameter 'value')"
    [InlineData("try { DateTimeOffset.MaxValue.AddMonths(1); return \"no\"; } catch (Exception e) { return e.Message; }")]             // "...(Parameter 'months')"
    [InlineData("try { new DateTimeOffset(9998, 12, 31, 23, 0, 0, TimeSpan.FromHours(-2)).AddYears(1); return \"no\"; } catch (Exception e) { return e.Message; }")] // "The UTC time represented when the offset is applied must be between year 0 and 10,000. (Parameter 'offset')"
    [InlineData("try { DateOnly.MaxValue.AddDays(1); return \"no\"; } catch (Exception e) { return e.Message; }")]                     // "Value to add was out of range. (Parameter 'value')"
    [InlineData("try { DateOnly.MaxValue.AddMonths(1); return \"no\"; } catch (Exception e) { return e.Message; }")]                   // "...(Parameter 'months')"
    [InlineData("try { DateOnly.MinValue.AddYears(-1); return \"no\"; } catch (Exception e) { return e.Message; }")]                   // "...(Parameter 'value')"
    [InlineData("try { new DateOnly(2026, 1, 1).AddMonths(-120001); return \"no\"; } catch (Exception e) { return e.Message; }")]      // "Months value must be between +/-120000. (Parameter 'months')"
    [InlineData("return new DateOnly(2024, 2, 29).AddYears(1) + \",\" + new DateOnly(2026, 1, 31).AddMonths(1);")]                       // "02/28/2025,02/28/2026"
    [InlineData("return new DateTime(2026, 1, 31).AddMonths(1).Day + \",\" + new DateTime(2024, 2, 29).AddYears(1).Day;")]             // "28,28"
    [InlineData("DateTime? d = null; d += TimeSpan.FromDays(1); DateTime? e = new DateTime(2026, 1, 1); e += TimeSpan.FromDays(1); return (d == null) + \",\" + e.Value.Day;")] // "True,2": a nullable is lifted
    public void DateArithmetic_LeavingTheCalendar_IsRefusedAsDotNetRefusesIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>A date's text is read as .NET reads it: a date or a clock that does not exist is
    /// refused, each in its own words, where it normalized into the next day or month, and the
    /// invariant form reads nothing after it (found by review, #606).</summary>
    [SkippableTheory]
    [InlineData("return DateTime.Parse(\"2028-02-29\").ToString(\"o\");")]                                                        // "2028-02-29T00:00:00.0000000": a leap day
    [InlineData("try { DateTime.Parse(\"2026-02-29\"); return \"no\"; } catch (FormatException e) { return e.Message; }")]          // "String '2026-02-29' was not recognized as a valid DateTime."
    [InlineData("try { DateTime.Parse(\"2026-04-31\"); return \"no\"; } catch (FormatException e) { return e.Message; }")]          // the same
    [InlineData("try { DateTime.Parse(\"2026-13-01\"); return \"no\"; } catch (FormatException e) { return e.Message; }")]          // the same
    [InlineData("try { DateTime.Parse(\"0000-01-01\"); return \"no\"; } catch (FormatException e) { return e.Message; }")]          // the same
    [InlineData("try { DateTime.Parse(\"2026-01-01T24:00:00\"); return \"no\"; } catch (FormatException e) { return e.Message; }")] // "The DateTime represented by the string … is not supported in calendar 'System.Globalization.GregorianCalendar'."
    [InlineData("try { DateTime.Parse(\"2026-01-01T23:60:00\"); return \"no\"; } catch (FormatException e) { return e.Message; }")] // the same
    [InlineData("try { DateTime.Parse(\"2026-01-15T09:30:00 junk\"); return \"no\"; } catch (FormatException e) { return e.Message; }")] // "String '…' was not recognized as a valid DateTime."
    public void ADatesText_IsReadAsDotNetReadsIt(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
