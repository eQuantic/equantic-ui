using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// A constructor of <c>DateTime</c> or <c>DateTimeOffset</c> lowers to the runtime's factory for its
/// SHAPE, read from the bound constructor's parameters and never from a count of arguments (#606): the
/// factories dispatched on the count, so a kind was read as the millisecond and a millisecond as the
/// offset. The conformance suite runs both sides; these pin the shape each overload takes.
/// </summary>
public class DateTimeConstructionTests
{
    [Theory]
    [InlineData("new DateTime(2026, 1, 2)", "$eq.time.dateTime.of(2026, 1, 2)")]
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5)", "$eq.time.dateTime.of(2026, 1, 2, 3, 4, 5)")]
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, 6, 7)", "$eq.time.dateTime.of(2026, 1, 2, 3, 4, 5, 6, 7)")]
    // A kind follows every component, a zero in the place of each one the constructor skips.
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)", "$eq.time.dateTime.of(2026, 1, 2, 3, 4, 5, 0, 0, 'utc')")]
    [InlineData("new DateTime(2026, 1, 2, 3, 4, 5, 6, DateTimeKind.Local)", "$eq.time.dateTime.of(2026, 1, 2, 3, 4, 5, 6, 0, 'local')")]
    [InlineData("new DateTime(639029198450060070L, DateTimeKind.Utc)", "$eq.time.dateTime.fromTicks(639029198450060070n, 'utc')")]
    [InlineData("new DateTime()", "$eq.time.dateTime.minValue()")]
    public void ADateTime_IsBuiltByItsConstructorsShape(string creation, string expected)
    {
        TestHelper.ConvertExpression(creation).Should().Be(expected);
    }

    [Theory]
    [InlineData("new DateTimeOffset(2026, 1, 2, 3, 4, 5, 6, TimeSpan.Zero)", "$eq.time.dateTimeOffset.of(2026, 1, 2, 3, 4, 5, 6, 0, ")]
    [InlineData("new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)", "$eq.time.dateTimeOffset.of(2026, 1, 2, 3, 4, 5, 0, 0, ")]
    [InlineData("new DateTimeOffset(new DateTime(2026, 1, 2))", "$eq.time.dateTimeOffset.fromDateTime($eq.time.dateTime.of(2026, 1, 2))")]
    [InlineData("new DateTimeOffset(639029198450060070L, TimeSpan.Zero)", "$eq.time.dateTimeOffset.fromTicks(639029198450060070n, ")]
    public void ADateTimeOffset_IsBuiltByItsConstructorsShape(string creation, string expected)
    {
        TestHelper.ConvertExpression(creation).Should().StartWith(expected);
    }

    /// <summary>A named argument takes its parameter's place, and the arguments still run in the
    /// order they are written: the template binds the holes, never the text.</summary>
    [Fact]
    public void NamedArguments_TakeTheirParametersPlaces()
    {
        TestHelper.ConvertExpression("new DateTime(day: 2, month: 1, year: 2026)")
            .Should().Be("$eq.time.dateTime.of(2026, 1, 2)");
    }

    /// <summary>The browser has none of .NET's calendars: an overload that takes one is a build error,
    /// where the factory read the calendar as a component.</summary>
    [Theory]
    [InlineData("new DateTime(2026, 1, 2, new System.Globalization.GregorianCalendar())")]
    [InlineData("new DateTimeOffset(2026, 1, 2, 3, 4, 5, 6, new System.Globalization.GregorianCalendar(), TimeSpan.Zero)")]
    public void AConstructorWithACalendar_IsABuildError(string creation)
    {
        TestHelper.DiagnosticsFor(creation).Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("Calendar"));
    }
}
