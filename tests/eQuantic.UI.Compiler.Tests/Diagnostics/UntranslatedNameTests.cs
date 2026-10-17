using eQuantic.UI.Compiler.CodeGen;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Diagnostics;

/// <summary>
/// A .NET name nothing in the browser defines fails the build naming it (EQ2004, or the policy a
/// culture has, EQ2108), and is never written (#839): a method group whose call has no translation, a
/// TimeSpan member its twin does not carry, a <c>FormattableString</c> the browser cannot hold, and a
/// construction of a class with no twin. Each built with no diagnostic and named something undefined,
/// <c>ts.tryFormat(…)</c>, <c>new Random()</c>, <c>new Func(…)</c>.
/// </summary>
public class UntranslatedNameTests
{
    private const string Types = """
        public delegate bool SpanReader(string text, out TimeSpan value);
        public delegate bool Reader(string text, out int value);
        """;

    /// <summary>The refusal, and what the module must not name.</summary>
    [Theory]
    // A group whose call nothing translates, and one whose method hands a value back through an out.
    [InlineData("Func<string, string, IFormatProvider, TimeSpan> read = TimeSpan.ParseExact; return read(\"1\", \"c\", null);",
        "EQ2004", "'System.TimeSpan.ParseExact'", "parseExact")]
    [InlineData("Func<string, string> encode = System.Net.WebUtility.HtmlEncode; return encode(\"a\");",
        "EQ2004", "'System.Net.WebUtility.HtmlEncode'", "htmlEncode")]
    [InlineData("SpanReader read = TimeSpan.TryParse; return read(\"1\", out var t);", "EQ2004", "'System.TimeSpan.TryParse'", "tryParse")]
    // A TimeSpan member no twin carries.
    [InlineData("var t = TimeSpan.FromSeconds(1); var buffer = new char[16]; return t.TryFormat(buffer, out var written);",
        "EQ2004", "'System.TimeSpan.TryFormat'", null)]
    // A date's, a time's and an instant's members no twin carries: the interop forms, and a wrapped day handed back.
    [InlineData("return new DateTime(2024, 1, 2).ToBinary();", "EQ2004", "'System.DateTime.ToBinary'", null)]
    [InlineData("return DateTime.FromOADate(1.5);", "EQ2004", "'System.DateTime.FromOADate'", null)]
    [InlineData("return new DateTime(2024, 1, 2).IsDaylightSavingTime();", "EQ2004", "'System.DateTime.IsDaylightSavingTime'", null)]
    [InlineData("return DateTimeOffset.UtcNow.ToFileTime();", "EQ2004", "'System.DateTimeOffset.ToFileTime'", null)]
    [InlineData("return new TimeOnly(23, 0).AddHours(2, out var wrapped);", "EQ2004", "'System.TimeOnly.AddHours'", null)]
    // A FormattableString the browser cannot hold: its format and its arguments are gone once it is text.
    [InlineData("var x = 1.5; FormattableString text = $\"{x}\"; return text.ToString();", "EQ2004", "'System.FormattableString'", "FormattableString")]
    [InlineData("var x = 1.5; FormattableString text = $\"{x}\"; return FormattableString.Invariant(text);", "EQ2004", "'System.FormattableString'", "FormattableString")]
    [InlineData("var x = 1.5; return string.Create(new System.Globalization.CultureInfo(\"pt-BR\"), $\"{x}\");", "EQ2108", "InvariantCulture", "create")]
    // A delegate built from another whose parameters hand a value back, made over a null one and over a
    // function: no function the browser calls has an out or a ref (Copilot's review of #958).
    [InlineData("Reader r = null; return new Reader(r);", "EQ2004", "'Reader'", null)]
    [InlineData("Reader r = (string s, out int v) => { v = s.Length; return true; }; return new Reader(r);", "EQ2004", "'Reader'", null)]
    // A construction of a class the browser has no twin of.
    [InlineData("return new Random();", "EQ2004", "'System.Random'", "Random")]
    [InlineData("Random r = new(); return r;", "EQ2004", "'System.Random'", "Random")]
    [InlineData("return new System.Diagnostics.Stopwatch();", "EQ2004", "'System.Diagnostics.Stopwatch'", "Stopwatch")]
    [InlineData("return new Lazy<int>(() => 1);", "EQ2004", "'System.Lazy<int>'", "Lazy")]
    [InlineData("return new Tuple<int, int>(1, 2);", "EQ2004", "'System.Tuple<int, int>'", "Tuple")]
    public void ANameTheBrowserLacks_FailsTheBuild_NamingIt(string body, string code, string named, string? absent)
    {
        var (js, diagnostics) = Convert(body);

        diagnostics.Should().Contain(d => d.Code == code && d.Message.Contains(named),
            "the build names what the browser does not define");
        diagnostics.Where(d => d.Severity == ConversionSeverity.Error).Should().OnlyContain(d => d.Code == code,
            "the one refusal, and no other for the same name");
        // A refused group and a refused construction are written as nothing; a refused call keeps its
        // text, and the build that refused it writes no module.
        if (absent is not null) js.Should().NotContain(absent, "a refused name is never written");
    }

    /// <summary>A group refused is refused once, where it is written.</summary>
    [Fact]
    public void ARefusedGroup_IsReportedOnce_WhereItIsWritten()
    {
        var (_, diagnostics) = Convert("Func<string, string> encode = System.Net.WebUtility.HtmlEncode;\n        return encode(\"a\");");

        diagnostics.Should().ContainSingle().Which.Line.Should().Be(4, "the group is on the method's first line");
    }

    private static (string Js, IReadOnlyList<ConversionDiagnostic> Diagnostics) Convert(string body)
    {
        var source = "using System;\nusing System.Linq;\npublic class Sample { public object Run() {\n        " + body + "\n    } }\n" + Types;
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("UntranslatedName", [tree], References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var converter = new CSharpToJsConverter { SymbolsAreAuthoritative = true };
        converter.SetSemanticModel(compilation.GetSemanticModel(tree));
        var run = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "Run");
        return (converter.Convert(run.Body!), converter.Diagnostics.ToList());
    }

    /// <summary>The framework, through the one owner of the test process's references (#549).</summary>
    private static readonly Lazy<MetadataReference[]> References = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(TestReferences.Of)
            .ToArray());
}
