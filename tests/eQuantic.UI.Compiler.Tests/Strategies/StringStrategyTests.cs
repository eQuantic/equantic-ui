using eQuantic.UI.Compiler.CodeGen;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

public class StringStrategyTests
{
    // ============ Instance Methods ============

    [Fact]
    public void Split_NoArgs_SplitsOnDotNetsWhiteSpace()
    {
        // It was `split('')`, which cut the text into its characters: with no separator .NET splits
        // on white space (WhiteSpaceConformanceTests runs both sides).
        var result = TestHelper.ConvertExpression("str.Split()");
        result.Should().Be("$eq.text.splitAny(this.str)");
    }

    [Fact]
    public void Split_WithSeparator_IsTheRuntimesSplit()
    {
        // JavaScript's split was handed the first argument whole, and dropped the count and the
        // options (#753); StringSplitAndSearchConformanceTests runs both sides.
        var result = TestHelper.ConvertExpression("str.Split(',')");
        result.Should().Be("$eq.text.split(this.str, ',')");
    }

    [Fact]
    public void Split_ByASet_TakesItsCountAndOptionsInTheirPlaces()
    {
        var result = TestHelper.ConvertExpression("str.Split(new[] { \"ab\", \"c\" }, StringSplitOptions.TrimEntries)");
        result.Should().Be("$eq.text.splitAny(this.str, ['ab', 'c'], undefined, 2)");
    }

    [Fact]
    public void Split_OverASpanHandedWhole_FailsTheBuild()
    {
        // A collection expression is an array in the browser; any other span has no form there.
        TestHelper.DiagnosticsFor("str.Split(\",;\".AsSpan())").Should().Contain(d => d.Code == "EQ1004");
    }

    /// <summary>With no model, the spelling places what it can: chars written one by one are a set, and
    /// so is an array or any count of arguments past three; options are spelled or named as such, a
    /// count is a number or named as one, and a named argument in its parameter's place is placed.</summary>
    [Theory]
    [InlineData("s.Split()", "$eq.text.splitAny(s)")]
    [InlineData("s.Split(',', ';')", "$eq.text.splitAny(s, [',', ';'])")]
    [InlineData("s.Split(a, b, c, d)", "$eq.text.splitAny(s, [a, b, c, d])")]
    [InlineData("s.Split(new[] { \"ab\", \"c\" }, StringSplitOptions.None)", "$eq.text.splitAny(s, ['ab', 'c'], undefined, 0)")]
    [InlineData("s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)", "$eq.text.split(s, ',', undefined, 3)")]
    [InlineData("s.Split(',', 2, StringSplitOptions.TrimEntries)", "$eq.text.split(s, ',', 2, 2)")]
    [InlineData("s.Split(',', 2)", "$eq.text.split(s, ',', 2)")]
    [InlineData("s.Split(sep, 2, o)", "$eq.text.split(s, sep, 2, o)")]
    [InlineData("s.Split(sep, n, StringSplitOptions.None)", "$eq.text.split(s, sep, n, 0)")]
    [InlineData("s.Split(new[] { a }, n, o)", "$eq.text.splitAny(s, [a], n, o)")]
    [InlineData("s.Split(separator: ',', options: o)", "$eq.text.split(s, ',', undefined, o)")]
    [InlineData("s.Split(',', count: n)", "$eq.text.split(s, ',', n)")]
    [InlineData("s.Split(sep, count: n, options: o)", "$eq.text.split(s, sep, n, o)")]
    [InlineData("s.Split((char[])null)", "$eq.text.splitAny(s, null)")]
    [InlineData("s.Split(((char[]?)null))", "$eq.text.splitAny(s, null)")]
    [InlineData("s.Split(default(char[]))", "$eq.text.splitAny(s, null)")]
    [InlineData("s.Split((string[])parts, StringSplitOptions.None)", "$eq.text.splitAny(s, parts, undefined, 0)")]
    [InlineData("s.Split((char[])x, n, o)", "$eq.text.splitAny(s, x, n, o)")]
    public void Split_WithNoModel_PlacesWhatItsSpellingSays(string code, string expected) =>
        new CSharpToJsConverter().ConvertExpression(SyntaxFactory.ParseExpression(code)).Should().Be(expected);

    /// <summary>A value converted with <c>as</c> to an array is a set of separators too, with no model.</summary>
    [Fact]
    public void Split_WithNoModel_OfAValueAsAnArray_IsASetOfThem() =>
        new CSharpToJsConverter().ConvertExpression(SyntaxFactory.ParseExpression("s.Split(x as string[], n, o)"))
            .Should().StartWith("$eq.text.splitAny(s, ").And.EndWith(", n, o)");

