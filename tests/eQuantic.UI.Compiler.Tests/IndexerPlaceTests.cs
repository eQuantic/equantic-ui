using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests;

/// <summary>
/// An entry of an indexer a twin carries is a place every writer reads and writes through a template
/// over its receiver and keys (<c>CodeGen.Strategies.Place</c>), and a template's holes are single
/// digits. A place that would take more parts than a template holds is refused at the access, rather
/// than written with a hole no template fills, which left <c>{10}</c> in the module and the module
/// unparseable.
/// </summary>
public class IndexerPlaceTests
{
    private static CompilationResult Compile(string source, string name) =>
        new ComponentCompiler().CompileSource(source, "Probe.cs").Single(result => result.ComponentName == name);

    [Fact]
    public void AnAccessWithMoreKeysThanATemplateHolds_IsRefused()
    {
        var result = Compile("""
            public sealed class Wide
            {
                public int this[int a, int b, int c, int d, int e, int f, int g, int h, int i] { get => a + i; set { } }
                public int Sum() { this[1, 2, 3, 4, 5, 6, 7, 8, 9] += 1; return this[1, 2, 3, 4, 5, 6, 7, 8, 9]; }
            }
            """, "Wide");

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Code == "EQ1004" && error.Message.Contains("passes 9 keys"));
        result.TypeScript.Should().NotContain("{10}");
    }

    [Fact]
    public void AnAccessWithEightKeys_IsWritten()
    {
        var result = Compile("""
            public sealed class Wide
            {
                public int this[int a, int b, int c, int d, int e, int f, int g, int h] { get => a + h; set { } }
                public int Sum() { this[1, 2, 3, 4, 5, 6, 7, 8] += 1; return this[1, 2, 3, 4, 5, 6, 7, 8]; }
            }
            """, "Wide");

        result.Errors.Should().BeEmpty();
        result.TypeScript.Should().Contain("this.setItem(1, 2, 3, 4, 5, 6, 7, 8, this.item(1, 2, 3, 4, 5, 6, 7, 8) + 1)");
    }
}
