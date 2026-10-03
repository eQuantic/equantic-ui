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

    private const string Shapes = """
        public record Point(int X, int Y);
        public record Line(Point From, Point To);
        public readonly struct Temperature
        {
            public double Celsius { get; }
            public Temperature(double celsius) { Celsius = celsius; }
            public void Deconstruct(out double celsius, out double fahrenheit)
            {
                celsius = Celsius;
                fahrenheit = Celsius * 9 / 5 + 32;
            }
        }
        """;

    /// <summary>
    /// Every shape a deconstruction takes goes through one lowering (#486): the declaration, the
    /// assignment, a declaration written as a tuple, a mixed one, and the loop, over a record, a
    /// struct whose own <c>Deconstruct</c> computes a part under a name no member has, a nested
    /// record, and a tuple and a dictionary's pairs as the controls.
    /// </summary>
    [SkippableTheory]
    [InlineData("(var a, var b) = new Point(1, 2); return a * 10 + b;")]                                   // 12
    [InlineData("int b; (var a, b) = new Point(3, 4); return a * 10 + b;")]                                // 34
    [InlineData("var s = 0; foreach (var (a, b) in new[] { new Point(1, 2), new Point(3, 4) }) s += a * 10 + b; return s;")] // 46
    [InlineData("var (c, f) = new Temperature(100); return c + f;")]                                       // 312
    [InlineData("double c, f; (c, f) = new Temperature(0); return c + f;")]                                 // 32
    [InlineData("var s = 0.0; foreach (var (c, f) in new[] { new Temperature(0), new Temperature(100) }) s += f; return s;")] // 244
    [InlineData("var ((x1, y1), (x2, y2)) = new Line(new Point(1, 2), new Point(3, 4)); return x1 + y1 * 10 + x2 * 100 + y2 * 1000;")] // 4321
    [InlineData("int a, b, n; ((a, b), n) = (new Point(5, 6), 7); return a * 100 + b * 10 + n;")]          // 567
    [InlineData("var (from, (_, y)) = new Line(new Point(1, 2), new Point(3, 4)); return from.X * 10 + y;")] // 14
    [InlineData("var (a, b) = (1, 2); return a * 10 + b;")]                                                // 12
    [InlineData("var d = new Dictionary<string, int> { [\"a\"] = 1 }; var s = \"\"; foreach (var (k, v) in d) s += k + v; return s;")] // "a1"
    public void EveryDeconstruction_ReadsARecordOrAStructByItsDeconstruct(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Shapes);
    }

    /// <summary>
    /// A nested level whose <c>Deconstruct</c> the app wrote is called too, in a declaration, an
    /// assignment and a loop: a pattern cannot call one, and it read the members its outs name,
    /// which a computed part is not. And an assignment's value is the tuple of its parts, as C#
    /// gives it, where the destructuring answered its right-hand side.
    /// </summary>
    [SkippableTheory]
    [InlineData("var ((c, f), n) = (new Temperature(100), 1); return c + f + n;")]                                     // 313
    [InlineData("double c, f; int n; ((c, f), n) = (new Temperature(0), 2); return c + f + n;")]                       // 34
    [InlineData("var s = 0.0; foreach (var ((c, f), n) in new[] { (new Temperature(0), 1), (new Temperature(100), 2) }) s += f * n; return s;")] // 456
    [InlineData("int a, b; var result = ((a, b) = new Point(1, 2)); return result.Item1 * 10 + result.Item2;")]        // 12
    [InlineData("int b; var r = ((_, b) = new Point(5, 6)); return r.Item1 + r.Item2;")]                               // 11
    public void ANestedDeconstructOfTheApps_IsCalled_AndAnAssignmentAnswersItsTuple(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Shapes);
    }
}
