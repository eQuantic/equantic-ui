using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// C#'s NON-short-circuit logical operators on <c>bool</c> — <c>|</c>, <c>&amp;</c>, <c>^</c> and
/// their compound forms — executed on both sides.
/// <para>
/// JavaScript has no operator that is both things these are. Its <c>|</c>/<c>&amp;</c> evaluate both
/// operands and answer a NUMBER, so a bool built with them compared unequal to <c>true</c> under the
/// strict equality eqc emits; its <c>||</c>/<c>&amp;&amp;</c> answer a bool and SKIP the right side,
/// so a call there would stop running. Found when the code editor's engine became transpiled code:
/// <c>typed |= Type(c)</c> stored <c>1</c> in a bool and tsc refused the module.
/// </para>
/// </summary>
public class BoolLogicConformanceTests
{
    [SkippableTheory]
    // BOTH sides run, every time — the half `||` would skip.
    [InlineData("int calls = 0; bool Take() { calls++; return true; } bool typed = false; foreach (var c in \"abc\") typed |= Take(); return (typed ? \"t\" : \"f\") + calls;")] // "t3"
    [InlineData("int calls = 0; bool Take() { calls++; return false; } bool all = true; for (int i = 0; i < 4; i++) all &= Take(); return (all ? \"t\" : \"f\") + calls;")] // "f4"
    [InlineData("int calls = 0; bool Take() { calls++; return true; } bool r = true | Take(); return (r ? \"t\" : \"f\") + calls;")] // "t1"
    [InlineData("int calls = 0; bool Take() { calls++; return true; } bool r = false & Take(); return (r ? \"t\" : \"f\") + calls;")] // "f1"
    // The answer is a BOOL — one that equals `true`, not a number that is merely truthy.
    [InlineData("bool r = true | false; return r == true ? \"bool\" : \"number\";")] // "bool"
    [InlineData("bool r = true & true; return r == true ? \"bool\" : \"number\";")] // "bool"
    [InlineData("bool a = true, b = false; return (a ^ b) == true ? \"bool\" : \"number\";")] // "bool"
    // Compound forms write a bool back.
    [InlineData("bool a = true; a &= false; a |= true; a ^= true; return a ? 1 : 0;")] // 0
    [InlineData("bool a = false; a ^= true; return a == true ? \"on\" : \"off\";")] // "on"
    public void BoolLogic_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine (embedded Bun or Node) available on this machine.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}

/// <summary>
/// The compound forms on a TARGET with a receiver: a dictionary entry, an array element, a property
/// reached through a call. C# evaluates the target once — the receiver, then the read, then the
/// right side — and the translation has to as well, or `GetState().Flag |= Next()` calls GetState()
/// twice. Found in review of the fix above: the dictionary path returned before the bool rule ever
/// ran, and stored the number anyway.
/// </summary>
public class BoolLogicCompoundTargetConformanceTests
{
    private const string Holder = "public record class Holder { public bool Flag { get; set; } }";

    [SkippableTheory]
    [InlineData("var flags = new Dictionary<string, bool> { [\"a\"] = false }; flags[\"a\"] |= true; return flags[\"a\"] == true ? \"bool\" : \"number\";")] // "bool"
    [InlineData("var flags = new Dictionary<string, bool> { [\"a\"] = true }; flags[\"a\"] &= false; flags[\"a\"] ^= true; return flags[\"a\"] ? 1 : 0;")] // 1
    [InlineData("int calls = 0; var box = new[] { false }; bool[] Get() { calls++; return box; } Get()[0] |= true; return (box[0] == true ? \"t\" : \"f\") + calls;")] // "t1"
    [InlineData("int calls = 0; var h = new Holder(); Holder Get() { calls++; return h; } Get().Flag |= true; return (h.Flag == true ? \"t\" : \"f\") + calls;")] // "t1"
    [InlineData("int calls = 0; var h = new Holder { Flag = true }; Holder Get() { calls++; return h; } Get().Flag ^= true; return (h.Flag ? \"t\" : \"f\") + calls;")] // "f1"
    public void ACompoundOnATargetEvaluatesItOnce(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine (embedded Bun or Node) available on this machine.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Holder);
    }
}
