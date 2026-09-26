using System.Reflection;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// Cross-assembly IconGlyph inlining (the write-once icon-pack client contract): a reference to a
/// pack's <c>static readonly IconGlyph</c> transpiles to the constructor AT THE USE SITE — so a
/// pack tree-shakes to only the glyphs referenced, with no per-pack JS module. The pack SOURCE is
/// in the compilation (a reference-source tree); the initializer is converted with that tree's model.
/// </summary>
public class InlinedConstantTests
{
    // A minimal in-compilation stand-in for eQuantic.UI.Primitives — the strategy keys on the
    // fully-qualified type name, so the real namespace is all that matters here.
    private const string PrimitivesSource = """
        namespace eQuantic.UI.Primitives;
        public enum IconGlyphStyle : byte { Fill = 0, Stroke = 1 }
        public readonly record struct IconGlyph(
            string Name, string Path, IconGlyphStyle Style = IconGlyphStyle.Fill,
            string ViewBox = "0 0 24 24", float StrokeWidth = 2);
        """;

    private const string PackSource = """
        using eQuantic.UI.Primitives;
        namespace eQuantic.UI.Lucide;
        public static class LucideIcons
        {
            // The shape the packs SHIP (expression-bodied properties, so the IL trimmer can drop
            // the thousands an app never names — a field pack is one static constructor and ships
            // whole: 14.5 MB to draw five glyphs, measured).
            public static IconGlyph Camera => new("camera", "M14.5 4h-5L7 7", IconGlyphStyle.Stroke);
            // And the FIELD shape, which must keep inlining: the rule is about the value, not
            // about which kind of member holds it, and a hand-written pack may still use one.
            public static readonly IconGlyph Heart = new("heart", "M12 21l-1-1");
        }
        """;

