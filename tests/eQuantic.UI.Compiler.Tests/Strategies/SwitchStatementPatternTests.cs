using System.Linq;
using eQuantic.UI.Compiler;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// A `switch` STATEMENT that uses pattern labels (`case Type t:`, `case { … }:`, `case … when …:`) has no
/// JS equivalent, so it is rewritten to an if/else chain (shared <see cref="PatternConverter"/> — same
/// condition/binding logic the conformance-validated switch EXPRESSION uses): value bound once to `_s`,
/// pattern bindings hoisted and assigned inside the arm condition (so a `when` can see them and a failing
/// `when` falls through), `default` → trailing `else`. A constant-only switch keeps the native `switch`.
/// </summary>
public class SwitchStatementPatternTests
{
    private readonly ITestOutputHelper _out;
    public SwitchStatementPatternTests(ITestOutputHelper o) => _out = o;

    private const string Header =
        "using System; using eQuantic.UI.Core; using eQuantic.UI.Components; namespace App; ";

    private string Build(string method) =>
        new ComponentCompiler().CompileSource(
            Header + "public record Pt(int X, int Y); public class C : StatelessComponent { " + method +
            " public override IComponent Build(RenderContext c) => new Box(); }")
        .Single(r => r.ComponentName == "C").TypeScript;

    [Fact]
    public void TypeAndWhenPattern_RewritesToIfChain_WithHoistedBindingInScopeForWhen()
    {
        var ts = Build("public string M(object o) { switch (o) { " +
                        "case int n when n > 0: return \"pos\" + n; case string s: return s; default: return \"?\"; } }");
        _out.WriteLine(ts);
        ts.Should().Contain("const _s = o;");
        ts.Should().Contain("let n");                                   // bindings hoisted for the chain
        // when-clause assigns the binding in the condition, so `n` is in scope for `n > 0`.
        ts.Should().Contain("typeof _s === 'number' && (n = _s, n > 0)");
        ts.Should().Contain("else if");
        ts.Should().Contain("typeof _s === 'string'");
        ts.Should().Contain("else { return '?'; }");
        ts.Should().NotContain("switch (o)");                           // not a native switch
    }

    [Fact]
    public void PropertyPattern_InSwitchStatement_UsesMemberAccess()
    {
        var ts = Build("public string M(Pt p) { switch (p) { " +
                       "case { X: 0, Y: var y }: return \"y\" + y; default: return \"?\"; } }");
        ts.Should().Contain("_s.x === 0");
        ts.Should().Contain("y = _s.y");                                // nested var bound from the property
    }

    [Fact]
    public void PositionalPattern_OverRecord_UsesDeconstructNames_NotIndex()
    {
        var ts = Build("public int M(Pt p) { switch (p) { " +
                       "case (0, var b): return b; default: return -1; } }");
        ts.Should().Contain("_s.x === 0");                              // position 0 -> X -> .x
        ts.Should().Contain("b = _s.y");                                // position 1 -> Y -> .y
        ts.Should().NotContain("_s[0]");                                // never index access on a record
    }

    [Fact]
    public void ConstantOnlySwitch_KeepsNativeSwitch()
    {
        var ts = Build("public string M(int x) { switch (x) { case 1: return \"a\"; case 2: return \"b\"; default: return \"c\"; } }");
        ts.Should().Contain("switch (x) {");
        ts.Should().Contain("case 1:");
    }
}
