using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for deconstruction: <c>var (a, b) = …</c> over value tuples (array destructuring,
/// holes for discards) and positional records (mapped to the type's Deconstruct element order).
/// </summary>
public class DeconstructionConformanceTests
{
    [SkippableTheory]
    // Tuple deconstruction (arrays).
    [InlineData("var (a, b) = (1, 2); return a + b;")]                       // 3
    [InlineData("var (a, b, c) = (1, 2, 3); return a * 100 + b * 10 + c;")]  // 123
    [InlineData("var (_, y) = (5, 7); return y;")]                           // 7 (discard keeps position)
    [InlineData("var (x, _) = (5, 7); return x;")]                           // 5
    [InlineData("(int a, int b) t = (4, 9); var (x, y) = t; return x * y;")] // 36
    public void TupleDeconstruction_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }

    [SkippableTheory]
    // Positional record deconstruction (objects -> mapped by Deconstruct order).
    [InlineData("var (x, y) = new Point(3, 4); return x + y;")]                  // 7
    [InlineData("var (a, b) = new Point(3, 4); return a * 10 + b;")]             // 34 (names are positional)
    [InlineData("Point p = new Point(8, 9); var (a, b) = p; return a - b;")]     // -1
    [InlineData("var (_, b) = new Point(1, 9); return b;")]                      // 9 (discard)
    [InlineData("var (a, _) = new Point(1, 9); return a;")]                      // 1
    public void RecordDeconstruction_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, "public record Point(int X, int Y);");
    }

    /// <summary>A record deconstructed into variables that already exist: it was array destructuring,
    /// which a record is not, and threw (#486).</summary>
    [SkippableTheory]
    [InlineData("int a, b; (a, b) = new Point(1, 2); return a * 10 + b;")]            // 12
    [InlineData("int a = 0; (a, _) = new Point(5, 6); return a;")]                   // 5
    [InlineData("var xs = new int[2]; (xs[0], xs[1]) = new Point(3, 4); return xs[0] + xs[1];")] // 7
    public void ARecordDeconstructedByAssignment_FillsItsTargets(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, "public record Point(int X, int Y);");
    }
}
