using eQuantic.UI.Compiler.CodeGen;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// A string's own overloads that compare reach the runtime's searches, chosen by the BOUND method
/// and handed the comparison as the value it is (#528). They were read from the comparison's
/// spelling and both sides were lower-cased; the conformance suite proves the answers on both
/// sides, and these pin the shape of each call and every refusal, which the harness cannot see.
/// </summary>
public class StringComparisonOverloadTests
{
    [Theory]
    [InlineData("var r = a.StartsWith(b, StringComparison.OrdinalIgnoreCase)", "$eq.text.startsWith(this.a, this.b, 'ordinalIgnoreCase')")]
    [InlineData("var r = a.EndsWith(b, StringComparison.Ordinal)", "$eq.text.endsWith(this.a, this.b, 'ordinal')")]
    [InlineData("var r = a.IndexOf(b, StringComparison.OrdinalIgnoreCase)", "$eq.text.indexOf(this.a, this.b, 'ordinalIgnoreCase')")]
    [InlineData("var r = a.IndexOf(b, 1, StringComparison.Ordinal)", "$eq.text.indexOf(this.a, this.b, 1, 'ordinal')")]
    [InlineData("var r = a.IndexOf(b, 1, 2, StringComparison.Ordinal)", "$eq.text.indexOf(this.a, this.b, 1, 2, 'ordinal')")]
    [InlineData("var r = a.IndexOf('x', StringComparison.OrdinalIgnoreCase)", "$eq.text.indexOf(this.a, 'x', 'ordinalIgnoreCase')")]
    [InlineData("var r = a.LastIndexOf(b, 3, StringComparison.OrdinalIgnoreCase)", "$eq.text.lastIndexOf(this.a, this.b, 3, 'ordinalIgnoreCase')")]
    [InlineData("var r = a.LastIndexOf(b, 3, 2, StringComparison.Ordinal)", "$eq.text.lastIndexOf(this.a, this.b, 3, 2, 'ordinal')")]
    [InlineData("var r = a.Contains(b, StringComparison.OrdinalIgnoreCase)", "$eq.text.contains(this.a, this.b, 'ordinalIgnoreCase')")]
    [InlineData("var r = a.Contains('x', StringComparison.Ordinal)", "$eq.text.contains(this.a, 'x', 'ordinal')")]
    [InlineData("var r = a.Replace(b, c, StringComparison.OrdinalIgnoreCase)", "$eq.text.replace(this.a, this.b, this.c, 'ordinalIgnoreCase')")]
    [InlineData("var r = a.Replace(b, c)", "$eq.text.replace(this.a, this.b, this.c, 'ordinal')")]
    [InlineData("var r = a.Equals(b, StringComparison.InvariantCultureIgnoreCase)", "$eq.text.instanceEquals(this.a, this.b, 'invariantCultureIgnoreCase')")]
    [InlineData("var r = a.CompareTo(b)", "$eq.text.compareTo(this.a, this.b)")]
    public void AComparingOverload_GoesToTheRuntime(string code, string expected) =>
        TestHelper.ConvertExpression(code).Should().Contain(expected);

    [Fact]
    public void ACharacterReplacement_StaysAJavaScriptReplacement() =>
        TestHelper.ConvertExpression("var r = a.Replace('x', 'y')").Should().Contain("this.a.replaceAll('x', 'y')");

    [Fact]
    public void AComparisonInAVariable_CrossesAsItsValue() =>
        TestHelper.ConvertExpression("var r = a.StartsWith(b, Active ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)")
            .Should().Contain("$eq.text.startsWith(this.a, this.b, this.active ? 'ordinal' : 'ordinalIgnoreCase')");

    [Fact]
    public void NamedArguments_KeepTheirParametersAndTheOrderTheyRunIn() =>
        TestHelper.ConvertExpression("var r = a.IndexOf(comparisonType: StringComparison.Ordinal, value: b)")
            .Should().Contain("(($0, $1, $2) => $eq.text.indexOf($0, $2, $1))(this.a, 'ordinal', this.b)");

