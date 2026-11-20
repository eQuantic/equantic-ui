using System.Globalization;
using eQuantic.UI.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Web;
using FluentAssertions;

namespace eQuantic.UI.Web.Tests;

/// <summary>
/// <see cref="MoneyInput"/>, <see cref="NumberInput"/> and <see cref="Currency"/> on the server's side (#1764):
/// how an amount is written and read in the culture, what is handed on, and what the field says. .NET is the
/// oracle for <see cref="Currency.Format"/> (its <c>C</c> with a copy of the culture's format whose symbol and
/// digits are the currency's), and the same C# runs in the browser, transpiled, against the fixture
/// <see cref="AmountInputsFixtureTests"/> writes from these drivers (amount-inputs.spec.ts).
/// </summary>
public class AmountInputsTests
{
    public static readonly string[] Cultures =
        ["", "en", "en-US", "pt-PT", "pt-BR", "de-DE", "de-AT", "de-CH", "nl-NL", "fr-FR", "ja-JP"];

    public static TheoryData<string> AllCultures()
    {
        var data = new TheoryData<string>();
        foreach (var culture in Cultures) data.Add(culture);
        return data;
    }

    // ---- Currency.Format, against .NET's own C ------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllCultures))]
    public void AnAmount_IsWrittenAsDotNetsCurrencyFormat_WithTheCurrencysSymbolAndDigits(string culture)
    {
        foreach (var currency in new[] { Currency.Euro, Currency.UsDollar, Currency.Yen, Currency.SwissFranc })
        {
            foreach (var amount in new long[] { 0, 5, 1250, 123450, -123450, 123456789012, -1 })
            {
                var asked = new CultureInfo(culture);
                var format = (NumberFormatInfo)asked.NumberFormat.Clone();
                format.CurrencySymbol = currency.Symbol;
                format.CurrencyDecimalDigits = currency.MinorDigits;
                var expected = currency.ToMajor(amount).ToString("C", format);

                AmountDriver.Under(culture, () => currency.Format(amount))
                    .Should().Be(expected, "{0} of {1} in '{2}'", amount, currency.Code, culture);
            }
        }
    }

    [Fact]
    public void TheBriefsExamples()
    {
        AmountDriver.Under("pt-PT", () => Currency.Euro.Format(1250)).Should().Be("12,50 €");
        AmountDriver.Under("en", () => Currency.Euro.Format(1250)).Should().Be("€12.50");
    }

    [Fact]
    public void ToMinor_RefusesMoreDigitsThanTheCurrencyHas_AndWhatALongCannotHold()
    {
        Currency.Euro.ToMinor(12.5m).Should().Be(1250);
        Currency.Euro.ToMinor(12.505m).Should().BeNull();
        Currency.Yen.ToMinor(12.5m).Should().BeNull();
        Currency.Euro.ToMinor(-0.01m).Should().Be(-1);
        Currency.Euro.ToMinor(decimal.MaxValue).Should().BeNull();
        Currency.Euro.ToMajor(1250).Should().Be(12.50m);
    }

    // ---- MoneyInput ----------------------------------------------------------------------------------

    [Fact]
    public void AtRest_TheAmountIsGrouped_WithTheSymbolOnTheCulturesSide()
    {
        var pt = AmountDriver.Money("pt-PT", 123450, Currency.Euro);
        pt.Observe().Should().BeEquivalentTo(new { Value = "1\u00A0234,50", Prefix = (string?)null, Suffix = "€", Name = "Price (€)" },
            options => options.ExcludingMissingMembers());

        var en = AmountDriver.Money("en", 123450, Currency.Euro);
        en.Observe().Should().BeEquivalentTo(new { Value = "1,234.50", Prefix = "€", Suffix = (string?)null },
            options => options.ExcludingMissingMembers());

        AmountDriver.Money("en", 1235, Currency.Yen).Observe().Value.Should().Be("1,235");
    }

    [Fact]
    public void Editing_ShowsThePlainAmount_AndLeavingGroupsItAgain()
    {
        var field = AmountDriver.Money("pt-PT", 123450, Currency.Euro);
        field.Focus(true);
        field.Observe().Value.Should().Be("1234,50");
        field.Focus(false);
        field.Observe().Value.Should().Be("1\u00A0234,50");
    }

    [Fact]
    public void TheEntry_IsOfTheDecimalKind()
    {
        AmountDriver.Money("en", null, Currency.Euro).Entry().Kind.Should().Be(TextEntryKind.Decimal);
        AmountDriver.Number("en", null, "days").Entry().Kind.Should().Be(TextEntryKind.Number);
    }

    public static TheoryData<string, string, long?> Readings => new()
    {
        // The device's separator, which a phone's decimal keypad shows whatever the page's culture.
        { "pt-PT", "12.5", 1250 },
        { "en", "12,5", 1250 },
        { "pt-PT", "12,5", 1250 },
        { "en", "12.50", 1250 },
        // A thousand in English, never 1.234 euros; en's 12,50 is twelve and a half, never 1250.
        { "en", "1,234", 123400 },
        { "en", "1,234.50", 123450 },
        { "pt-BR", "1.234,50", 123450 },
        { "pt-BR", "1.234", 123400 },
        { "de-DE", "1.234,5", 123450 },
        { "en", "-3.5", -350 },
        { "en", "  7  ", 700 },
    };

