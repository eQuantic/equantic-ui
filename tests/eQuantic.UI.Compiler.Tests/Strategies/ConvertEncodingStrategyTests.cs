using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// <c>Convert.ToBase64String</c>, <c>ToBase64CharArray</c>, <c>ToHexString</c> and
/// <c>ToHexStringLower</c> as the runtime's twins (#716), and <c>TryParse(string, out T)</c> of the five
/// time types as an inline answer over theirs (#718). What each one answers is the conformance suite's
/// (<c>ConvertEncodingConformanceTests</c>, <c>DateTimeTryParseConformanceTests</c>); these pin what
/// only the compiler can show: where each argument lands, the spelling read where no model can be
/// asked, and what is refused.
/// </summary>
public class ConvertEncodingStrategyTests
{
    /// <summary>Each argument lands in the twin's parameter it fills, the ones the overload does not
    /// take <c>undefined</c> where a later one is passed.</summary>
    [Theory]
    [InlineData("Convert.ToBase64String(new byte[] { 1 })", "$eq.convert.toBase64String([1])")]
    [InlineData("Convert.ToBase64String(new byte[] { 1 }, Base64FormattingOptions.InsertLineBreaks)",
        "$eq.convert.toBase64String([1], undefined, undefined, 1)")]
    [InlineData("Convert.ToBase64String(new byte[] { 1 }, 0, 1)", "$eq.convert.toBase64String([1], 0, 1)")]
    [InlineData("Convert.ToHexStringLower([1, 2])", "$eq.convert.toHexStringLower([1, 2])")]
    [InlineData("Convert.ToBase64CharArray(new byte[] { 1 }, 0, 1, new char[] { 'x' }, 0)",
        "$eq.convert.toBase64CharArray([1], 0, 1, ['x'], 0)")]
    public void AnEncoding_IsTheRuntimesTwin(string code, string expected) =>
        TestHelper.ConvertExpression(code).Should().Be(expected);