    /// <summary>With no model, a search's start, count and comparison are placed where the spelling
    /// settles them: a number or a name is a start or a count, a spelled or named comparison is one, and
    /// a place only an int takes (the second of three, the second and third of four) holds one.</summary>
    [Theory]
    [InlineData("a.LastIndexOf(value: \"bc\", startIndex: start)", "$eq.text.lastIndexOf(a, 'bc', start, 'ordinal')")]
    [InlineData("a.IndexOf(\"bc\", startIndex: start)", "$eq.text.indexOf(a, 'bc', start, 'ordinal')")]
    [InlineData("a.IndexOf(v, start, 2)", "$eq.text.indexOf(a, v, start, 2, 'ordinal')")]
    [InlineData("a.IndexOf(v, start, count: n)", "$eq.text.indexOf(a, v, start, n, 'ordinal')")]
    [InlineData("a.LastIndexOf(v, start, n, comparison)", "$eq.text.lastIndexOf(a, v, start, n, comparison)")]
    [InlineData("a.IndexOf(v, comparisonType: comparison)", "$eq.text.indexOf(a, v, comparison)")]
    [InlineData("a.LastIndexOf(\"x\", i - 1, 2)", "$eq.text.lastIndexOf(a, 'x', i - 1, 2, 'ordinal')")]
    [InlineData("a.IndexOf('x', startIndex: start)", "$eq.text.indexOfChar(a, 'x', start)")]
    public void Search_WithNoModel_PlacesWhatItsSpellingSays(string code, string expected) =>
        new CSharpToJsConverter().ConvertExpression(SyntaxFactory.ParseExpression(code)).Should().Be(expected);

    /// <summary>With no model, what the spelling cannot place is a build error rather than a guess, as
    /// <c>string.Format</c>'s first argument is: a name in a place a start and a comparison share (or a
    /// count and options, or three chars and a separator, a count and options), and a named argument
    /// out of its parameter's place, which nothing here moves.</summary>
    [Theory]
    [InlineData("s.Split(',', o)")]
    [InlineData("s.Split(sep, n, o)")]
    [InlineData("s.Split(',', options: o, count: 2)")]
    [InlineData("s.Split(count: 2, separator: ',')")]
    [InlineData("s.IndexOf(v, comparison)")]
    [InlineData("s.LastIndexOf(\"bc\", start)")]
    [InlineData("s.IndexOf('x', start)")]
    [InlineData("s.IndexOf(v, start, other)")]
    [InlineData("s.IndexOf(\"x\", count: 2, startIndex: 1)")]
    [InlineData("s.LastIndexOf(startIndex: 1, value: \"x\")")]
    public void ANameTheSpellingCannotPlace_IsNotGuessed(string code)
    {
        var converter = new CSharpToJsConverter();
        converter.ConvertExpression(SyntaxFactory.ParseExpression(code));
        converter.Diagnostics.Should().Contain(d => d.Code == "EQ1004");
    }

    [Fact]
    public void Split_OfCharsWrittenOneByOne_IsASetOfThem()
    {
        var result = TestHelper.ConvertExpression("str.Split(',', ';')");
        result.Should().Be("$eq.text.splitAny(this.str, [',', ';'])");
    }

