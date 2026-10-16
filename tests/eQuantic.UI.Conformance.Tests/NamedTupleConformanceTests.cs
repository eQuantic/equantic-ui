using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for value-tuple element access: tuples are emitted as arrays, and elements are reached
/// by the positional accessor (<c>Item1</c>) or by declared name (<c>(int X, int Y)</c> → <c>.X</c>).
/// </summary>
public class NamedTupleConformanceTests
{
    [SkippableTheory]
    // Positional accessor on an inline tuple
    [InlineData("(1, 2).Item1")]                 // 1
    [InlineData("(1, 2).Item2")]                 // 2
    [InlineData("(10, 20, 30).Item3")]           // 30
    public void TupleItemAccess_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }

    [SkippableTheory]
    // Named element access via a typed local.
    [InlineData("(int X, int Y) p = (3, 4); return p.X + p.Y;")]                        // 7
    [InlineData("(int X, int Y) p = (3, 4); return p.X * 10 + p.Y;")]                    // 34
    // Named + positional accessors refer to the same slots.
    [InlineData("(int X, int Y) p = (3, 4); return p.X == p.Item1 && p.Y == p.Item2;")] // true
    // Named tuple returned from a local function, accessed by name.
    [InlineData("(string Name, int Age) Make() => (\"Ann\", 30); var p = Make(); return p.Name + \":\" + p.Age;")] // "Ann:30"
    // Mutating a named element then reading it back.
    [InlineData("(int X, int Y) p = (1, 1); p.Y = 9; return p.X + p.Y;")]               // 10
    // Named tuples compare structurally (by value).
    [InlineData("(int X, int Y) a = (1, 2); (int X, int Y) b = (1, 2); return a == b;")] // true
    public void NamedTupleStatements_MatchDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    /// <summary>A nullable tuple's element read through <c>?.</c>: by name it was <c>t?.a</c>, undefined
    /// on the array, and positionally a name nothing defines (#847).</summary>
    [SkippableTheory]
    [InlineData("(int A, int B)? t = (1, 2); return t?.A ?? -1;")]                          // 1
    [InlineData("(int A, int B)? t = (1, 2); return t?.Item2 ?? -1;")]                      // 2
    [InlineData("(int A, int B)? t = null; return t?.B ?? -1;")]                            // -1
    [InlineData("(string Name, int Age)? p = (\"Ann\", 30); return p?.Name + \":\" + p?.Age;")] // "Ann:30"
    public void ANullableTuplesElement_ReadThroughTheGuard_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
