using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class StringStrategyTests
{
    // ============ Instance Methods ============

    [Fact]
    public void Split_NoArgs_MapsToSplitEmpty()
    {
        var result = TestHelper.ConvertExpression("str.Split()");
        result.Should().Be("this.str.split('')");
    }

    [Fact]
    public void Split_WithSeparator_MapsToSplit()
    {
        var result = TestHelper.ConvertExpression("str.Split(',')");
        result.Should().Be("this.str.split(',')");
    }

    [Fact]
    public void Replace_MapsToReplaceAll()
    {
        var result = TestHelper.ConvertExpression("str.Replace(\"old\", \"new\")");
        result.Should().Be("this.str.replaceAll('old', 'new')");
    }

    [Fact]
    public void StartsWith_MapsToStartsWith()
    {
        var result = TestHelper.ConvertExpression("str.StartsWith(\"prefix\")");
        result.Should().Be("this.str.startsWith('prefix')");
    }

    [Fact]
    public void EndsWith_MapsToEndsWith()
    {
        var result = TestHelper.ConvertExpression("str.EndsWith(\"suffix\")");
        result.Should().Be("this.str.endsWith('suffix')");
    }

    [Fact]
    public void Contains_String_MapsToIncludes()
    {
        var result = TestHelper.ConvertExpression("str.Contains(\"sub\")");
        result.Should().Be("this.str.includes('sub')");
    }

    [Fact]
    public void Substring_OneArg_GoesThroughTheRuntime()
    {
        // Not `.slice(5)`: JavaScript's own clamps an out-of-range index to an empty string where
        // .NET throws, so the call went quiet in the browser and loud on the server.
        var result = TestHelper.ConvertExpression("str.Substring(5)");
        result.Should().Be("$eq.text.substring(this.str, 5)");
    }

    [Fact]
    public void Substring_TwoArgs_GoesThroughTheRuntime()
    {
        // The runtime takes (start, LENGTH) as C# does, and refuses a range the string cannot give.
        var result = TestHelper.ConvertExpression("str.Substring(5, 10)");
        result.Should().Be("$eq.text.substring(this.str, 5, 10)");
    }

    [Fact]
    public void IndexOf_MapsToIndexOf()
    {
        var result = TestHelper.ConvertExpression("str.IndexOf(\"x\")");
        result.Should().Be("this.str.indexOf('x')");
    }

    [Fact]
    public void LastIndexOf_MapsToLastIndexOf()
    {
        var result = TestHelper.ConvertExpression("str.LastIndexOf(\"x\")");
        result.Should().Be("this.str.lastIndexOf('x')");
    }

    [Fact]
    public void PadLeft_MapsTopadStart()
    {
        var result = TestHelper.ConvertExpression("str.PadLeft(10)");
        result.Should().Be("this.str.padStart(10)");
    }

    [Fact]
    public void PadLeft_WithChar_MapsTopadStart()
    {
        var result = TestHelper.ConvertExpression("str.PadLeft(10, '0')");
        result.Should().Be("this.str.padStart(10, '0')");
    }

    [Fact]
    public void PadRight_MapsTopadEnd()
    {
        var result = TestHelper.ConvertExpression("str.PadRight(10)");
        result.Should().Be("this.str.padEnd(10)");
    }

    [Fact]
    public void TrimStart_MapsToTrimStart()
    {
        var result = TestHelper.ConvertExpression("str.TrimStart()");
        result.Should().Be("this.str.trimStart()");
    }

    [Fact]
    public void TrimEnd_MapsToTrimEnd()
    {
        var result = TestHelper.ConvertExpression("str.TrimEnd()");
        result.Should().Be("this.str.trimEnd()");
    }

    [Fact]
    public void ToCharArray_MapsToSpread()
    {
        var result = TestHelper.ConvertExpression("str.ToCharArray()");
        result.Should().Be("[...this.str]");
    }

    [Fact]
    public void Insert_MapsToSliceConcat()
    {
        var result = TestHelper.ConvertExpression("str.Insert(5, \"text\")");
        result.Should().Be("(this.str.slice(0, 5) + 'text' + this.str.slice(5))");
    }

    [Fact]
    public void Remove_OneArg_MapsToSlice()
    {
        var result = TestHelper.ConvertExpression("str.Remove(5)");
        result.Should().Be("this.str.slice(0, 5)");
    }

    [Fact]
    public void Remove_TwoArgs_MapsToSliceConcat()
    {
        var result = TestHelper.ConvertExpression("str.Remove(5, 3)");
        result.Should().Be("(this.str.slice(0, 5) + this.str.slice(5 + 3))");
    }

    [Fact]
    public void Trim_MapsToTrim()
    {
        var result = TestHelper.ConvertExpression("str.Trim()");
        result.Should().Be("this.str.trim()");
    }

    [Fact]
    public void ToUpper_MapsToToUpperCase()
    {
        var result = TestHelper.ConvertExpression("str.ToUpper()");
        result.Should().Be("this.str.toUpperCase()");
    }

    [Fact]
    public void ToLower_MapsToToLowerCase()
    {
        var result = TestHelper.ConvertExpression("str.ToLower()");
        result.Should().Be("this.str.toLowerCase()");
    }

    [Fact]
    public void ToUpperInvariant_MapsToToUpperCase()
    {
        var result = TestHelper.ConvertExpression("str.ToUpperInvariant()");
        result.Should().Be("this.str.toUpperCase()");
    }

    [Fact]
    public void ToLowerInvariant_MapsToToLowerCase()
    {
        var result = TestHelper.ConvertExpression("str.ToLowerInvariant()");
        result.Should().Be("this.str.toLowerCase()");
    }

    // ============ Static Methods ============

    [Fact]
    public void IsNullOrEmpty_MapsToFalsyCheck()
    {
        var result = TestHelper.ConvertExpression("string.IsNullOrEmpty(str)");
        result.Should().Be("!this.str");
    }

    [Fact]
    public void IsNullOrWhiteSpace_MapsToTrimCheck()
    {
        var result = TestHelper.ConvertExpression("string.IsNullOrWhiteSpace(str)");
        result.Should().Be("(!this.str || !this.str.trim())");
    }

    [Fact]
    public void Join_MapsToArrayJoin()
    {
        var result = TestHelper.ConvertExpression("string.Join(\", \", items)");
        result.Should().Be("this.items.join(', ')");
    }

    [Fact]
    public void Concat_JoinsTheTextOfEachValue()
    {
        // Text, not a sum: a null string is nothing, as .NET writes it.
        var result = TestHelper.ConvertExpression("string.Concat(a, b, c)");
        result.Should().Be("'' + (this.a ?? '') + (this.b ?? '') + (this.c ?? '')");
    }

    [Fact]
    public void Compare_IsTheCurrentCulturesComparison()
    {
        var result = TestHelper.ConvertExpression("string.Compare(a, b)");
        result.Should().Be("$eq.text.compare(this.a, this.b, 'currentCulture')");
    }

    [Fact]
    public void Compare_WithACultureOrCompareOptions_IsRefused()
    {
        // No form this side reads either: dropping them compared as if neither had been passed.
        TestHelper.DiagnosticsFor(
                "var r = string.Compare(a, b, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.CompareOptions.IgnoreCase)")
            .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("string.Compare with a CultureInfo"));
        TestHelper.DiagnosticsFor("var r = string.Compare(a, b, StringComparison.OrdinalIgnoreCase)")
            .Should().NotContain(d => d.Code == "EQ1004", "a StringComparison crosses as its name");
    }

    [Fact]
    public void Format_MapsToRuntimeHelper()
    {
        var result = TestHelper.ConvertExpression("string.Format(\"{0} + {1}\", a, b)");
        result.Should().Be("$eq.text.stringFormat('{0} + {1}', this.a, this.b)");
    }

    [Fact]
    public void Format_WithSpecifier_PreservesFormatString()
    {
        var result = TestHelper.ConvertExpression("string.Format(\"{0:F2}\", Id)");
        result.Should().Be("$eq.text.stringFormat('{0:F2}', this.id)");
    }

    /// <summary>
    /// The provider overload binds its template by the method, never the provider (#377): taken
    /// for the template, the provider reached the browser as `CultureInfo.invariantCulture`.
    /// </summary>
    [Fact]
    public void Format_WithTheInvariantCulture_FormatsInvariantly_AndLeavesTheProviderOut()
    {
        var result = TestHelper.ConvertExpression("string.Format(System.Globalization.CultureInfo.InvariantCulture, \"{0}\", Amount)");
        result.Should().Be("$eq.text.stringFormatInvariant('{0}', this.amount)");
    }

    [Fact]
    public void Format_WithTheCurrentCulture_FormatsAsTheAppsCulture()
    {
        var result = TestHelper.ConvertExpression("string.Format(System.Globalization.CultureInfo.CurrentCulture, \"{0}\", Amount)");
        result.Should().Be("$eq.text.stringFormat('{0}', this.amount)");
    }

    [Fact]
    public void Format_WithAnotherProvider_IsABuildError()
    {
        TestHelper.DiagnosticsFor("string.Format(System.Globalization.CultureInfo.GetCultureInfo(\"pt-BR\"), \"{0}\", Amount)")
            .Should().Contain(d => d.Code == "EQ2108");
    }

    /// <summary>A params array passed as itself is the values, as C#'s normal form reads it, and one
    /// written in place IS its elements, each passed as a value of its own (an array variable is
    /// spread, which the conformance suite runs).</summary>
    [Fact]
    public void Format_WithAParamsArrayWrittenInPlace_PassesItsElements()
    {
        var result = TestHelper.ConvertExpression("string.Format(\"{0} {1}\", new object[] { a, b })");
        result.Should().Be("$eq.text.stringFormat('{0} {1}', this.a, this.b)");
    }

    [Fact]
    public void Equals_MapsToStrictEquality()
    {
        var result = TestHelper.ConvertExpression("string.Equals(a, b)");
        result.Should().Be("(this.a === this.b)");
    }
}