    [Theory]
    [MemberData(nameof(Readings))]
    public void WhatIsTyped_IsReadInTheCulture_AndHandedOn(string culture, string typed, long? amount)
    {
        var field = AmountDriver.Money(culture, null, Currency.Euro);
        field.Type(typed);
        field.Handed.Should().Equal([amount]);
        field.Observe().Invalid.Should().BeFalse();
    }

    [Theory]
    [InlineData("pt-PT", "1,234")]   // three fraction digits of a euro: no amount
    [InlineData("en", "12.505")]
    [InlineData("en", "abc")]
    [InlineData("en", "1.2.3")]
    public void TextThatIsNoAmount_IsNotHandedOn_AndTheFieldSaysWhatItWants(string culture, string typed)
    {
        var field = AmountDriver.Money(culture, 500, Currency.Euro);
        field.Type(typed);
        field.Handed.Should().BeEmpty();
        var seen = field.Observe();
        seen.Invalid.Should().BeTrue();
        seen.Caption.Should().Be(AmountDriver.Under(culture, () => SdkStrings.AmountExpected(culture == "en" ? "12.50" : "12,50")));
        // Leaving keeps the text, for the reader to correct.
        field.Focus(false);
        field.Observe().Value.Should().Be(typed);
    }

    [Fact]
    public void AnAmountOutsideTheRange_IsNotHandedOn_AndTheFieldNamesTheRange()
    {
        var least = AmountDriver.Money("pt-PT", null, Currency.Euro, min: 1000);
        least.Type("5");
        least.Handed.Should().BeEmpty();
        least.Observe().Caption.Should().Be("Enter at least 10,00 €");

        var both = AmountDriver.Money("en", null, Currency.Euro, min: 0, max: 10000);
        both.Type("200");
        both.Observe().Caption.Should().Be("Enter a value from €0.00 to €100.00");
        both.Type("99.99");
        both.Handed.Should().Equal([9999L]);
        both.Observe().Invalid.Should().BeFalse();
    }

    [Fact]
    public void EmptyingTheField_HandsOnNull_AndIsNoError()
    {
        var field = AmountDriver.Money("en", 1250, Currency.Euro);
        field.Type("");
        field.Handed.Should().Equal([(long?)null]);
        field.Observe().Invalid.Should().BeFalse();
    }

    [Fact]
    public void TheAppsError_IsShownInsteadOfTheFields()
    {
        var field = AmountDriver.Money("en", null, Currency.Euro, error: "Required");
        field.Type("abc");
        field.Observe().Caption.Should().Be("Required");
    }

    /// <summary>The buffer outlives a parent that hands back what was typed or leaves the amount where it
    /// was, and goes when the app moves the amount somewhere the buffer does not say (a form reset).</summary>
    [Fact]
    public void WhatIsTyped_OutlivesARebuild_UntilTheAppMovesTheAmount()
    {
        var field = AmountDriver.Money("pt-PT", null, Currency.Euro);
        field.Type("12,5");
        field.Adopt(1250);
        field.Observe().Value.Should().Be("12,5", "the parent handed back what was typed: no reformatting under the caret");

        field.Type("12,5x");
        field.Adopt(1250);
        field.Observe().Value.Should().Be("12,5x", "the amount did not move, so the half-typed text stays");

        field.Adopt(null);
        field.Observe().Value.Should().Be("", "the app reset the amount");
    }

    [Fact]
    public void ADisabledField_TakesNoTyping()
    {
        var field = AmountDriver.Money("en", 1250, Currency.Euro, disabled: true);
        field.Render().Attributes.Should().NotBeNull();
        var input = AmountDriver.Walk(field.Render()).Single(n => n.Tag == "input");
        input.Attributes.Should().ContainKey("disabled");
    }

    // ---- NumberInput ---------------------------------------------------------------------------------

    [Fact]
    public void ANumber_CarriesItsUnit_AndIsHandedOnInsideItsRange()
    {
        var days = AmountDriver.Number("pt-PT", 3, "dias", min: 1, max: 30);
        days.Observe().Should().BeEquivalentTo(new { Value = "3", Suffix = "dias", Name = "Price (dias)" },
            options => options.ExcludingMissingMembers());

        days.Type("45");
        days.Handed.Should().BeEmpty();
        days.Observe().Caption.Should().Be("Enter a value from 1 dias to 30 dias");

        days.Type("12");
        days.Handed.Should().Equal([12]);

        days.Type("2,5");
        days.Observe().Caption.Should().Be("Enter a whole number, such as 12");

        days.Type("");
        days.Handed.Should().Equal([12, (int?)null]);
    }

    [Fact]
    public void ALargeNumber_IsGroupedAtRest()
    {
        var grams = AmountDriver.Number("en", 12500, "g");
        grams.Observe().Value.Should().Be("12,500");
        grams.Focus(true);
        grams.Observe().Value.Should().Be("12500");
        grams.Type("1500");
        grams.Handed.Should().Equal([1500]);
        // No group separator is read: English's 2,5 would be twenty-five.
        grams.Type("2,5");
        grams.Handed.Should().Equal([1500]);
        grams.Observe().Invalid.Should().BeTrue();
    }
}