    private static string Transpile(string componentSource)
    {
        var trees = new[]
        {
            CSharpSyntaxTree.ParseText(PrimitivesSource, path: "Primitives.cs"),
            CSharpSyntaxTree.ParseText(PackSource, path: "LucideIcons.cs"),
            CSharpSyntaxTree.ParseText(componentSource, path: "IconStrip.cs"),
        };
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("Inlining", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var compiler = new ComponentCompiler();
        compiler.SetProjectCompilation(compilation);
        var result = compiler.CompileSource(componentSource, "IconStrip.cs").Single();
        result.Success.Should().BeTrue(string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.TypeScript;
    }

    /// <summary>
    /// A constant is written as its value in its C# type (#447): a decimal as the runtime's exact
    /// Decimal, reached through its type or a <c>using static</c>, and a long as a BigInt. Neither had
    /// a writer: <c>decimal.maxValue</c> was a ReferenceError, and <c>TimeSpan.TicksPerSecond</c> a
    /// number the first long threw on. A const of the class's own source keeps its reference.
    /// </summary>
    [Fact]
    public void AConstant_IsWrittenAsItsValue_InItsType()
    {
        var ts = Transpile("""
            using static System.Decimal;
            namespace App;
            public class Prices
            {
                public const decimal Standard = 1.50m;
                public decimal Max() => decimal.MaxValue;
                public decimal Min() => MinValue;
                public long Ticks() => System.TimeSpan.TicksPerSecond;
                public ulong Top() => ulong.MaxValue;
                public decimal Qualified() => Prices.Standard;
                public decimal Bare() => Standard;
            }
            """);

        ts.Should().Contain("$eq.num.dec(\"79228162514264337593543950335\")");
        ts.Should().Contain("$eq.num.dec(\"-79228162514264337593543950335\")");
        ts.Should().Contain("return 10000000n;");
        ts.Should().Contain("return 18446744073709551615n;");
        ts.Should().Contain("return $eq.num.dec(\"1.50\");");
        ts.Should().Contain("return Prices.standard;");
        ts.Should().NotContain("decimal.maxValue").And.NotContain("Decimal.minValue");
    }

    /// <summary>A constant's own static is annotated in its JavaScript type: a narrow integer as a
    /// number and a ulong as a bigint, where each reached TypeScript verbatim, naming nothing there.</summary>
    [Fact]
    public void AConstantsStatic_IsAnnotatedInItsJavaScriptType()
    {
        var ts = Transpile("""
            namespace App;
            public class Limits
            {
                public const ulong Top = 18446744073709551615UL;
                public const byte Small = 7;
                public const ushort Port = 8080;
                public const sbyte Signed = -3;
                public ulong ReadTop() => Top;
                public int Sum() => Small + Port + Signed;
            }
            """);

        ts.Should().Contain("static get top(): bigint");
        ts.Should().Contain("static small: number = 7;");
        ts.Should().Contain("static port: number = 8080;");
        ts.Should().Contain("static signed: number = -3;");
        foreach (var raw in new[] { ": ulong", ": byte", ": ushort", ": sbyte" })
            ts.Should().NotContain(raw);
    }

    /// <summary>
    /// A parameter's default filled in for an argument a named one skips is the constant it is, in
    /// the parameter's type: a char was written with no quotes (a bare identifier), a string with a
    /// quote in it as a broken literal, a decimal and a long as numbers, and a float as its own text.
    /// </summary>
    [Fact]
    public void ASkippedParametersDefault_IsWrittenAsItsValue_InItsType()
    {
        var ts = Transpile("""
            namespace App;
            public class Labels
            {
                public static string Label(char c = 'x', string s = "it's", decimal d = 1.5m, long l = 5,
                    float f = 0.1f, int n = 0) => s;
                public string Call() => Label(n: 1);
            }
            """);

        ts.Should().Contain("Labels.label('x', 'it\\'s', $eq.num.dec(\"1.5\"), 5n, 0.10000000149011612, 1)");
    }

    [Fact]
    public void PackGlyphAccess_InlinesTheConstructor_AtTheUseSite()
    {
        var ts = Transpile("""
            using eQuantic.UI.Lucide;
            using eQuantic.UI.Primitives;
            namespace App;
            public class IconStrip
            {
                public IconGlyph Build() => LucideIcons.Camera;
            }
            """);

        // The pack member access becomes the glyph constructor — no `LucideIcons` reference survives.
        ts.Should().Contain("new IconGlyph('camera', 'M14.5 4h-5L7 7', 'stroke')");
        ts.Should().NotContain("LucideIcons");
    }

    [Fact]
    public void InlinedGlyph_ImportsIconGlyphFromTheRuntime()
    {
        var ts = Transpile("""
            using eQuantic.UI.Lucide;
            using eQuantic.UI.Primitives;
            namespace App;
            public class IconStrip
            {
                public IconGlyph Build() => LucideIcons.Heart;
            }
            """);

        // Heart uses the defaulted ctor (Fill, default viewBox) — style/viewBox omitted.
        ts.Should().Contain("new IconGlyph('heart', 'M12 21l-1-1')");
        ts.Should().Contain("import {");
        ts.Should().Contain("IconGlyph");
        ts.Should().Contain("@equantic/runtime");
    }

    [Fact]
    public void CuratedIconsEnum_IsNotInlined_ItRemainsTheEnumMemberString()
    {
        // A sanity fence: the strategy is scoped to IconGlyph fields, so a normal enum member access
        // elsewhere is untouched (it lowers via the enum strategy to its string).
        var ts = Transpile("""
            using eQuantic.UI.Primitives;
            namespace App;
            public class IconStrip
            {
                public IconGlyphStyle Build() => IconGlyphStyle.Stroke;
            }
            """);
        ts.Should().Contain("'stroke'");
        ts.Should().NotContain("new IconGlyph");
    }

    [Fact]
    public void APackShapedProperty_InlinesLikeAField()
    {
        // The regression this guards is silent and expensive: change the packs to properties so the
        // IL trimmer can drop what a native app never names, and the WEB stops inlining — every
        // glyph becomes a member access into a pack module that tree-shaking can no longer reduce.
        // The suite was green either way before this test existed, because the fixture used only
        // the field shape the packs had stopped shipping.
        var ts = Transpile("""
            using eQuantic.UI.Lucide;
            using eQuantic.UI.Primitives;

            namespace App;

            public class IconStrip : StatelessComponent
            {
                public override VisualNode Build(ComponentContext context) =>
                    new Icon(LucideIcons.Camera);
            }
            """);

        ts.Should().Contain("'camera'").And.Contain("M14.5 4h-5L7 7");
        ts.Should().NotContain("LucideIcons",
            "the construction lands at the use site, so the pack itself is never named");
    }
}