    /// <summary>A span that is not a collection expression has no JavaScript form.</summary>
    [Fact]
    public void AnEncoding_OverASpan_IsRefused() =>
        TestHelper.DiagnosticsFor("Convert.ToHexString((ReadOnlySpan<byte>)new byte[] { 1 })")
            .Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("over a span"));

    /// <summary>With no model, the count settles the overload and a named argument fills the
    /// parameter it names.</summary>
    [Theory]
    [InlineData("Convert.ToBase64String(b)", "$eq.convert.toBase64String(b)")]
    [InlineData("Convert.ToBase64String(b, o)", "$eq.convert.toBase64String(b, undefined, undefined, o)")]
    [InlineData("System.Convert.ToHexString(b, 1, 2)", "$eq.convert.toHexString(b, 1, 2)")]
    [InlineData("Convert.ToBase64String(b, 1, 2, o)", "$eq.convert.toBase64String(b, 1, 2, o)")]
    [InlineData("Convert.ToBase64String(bytes: b, options: o)", "$eq.convert.toBase64String(b, undefined, undefined, o)")]
    [InlineData("Convert.ToBase64CharArray(b, 0, n, chars, 2)", "$eq.convert.toBase64CharArray(b, 0, n, chars, 2)")]
    [InlineData("Convert.ToBase64CharArray(b, 0, n, chars, 2, o)", "$eq.convert.toBase64CharArray(b, 0, n, chars, 2, o)")]
    public void AnEncoding_WithNoModel_PlacesWhatItsSpellingSays(string code, string expected) =>
        new CSharpToJsConverter().ConvertExpression(SyntaxFactory.ParseExpression(code)).Should().Be(expected);

    /// <summary>With no model, a count no overload has, or a name the overload of that count does not
    /// take, is a build error rather than a guess.</summary>
    [Theory]
    [InlineData("Convert.ToHexString(b, o)")]
    [InlineData("Convert.ToBase64CharArray(b, 0, n)")]
    [InlineData("Convert.ToBase64String(b, offset: 1)")]
    [InlineData("Convert.ToHexString(b, options: o, length: 2)")]
    public void AnEncoding_WithNoModel_RefusesWhatItCannotPlace(string code)
    {
        var converter = new CSharpToJsConverter();
        converter.ConvertExpression(SyntaxFactory.ParseExpression(code));
        converter.Diagnostics.Should().Contain(d => d.Code == "EQ1004");
    }

    /// <summary>A class of the app's that shares the name keeps its own methods: the call is the app's,
    /// not the twin's.</summary>
    [Fact]
    public void AClassNamedConvert_IsTheAppsOwn()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            using System;
            public static class Convert { public static string ToHexString(byte[] b) => "mine"; }
            public class Sample { public string Hex(byte[] b) => Convert.ToHexString(b); }
            """);
        var compilation = CSharpCompilation.Create("ClassUnderTest", [tree], [TestReferences.Of(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var converter = new CSharpToJsConverter { SymbolsAreAuthoritative = true };
        converter.SetSemanticModel(compilation.GetSemanticModel(tree));

        converter.ConvertExpression(tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single())
            .Should().Be("Convert.toHexString(b)");
    }

    /// <summary>A TryParse answers inline over the twin's reader, the type's zero in its out argument
    /// where the text is no value of it.</summary>
    [Theory]
    [InlineData("DateTime.TryParse(str, out var d)",
        "((d = $eq.time.dateTime.tryParse(this.str)) !== undefined || ((d = $eq.time.dateTime.minValue()), false))")]
    [InlineData("DateTimeOffset.TryParse(str, out var d)",
        "((d = $eq.time.dateTimeOffset.tryParse(this.str)) !== undefined || ((d = $eq.time.dateTimeOffset.minValue()), false))")]
    [InlineData("DateOnly.TryParse(str, out var d)",
        "((d = $eq.time.dateOnly.tryParse(this.str)) !== undefined || ((d = $eq.time.dateOnly.minValue()), false))")]
    [InlineData("TimeOnly.TryParse(str, out var d)",
        "((d = $eq.time.timeOnly.tryParse(this.str)) !== undefined || ((d = $eq.time.timeOnly.minValue()), false))")]
    [InlineData("TimeSpan.TryParse(str, out var d)",
        "((d = $eq.time.timeSpan.tryParse(this.str)) !== undefined || ((d = $eq.time.timeSpan.zero), false))")]
    [InlineData("TimeSpan.TryParse(str, out _)", "($eq.time.timeSpan.tryParse(this.str) !== undefined)")]
    public void ATimeTryParse_AnswersOverTheTwinsReader(string code, string expected) =>
        TestHelper.ConvertExpression(code).Should().Be(expected);

    /// <summary>With no model, the type's spelling and two arguments, one of them <c>out</c>, settle it.</summary>
    [Theory]
    [InlineData("DateOnly.TryParse(s, out var d)",
        "((d = $eq.time.dateOnly.tryParse(s)) !== undefined || ((d = $eq.time.dateOnly.minValue()), false))")]
    [InlineData("System.TimeSpan.TryParse(result: out var d, s: s)",
        "((d = $eq.time.timeSpan.tryParse(s)) !== undefined || ((d = $eq.time.timeSpan.zero), false))")]
    public void ATimeTryParse_WithNoModel_ReadsTheTypesSpelling(string code, string expected) =>
        new CSharpToJsConverter().ConvertExpression(SyntaxFactory.ParseExpression(code)).Should().Be(expected);

    /// <summary>Every other overload fails the build: handed to the twin, it would read the text and
    /// never write its out argument, where the page used to fail on a member the twin did not have.</summary>
    [Theory]
    [InlineData("DateTime.TryParse(str, null, out var d)")]
    [InlineData("DateOnly.TryParse(str, null, System.Globalization.DateTimeStyles.None, out var d)")]
    [InlineData("TimeSpan.TryParse(str.AsSpan(), out var d)")]
    public void ATimeTryParse_OfAnotherOverload_IsRefused(string code) =>
        TestHelper.DiagnosticsFor(code).Should().Contain(d => d.Code == "EQ1004" && d.Message.Contains("TryParse with a provider"));

    [Fact]
    public void ATimeTryParse_WithNoModel_OfAnotherOverload_IsRefused()
    {
        var converter = new CSharpToJsConverter();
        converter.ConvertExpression(SyntaxFactory.ParseExpression("DateTime.TryParse(s, provider, out var d)"));
        converter.Diagnostics.Should().Contain(d => d.Code == "EQ1004");
    }
}
