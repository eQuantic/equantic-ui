using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for <c>Nullable&lt;T&gt;</c> (<c>T?</c>): <c>HasValue</c>/<c>Value</c>,
/// <c>GetValueOrDefault()</c> (type-aware default) and <c>GetValueOrDefault(fallback)</c>,
/// null-coalescing, and the lifted operators (<c>== != &lt; &gt; &lt;= &gt;=</c> and arithmetic,
/// which propagate <c>null</c> exactly as .NET does — a place where naive JS silently diverges,
/// e.g. <c>null &lt; 5</c> is <c>false</c> in C# but <c>true</c> in JS).
/// </summary>
public class NullableConformanceTests
{
    [SkippableTheory]
    // HasValue / Value
    [InlineData("((int?)5).HasValue")]                 // true
    [InlineData("((int?)null).HasValue")]              // false
    [InlineData("((int?)5).Value")]                    // 5
    // GetValueOrDefault() — type-aware default
    [InlineData("((int?)5).GetValueOrDefault()")]      // 5
    [InlineData("((int?)null).GetValueOrDefault()")]   // 0
    [InlineData("((double?)null).GetValueOrDefault()")] // 0
    [InlineData("((bool?)null).GetValueOrDefault()")]  // false
    [InlineData("((bool?)true).GetValueOrDefault()")]  // true
    // GetValueOrDefault(fallback)
    [InlineData("((int?)null).GetValueOrDefault(42)")] // 42
    [InlineData("((int?)5).GetValueOrDefault(42)")]    // 5
    [InlineData("((double?)null).GetValueOrDefault(1.5)")] // 1.5
    // Null-coalescing on T?
    [InlineData("((int?)null) ?? 7")]                  // 7
    [InlineData("((int?)5) ?? 7")]                     // 5
    // Lifted equality / inequality
    [InlineData("((int?)null) == ((int?)null)")]       // true
    [InlineData("((int?)5) == ((int?)null)")]          // false
    [InlineData("((int?)5) == ((int?)5)")]             // true
    [InlineData("((int?)5) != ((int?)null)")]          // true
    // Lifted relational — null operand makes the comparison false (NOT a numeric coercion)
    [InlineData("((int?)null) < 5")]                   // false  (JS naive: null<5 -> true)
    [InlineData("((int?)3) < 5")]                      // true
    [InlineData("5 > ((int?)null)")]                   // false
    [InlineData("((int?)3) <= ((int?)3)")]             // true
    [InlineData("((int?)null) >= ((int?)null)")]       // false
    // Lifted arithmetic — null if either operand is null
    [InlineData("((int?)null) + 5")]                   // null   (JS naive: null+5 -> 5)
    [InlineData("((int?)3) + 5")]                      // 8
    [InlineData("((int?)3) * ((int?)4)")]              // 12
    [InlineData("((int?)null) * ((int?)4)")]           // null
    public void Nullable_MatchesDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }

    [SkippableTheory]
    // Realistic statement-level usage with declared locals.
    [InlineData("int? a = 5; int? b = null; return a.GetValueOrDefault() + b.GetValueOrDefault(10);")] // 15
    [InlineData("int? a = null; return a.HasValue ? a.Value : -1;")]                                    // -1
    [InlineData("decimal? d = null; return d.GetValueOrDefault(2.5m).ToString();")]                     // "2.5"
    [InlineData("long? n = null; return n.GetValueOrDefault().ToString();")]                            // "0"
    public void NullableStatements_MatchesDotNet(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements);
    }
}
