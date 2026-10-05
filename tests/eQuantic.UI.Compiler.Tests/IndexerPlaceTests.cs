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

    /// <summary>
    /// A <c>^n</c> that is no array's, list's or string's index, nor a from-the-end key over a type that
    /// counts its elements, is a System.Index VALUE, which has no JavaScript translation: a
    /// <c>this[Index]</c> key, a stored index. It is refused where it was written as a call to
    /// <c>__INDEX_FROM_END__</c>, which nothing defines, and the access to a <c>this[Index]</c> goes
    /// through the indexer, where it read the length of a twin that has none.
    /// </summary>
    [Fact]
    public void AnIndexValue_IsRefused()
    {
        var result = Compile("""
            public sealed class Indexed
            {
                public int this[System.Index i] { get => 0; set { } }
                public int Last() { this[^1] = 2; System.Index i = ^2; return this[^1]; }
            }
            """, "Indexed");

        result.Success.Should().BeFalse();
        result.Errors.Where(error => error.Code == "EQ1004").Should().HaveCount(3);
        result.TypeScript.Should().NotContain("__INDEX_FROM_END__").And.NotContain(".length");
    }

    /// <summary>
    /// C# 14's instance <c>void operator ++()</c> steps the value in place, which no twin carries: the
    /// step is refused, where it was written as JavaScript's own step of an object, NaN.
    /// </summary>
    [Fact]
    public void AnInstanceStepOperator_IsRefused()
    {
        var result = Compile("""
            public sealed class Box
            {
                public int V;
                public void operator ++() { V++; }
                public static int Run() { var b = new Box(); b++; return b.V; }
            }
            """, "Box");

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Code == "EQ1004" && error.Message.Contains("user-defined step"));
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

    /// <summary>
    /// A setter's own <c>return</c> runs its body in an arrow of its own, so it ends the setter and
    /// not its answer; a <c>return</c> of a lambda or a local function inside it ends that function
    /// alone, and the setter stays a plain body, where it paid for the arrow too.
    /// </summary>
    [Fact]
    public void ASetterRunsInAnArrow_OnlyForAReturnOfItsOwn()
    {
        var hooks = Compile("""
            public sealed class Hooks
            {
                private readonly System.Func<int, int>[] _f = new System.Func<int, int>[2];
                public int Last;
                public int this[int i]
                {
                    get => _f[i](i);
                    set { _f[i] = x => { return x + 1; }; int Local() { return 2; } Last = Local(); }
                }
            }
            """, "Hooks");
        var guarded = Compile("""
            public sealed class Guarded
            {
                public int Last;
                public int this[int i]
                {
                    get => Last;
                    set { if (value < 0) return; Last = value; }
                }
            }
            """, "Guarded");

        hooks.Errors.Should().BeEmpty();
        hooks.TypeScript.Should().NotContain("(() => {", "the setter's only returns are its lambda's and its local function's");
        guarded.TypeScript.Should().Contain("(() => {", "the setter that returns on its own runs in an arrow");
    }
}
