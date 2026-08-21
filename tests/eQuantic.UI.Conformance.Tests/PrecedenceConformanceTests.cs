using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Operator composition executed on both sides. The headline case is <c>a ?? b &amp;&amp; c</c>:
/// C# needs no parentheses (<c>&amp;&amp;</c> binds tighter than <c>??</c>) and JavaScript REFUSES
/// the bare mix, so the emitted file used to fail to parse — every one of these would have thrown
/// a SyntaxError before the expression IR put the parentheses in.
/// </summary>
public class PrecedenceConformanceTests
{
    [SkippableTheory]
    // ?? beside && / || — unparenthesized in C#, a JS SyntaxError without fencing
    [InlineData("bool? a = null; bool b = true; return a ?? b && b;")]                     // true
    [InlineData("bool? a = false; bool b = true; return a ?? b || b;")]                    // false
    [InlineData("bool? a = null; bool b = false; return a ?? b && true;")]                 // false
    [InlineData("bool? a = null; bool b = false; return a ?? b || true;")]                 // true
    [InlineData("bool? a = true; return a ?? false && false;")]                            // true
    // the same mix reached through the author's own parentheses
    [InlineData("bool? a = null; return (a ?? false) && true;")]                           // false
    [InlineData("bool? a = null; return true && (a ?? true);")]                            // true
    // ?? chains and ?? beside a ternary
    [InlineData("int? a = null; int? b = null; return a ?? b ?? 7;")]                       // 7
    [InlineData("int? a = null; return (a ?? 3) * 2;")]                                     // 6
    [InlineData("int? a = null; return (a ?? 3) > 2 ? 10 : 20;")]                           // 10
    [InlineData("int? a = null; return a ?? (3 > 2 ? 10 : 20);")]                           // 10
    [InlineData("string s = null; return (s ?? \"x\") + \"y\";")]                           // xy
    // ??= beside a logical operator
    [InlineData("int? a = null; var r = (a ??= 5) > 1 && true; return r;")]                 // true
    // ordinary regrouping the writer must not disturb
    [InlineData("return 2 + 3 * 4;")]                                                        // 14
    [InlineData("return (2 + 3) * 4;")]                                                      // 20
    [InlineData("return 20 - 5 - 3;")]                                                        // 12
    [InlineData("return 20 - (5 - 3);")]                                                      // 18
    [InlineData("return -(2 + 3) * 4;")]                                                      // -20
    [InlineData("bool b = true; return (b ? 1 : 2) * 3;")]                                    // 3
    [InlineData("int x = 5; return -(-x);")]                                                  // 5
    [InlineData("return 100 / (2 + 3);")]                                                     // 20
    public void OperatorComposition_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
