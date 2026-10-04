using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A lowering that wrote its own JavaScript function around a C# expression broke it one of two ways
/// (#539): an <c>await</c> in the expression landed in an arrow that is not <c>async</c>, and the
/// module did not parse, or an expression C# evaluates once ran once per element of a callback. Each
/// site now takes the expression as an ARGUMENT, evaluated where C# evaluates it, or needs no function
/// at all: <c>checked</c>, a <c>throw</c> expression, <c>Trim</c>'s characters, and
/// <c>Enumerable.Range</c>'s and <c>Repeat</c>'s arguments.
/// </summary>
public class IntroducedFunctionConformanceTests
{
    /// <summary>Every site of the table with an awaited argument, which did not parse.</summary>
    [SkippableTheory]
    [InlineData("async Task<int> F() { await Task.Yield(); return 1; } return checked(await F() + 1);")] // 2
    [InlineData("async Task<string> M() { await Task.Yield(); return \"m\"; } string s = null; try { return s ?? throw new InvalidOperationException(await M()); } catch (InvalidOperationException e) { return e.Message; }")] // "m"
    [InlineData("async Task<string> M() { await Task.Yield(); return \"m\"; } string s = \"kept\"; return s ?? throw new InvalidOperationException(await M());")] // "kept"
    [InlineData("async Task<int> F() { await Task.Yield(); return -1; } int x = 5; try { return x < 0 ? x : throw new InvalidOperationException(\"n\" + await F()); } catch (InvalidOperationException e) { return e.Message; }")] // "n-1"
    [InlineData("async Task<char> C() { await Task.Yield(); return 'x'; } return \"xxaxx\".Trim(await C());")] // "a"
    [InlineData("async Task<char> C() { await Task.Yield(); return 'x'; } return \"xxaxx\".TrimStart(await C()) + \"|\" + \"xxaxx\".TrimEnd(await C());")] // "axx|xxa"
    [InlineData("async Task<int> F() { await Task.Yield(); return 1; } return string.Join(\",\", Enumerable.Range(await F(), 2));")] // "1,2"
    [InlineData("async Task<int> F() { await Task.Yield(); return 3; } return string.Join(\",\", Enumerable.Range(0, await F()));")] // "0,1,2"
    [InlineData("async Task<int> F() { await Task.Yield(); return 7; } return string.Join(\",\", Enumerable.Repeat(await F(), 2));")] // "7,7"
    public void AnAwaitedArgument_RunsInTheCallersFunction(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>Every site of the table with an argument whose evaluation is counted: C# evaluates it
    /// once, where it stands, and a <c>throw</c> expression's exception only where it throws.</summary>
    [SkippableTheory]
    [InlineData("var calls = 0; int Start() { calls++; return 1; } var r = string.Join(\",\", Enumerable.Range(Start(), 3)); return r + \":\" + calls;")] // "1,2,3:1"
    [InlineData("var calls = 0; int Count() { calls++; return 2; } var r = string.Join(\",\", Enumerable.Range(5, Count())); return r + \":\" + calls;")] // "5,6:1"
    [InlineData("var xs = Enumerable.Repeat(new List<int>(), 3).ToList(); return object.ReferenceEquals(xs[0], xs[1]) && object.ReferenceEquals(xs[1], xs[2]);")] // true
    [InlineData("var calls = 0; string Next() { calls++; return \"v\" + calls; } var r = string.Join(\",\", Enumerable.Repeat(Next(), 3)); return r + \":\" + calls;")] // "v1,v1,v1:1"
    [InlineData("var calls = 0; int Next() { calls++; return 41; } var r = checked(Next() + 1); return r + \":\" + calls;")] // "42:1"
    [InlineData("var calls = 0; char[] Chars() { calls++; return new[] { 'x', 'y' }; } var r = \"xyaxy\".Trim(Chars()); return r + \":\" + calls;")] // "a:1"
    [InlineData("var calls = 0; string Message() { calls++; return \"m\"; } string s = \"kept\"; var r = s ?? throw new InvalidOperationException(Message()); return r + \":\" + calls;")] // "kept:0"
    // The order C# evaluates in: the receiver before the characters, and named arguments as written.
    [InlineData("var log = \"\"; string Text() { log += \"t\"; return \"xax\"; } char X() { log += \"c\"; return 'x'; } var r = Text().Trim(X()); return r + \":\" + log;")] // "a:tc"
    [InlineData("var log = \"\"; int Count() { log += \"c\"; return 2; } int Start() { log += \"s\"; return 4; } var r = string.Join(\",\", Enumerable.Range(count: Count(), start: Start())); return r + \":\" + log;")] // "4,5:cs"
    public void AnArgumentEvaluatedOnce_IsEvaluatedOnce(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>What the sites answer otherwise, with the refusals .NET makes, which the runtime makes
    /// now: <c>Trim</c> of no characters is white space, and a negative count is out of range.</summary>
    [SkippableTheory]
    [InlineData("return \"  a  \".Trim(new char[0]) + \"|\" + \"xax\".Trim('x') + \"|\" + \"xyaxy\".Trim('x', 'y') + \"|\" + \" a \".Trim();")] // "a|a|a|a"
    [InlineData("char[] none = null; return \"\\u3000a\\u0085\".Trim(none);")] // "a"
    [InlineData("return string.Join(\",\", Enumerable.Range(-2, 3)) + \"|\" + Enumerable.Range(1, 0).Count() + \"|\" + string.Join(\",\", Enumerable.Repeat(\"z\", 2));")] // "-2,-1,0|0|z,z"
    [InlineData("try { Enumerable.Range(0, -1).ToList(); return \"none\"; } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("try { Enumerable.Range(int.MaxValue, 2).ToList(); return \"none\"; } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("try { Enumerable.Repeat(1, -1).ToList(); return \"none\"; } catch (ArgumentOutOfRangeException e) { return e.Message; }")]
    [InlineData("int m = int.MaxValue; try { return checked(m + 1); } catch (OverflowException e) { return e.Message; }")]
    [InlineData("int m = int.MaxValue; return unchecked(m + 1);")] // -2147483648
    public void TheSites_AnswerAsDotNetDoes(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
