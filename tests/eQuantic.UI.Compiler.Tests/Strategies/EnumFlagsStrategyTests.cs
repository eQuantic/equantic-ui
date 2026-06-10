using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// A `[Flags]` enum is represented NUMERICALLY (members emit their underlying value) so bitwise
/// combination / masking / <c>HasFlag</c> behave like .NET — the member-name string representation used
/// for normal enums cannot express <c>Read | Write</c>. A non-flags enum in the same file stays a string.
/// </summary>
public class EnumFlagsStrategyTests
{
    private const string Header =
        "using System; using eQuantic.UI.Core; using eQuantic.UI.Components; namespace App; " +
        "[Flags] public enum Perm { None = 0, Read = 1, Write = 2, Exec = 4 } " +
        "public enum Color { Red, Green, Blue } ";

    private static string Ts(string buildBody) =>
        new ComponentCompiler().CompileSource(
            Header +
            "public class C : StatelessComponent { public Perm P { get; set; } " +
            "public override IComponent Build(RenderContext ctx) { " + buildBody + " return new Box(); } }")
        .Single(r => r.ComponentName == "C").TypeScript;

    [Fact]
    public void FlagsMembers_EmitNumericValues()
    {
        var ts = Ts("var combo = Perm.Read | Perm.Write | Perm.Exec;");
        ts.Should().Contain("1 | 2 | 4");
        ts.Should().NotContain("'read'");
        ts.Should().NotContain("'write'");
    }

    [Fact]
    public void HasFlag_BecomesBitwiseTest()
    {
        var ts = Ts("bool hf = P.HasFlag(Perm.Read);");
        ts.Should().Contain("(this.p & 1) === 1");
        ts.Should().NotContain("hasFlag");
    }

    [Fact]
    public void Mask_AndCompare_AreNumeric()
    {
        var ts = Ts("bool any = (P & Perm.Write) != 0;");
        ts.Should().Contain("(this.p & 2) !== 0");
    }

    [Fact]
    public void FlagsCasts_AreNumericIdentity()
    {
        var ts = Ts("int n = (int)P; Perm back = (Perm)3;");
        ts.Should().Contain("let n = this.p");      // (int)flags -> identity (already numeric)
        ts.Should().Contain("let back = 3");        // (Perm)int -> stays numeric
    }

    [Fact]
    public void NonFlagsEnum_StaysString()
    {
        var ts = Ts("var c = Color.Blue; bool g = c == Color.Green;");
        ts.Should().Contain("'blue'");
        ts.Should().Contain("=== 'green'");
    }
}