    [Theory]
    [InlineData("var r = a.StartsWith(b, StringComparison.CurrentCulture)")]
    [InlineData("var r = a.EndsWith(b, StringComparison.InvariantCultureIgnoreCase)")]
    [InlineData("var r = a.IndexOf(b, StringComparison.InvariantCulture)")]
    [InlineData("var r = a.LastIndexOf(b, 2, StringComparison.CurrentCultureIgnoreCase)")]
    [InlineData("var r = a.Contains(b, StringComparison.InvariantCulture)")]
    [InlineData("var r = a.Replace(b, c, StringComparison.CurrentCultureIgnoreCase)")]
    public void ASearchByAConstantCultureComparison_IsRefused(string code) =>
        TestHelper.DiagnosticsFor(code).Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("a culture comparison has no search in the browser: search by Ordinal"));

    [Theory]
    [InlineData("var r = a.StartsWith(b, true, System.Globalization.CultureInfo.InvariantCulture)")]
    [InlineData("var r = a.EndsWith(b, false, null)")]
    [InlineData("var r = a.Replace(b, c, true, null)")]
    public void AnOverloadTakingACultureInfo_IsRefused(string code) =>
        TestHelper.DiagnosticsFor(code).Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("a CultureInfo has no search in the browser: pass StringComparison"));

    [Theory]
    [InlineData("var r = a.Equals(b, StringComparison.CurrentCulture)")]
    [InlineData("var r = a.ToUpper(System.Globalization.CultureInfo.InvariantCulture)")]
    [InlineData("var r = a.StartsWith(b)")]
    public void AWholeStringCultureComparisonAndACasingCulture_AreNotRefused(string code) =>
        TestHelper.DiagnosticsFor(code).Should().NotContain(d => d.Code == "EQ1004");

    [Fact]
    public void ANamedComparison_IsFoundByItsParameter()
    {
        // The start, written last, was read as the comparison and refused as a culture one (#536).
        TestHelper.DiagnosticsFor("var r = a.IndexOf(value: b, comparisonType: StringComparison.Ordinal, startIndex: 1)")
            .Should().NotContain(d => d.Code == "EQ1004");
        TestHelper.DiagnosticsFor("var r = a.StartsWith(comparisonType: StringComparison.InvariantCulture, value: b)")
            .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("a culture comparison has no search"));
    }

    [Fact]
    public void AComparisonThatIsNotAConstant_ReachesTheRuntime()
    {
        // The model says it is no constant; its spelling naming a culture member does not refuse it.
        const string code = "var r = a.StartsWith(b, Active ? StringComparison.Ordinal : StringComparison.CurrentCulture)";
        TestHelper.DiagnosticsFor(code).Should().NotContain(d => d.Code == "EQ1004");
        TestHelper.ConvertExpression(code)
            .Should().Contain("$eq.text.startsWith(this.a, this.b, this.active ? 'ordinal' : 'currentCulture')");
    }

    [Fact]
    public void CompareToAnObject_IsRefused() =>
        TestHelper.DiagnosticsFor("var r = a.CompareTo((object)b)")
            .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("string.CompareTo(object)"));

    [Theory]
    [InlineData("a.Replace(\"x\", \"$&\")", "$eq.text.replace(a, 'x', '$&', 'ordinal')")]
    [InlineData("a.Replace('x', 'y')", "$eq.text.replace(a, 'x', 'y', 'ordinal')")]
    public void WithNoModel_ATwoArgumentReplace_IsTheOrdinalReplace(string code, string expected) =>
        new CSharpToJsConverter().ConvertExpression(SyntaxFactory.ParseExpression(code)).Should().Be(expected);

    [Fact]
    public void WithNoModel_AComparisonSpelledLast_StillReachesTheRuntime()
    {
        // The playground compiles a buffer alone: the count of the arguments and a comparison
        // spelled last are the only evidence there is.
        var converter = new CSharpToJsConverter();
        converter.ConvertExpression(SyntaxFactory.ParseExpression("a.LastIndexOf(b, 2, StringComparison.OrdinalIgnoreCase)"))
            .Should().StartWith("$eq.text.lastIndexOf(a, b, ");
    }
}
