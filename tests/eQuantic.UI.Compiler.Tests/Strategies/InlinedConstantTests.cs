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
            public static readonly IconGlyph Camera = new("camera", "M14.5 4h-5L7 7", IconGlyphStyle.Stroke);
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
}