    [Fact]
    public void Replace_IsTheRuntimesOrdinalReplace()
    {
        // replaceAll read $& in the replacement as a pattern and wrote a null one as "null" (#528).
        var result = TestHelper.ConvertExpression("str.Replace(\"old\", \"new\")");
        result.Should().Be("$eq.text.replace(this.str, 'old', 'new', 'ordinal')");
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
    public void LastIndexOf_FromAStartForACount_IsTheRuntimesOrdinalSearch()
    {
        // JavaScript's lastIndexOf takes no count and reads its start as where a match may begin,
        // where .NET's is where it must end by (#724).
        var result = TestHelper.ConvertExpression("str.LastIndexOf(\"x\", 4, 2)");
        result.Should().Be("$eq.text.lastIndexOf(this.str, 'x', 4, 2, 'ordinal')");
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
    public void TrimStart_TrimsDotNetsWhiteSpace()
    {
        var result = TestHelper.ConvertExpression("str.TrimStart()");
        result.Should().Be("$eq.text.trimStart(this.str)");
    }

    [Fact]
    public void TrimEnd_TrimsDotNetsWhiteSpace()
    {
        var result = TestHelper.ConvertExpression("str.TrimEnd()");
        result.Should().Be("$eq.text.trimEnd(this.str)");
    }

    [Fact]
    public void ToCharArray_SplitsIntoCodeUnits()
    {
        // A char is one UTF-16 code unit: a spread iterates code points, and handed a pair it made
        // one two-unit element where .NET makes two chars (#524).
        var result = TestHelper.ConvertExpression("str.ToCharArray()");
        result.Should().Be("this.str.split('')");
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
    public void Trim_TrimsDotNetsWhiteSpace()
    {
        // JavaScript's `trim` leaves U+0085 and takes U+FEFF, which .NET does the other way round.
        var result = TestHelper.ConvertExpression("str.Trim()");
        result.Should().Be("$eq.text.trim(this.str)");
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
    public void IsNullOrWhiteSpace_ReadsItsArgumentOnce_AsDotNetsWhiteSpace()
    {
        var result = TestHelper.ConvertExpression("string.IsNullOrWhiteSpace(str)");
        result.Should().Be("(!$eq.text.hasNonWhiteSpace(this.str))");
    }

    [Fact]
    public void Join_ReadsTheSequenceInTheRuntime()
    {
        // A sequence of strings needs no conversion, and the runtime refuses a null one by name.
        var result = TestHelper.ConvertExpression("string.Join(\", \", items)");
        result.Should().Be("$eq.text.join(', ', this.items)");
    }

    [Fact]
    public void Join_WritesEachValueByItsElementType()
    {
        // A bool as True or False: the conversion a concatenation applies, per element (#441).
        TestHelper.ConvertExpression("string.Join(\",\", new[] { true, false })")
            .Should().Be("$eq.text.join(',', [true, false], ($value) => $eq.text.format($value, null))");
    }

    [Fact]
    public void Join_WritesValuesPassedOneByOneByTheirOwnTypes()
    {
        // The params array in its expanded form: each value its own text, then the array's join.
        TestHelper.ConvertExpression("string.Join(\",\", 1, true)")
            .Should().Be("[1, $eq.text.format(true, null)].join(',')");
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
        // An int beside a specifier says it is one: it rounds a formatted half away from zero (#393).
        var result = TestHelper.ConvertExpression("string.Format(\"{0:F2}\", Id)");
        result.Should().Be("$eq.text.stringFormat('{0:F2}', $eq.text.asNumber(this.id, 'int32'))");
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

    /// <summary>With no model to bind the call, a named culture or a null in first place is still
    /// the provider, known by its spelling: taken for the template, it put `CultureInfo` in the
    /// browser. The string API converts with no model at all.</summary>
    [Theory]
    [InlineData("string.Format(CultureInfo.InvariantCulture, \"{0}\", x)", "$eq.text.stringFormatInvariant('{0}', x)")]
    [InlineData("string.Format(System.Globalization.CultureInfo.CurrentCulture, \"{0}\", x)", "$eq.text.stringFormat('{0}', x)")]
    [InlineData("string.Format(null, \"{0}\", x)", "$eq.text.stringFormat('{0}', x)")]
    [InlineData("string.Format((IFormatProvider?)null, \"{0}\", x)", "$eq.text.stringFormat('{0}', x)")]
    [InlineData("string.Format(default(IFormatProvider), \"{0}\", x)", "$eq.text.stringFormat('{0}', x)")]
    [InlineData("string.Format(\"{0}\", x)", "$eq.text.stringFormat('{0}', x)")]
    [InlineData("string.Format(\"{0} {1}\", new object[] { a, b })", "$eq.text.stringFormat('{0} {1}', a, b)")]
    [InlineData("string.Format(\"{0} {1}\", new string[] { a, b })", "$eq.text.stringFormat('{0} {1}', a, b)")]
    [InlineData("string.Format(\"{0} {1}\", [a, b])", "$eq.text.stringFormat('{0} {1}', a, b)")]
    public void Format_WithNoModel_KnowsTheProviderByItsSpelling(string code, string expected) =>
        new CSharpToJsConverter().ConvertExpression(SyntaxFactory.ParseExpression(code)).Should().Be(expected);

    /// <summary>With no model, a first argument that is neither text nor a named culture could be
    /// the template or a provider, and the spelling cannot say which: `string.Format(format, x)` and
    /// `string.Format(provider, "", x)` read the same. It is a build error rather than a guess, and so
    /// is a named argument.</summary>
    [Theory]
    [InlineData("string.Format(provider, \"{0}\", x)")]
    [InlineData("string.Format(provider, \"\", x)")]
    [InlineData("string.Format(format, x)")]
    [InlineData("string.Format(format: \"{0}\", arg0: x)")]
    [InlineData("string.Format(\"{0}\", new[] { a })")]
    [InlineData("string.Format(\"{0}\", new Thing[] { a })")]
    public void Format_WithNoModel_RefusesWhatItCannotPlace(string code)
    {
        var converter = new CSharpToJsConverter();
        converter.ConvertExpression(SyntaxFactory.ParseExpression(code));
        converter.Diagnostics.Should().Contain(d => d.Code == "EQ1004");
    }

    [Fact]
    public void Equals_MapsToStrictEquality()
    {
        var result = TestHelper.ConvertExpression("string.Equals(a, b)");
        result.Should().Be("(this.a === this.b)");
    }
}
