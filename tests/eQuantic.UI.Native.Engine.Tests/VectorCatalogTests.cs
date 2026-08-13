using eQuantic.UI.Build;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The generator that turns an app's <c>Assets/**/*.svg</c> into C#. It is the DOOR to everything
/// else here: without it a drawing is a library with no way to point at a file, and with it an app
/// writes <c>Vectors.Mark</c> — a compile error when it is wrong, where a path string would have
/// been a blank space at runtime.
/// </summary>
public class VectorCatalogTests
{
    private const string Mark = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 40">
          <rect width="120" height="40" fill="#4F46E5"/>
          <path d="M8 8h20v8H8z" fill="currentColor" fill-opacity="0.5"/>
        </svg>
        """;

    private static (string Source, int Count) Generate(params (string Name, string Svg)[] files)
    {
        var assets = Path.Combine(Path.GetTempPath(), "eq-vectors-" + Guid.NewGuid().ToString("n"));
        var output = Path.Combine(assets, "obj", "Vectors.g.cs");
        try
        {
            foreach (var (name, svg) in files)
            {
                var path = Path.Combine(assets, name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, svg);
            }
            Directory.CreateDirectory(assets);
            var count = VectorCatalog.Write(assets, output, "Sample.App");
            return (File.Exists(output) ? File.ReadAllText(output) : "", count);
        }
        finally
        {
            if (Directory.Exists(assets)) Directory.Delete(assets, recursive: true);
        }
    }

    [Fact]
    public void AFileBecomesATypedMemberCarryingItsShapes()
    {
        var (source, count) = Generate(("mark.svg", Mark));

        count.Should().Be(1);
        source.Should().Contain("public static readonly VectorDrawing Mark");
        source.Should().Contain("0f, 0f, 120f, 40f");
        source.Should().Contain("VectorPaint.Solid(Color.FromRgba(79, 70, 229, 255))");
        // The inherited paint keeps the fraction the file asked for — 255 × 0.5.
        source.Should().Contain("VectorPaintKind.Inherit, Color.FromRgba(0, 0, 0, 128)");
    }

    /// <summary>
    /// The catalog puts ITSELF in scope, and the directive goes FIRST. A `Using` item in the SDK
    /// would have done the same job and would have depended on this file existing before MSBuild
    /// evaluated the project — which on a COLD build it does not: the first build failed on
    /// `Vectors` not existing and the second passed, which reads as flaky and is not.
    /// </summary>
    [Fact]
    public void TheCatalogPutsItselfInScope()
    {
        var (source, _) = Generate(("mark.svg", Mark));

        var lines = source.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        var globalUsing = lines.FindIndex(line => line.StartsWith("global using"));
        var ordinaryUsing = lines.FindIndex(line => line.StartsWith("using "));
        globalUsing.Should().BeGreaterThan(-1);
        globalUsing.Should().BeLessThan(ordinaryUsing, "C# refuses a global using after an ordinary one");
        lines[globalUsing].Should().Be("global using Sample.App;");
    }

    /// <summary>Folders and dashes are how designers name files, and none of it is C#.</summary>
    [Theory]
    [InlineData("logo-dark.svg", "LogoDark")]
    [InlineData("brand/app_icon.svg", "BrandAppIcon")]
    [InlineData("2024/report.svg", "_2024Report")]
    public void AFilePathBecomesAMemberName(string file, string member)
    {
        var (source, _) = Generate((file, Mark));

        source.Should().Contain($"VectorDrawing {member} =");
    }

    /// <summary>
    /// A file this subset cannot carry is SKIPPED, not emitted empty: a member that draws nothing
    /// is a compile-time promise the screen does not keep. The others still generate.
    /// </summary>
    [Fact]
    public void AFileTheSubsetCannotCarryIsLeftOut()
    {
        var (source, count) = Generate(
            ("mark.svg", Mark),
            ("gradient.svg", """
                <svg viewBox="0 0 10 10">
                  <defs><linearGradient id="g"/></defs>
                  <rect width="10" height="10" fill="url(#g)"/>
                </svg>
                """));

        count.Should().Be(1);
        source.Should().Contain("Mark").And.NotContain("Gradient");
    }

    /// <summary>No artwork, no file — an app without an Assets folder gets no empty class to
    /// wonder about, and no global using naming a namespace nothing declares.</summary>
    [Fact]
    public void NoArtworkWritesNothingAtAll()
    {
        var (source, count) = Generate();

        count.Should().Be(0);
        source.Should().BeEmpty();
    }
}
