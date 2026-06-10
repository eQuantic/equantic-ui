using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// Conformance for enums (regression for #13). Enum members compile to their member-name string,
/// so equality/switch/ternary behave identically to .NET. (enum.ToString() is intentionally not
/// asserted: the transpiler emits a camelCase name while .NET returns the PascalCase name — a
/// documented representation choice, not a conformance target.)
/// </summary>
public class EnumConformanceTests
{
    private const string Prelude = "enum Status { Active, Pending, Inactive }";

    [SkippableTheory]
    [InlineData("Status.Active == Status.Active")]
    [InlineData("Status.Active == Status.Pending")]
    [InlineData("Status.Active != Status.Pending")]
    [InlineData("Status.Active == Status.Active ? \"yes\" : \"no\"")]
    [InlineData("Status.Pending switch { Status.Active => 1, Status.Pending => 2, _ => 0 }")]
    [InlineData("Status.Inactive switch { Status.Active => 1, Status.Pending => 2, _ => 0 }")]
    public void Enums_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, Prelude);
    }

    /// <summary>
    /// Numeric casts: the transpiler bridges the string representation with the enum's compile-time
    /// name↔value table, so `(int)enum` yields the real underlying value and `(EnumType)int` yields a
    /// member comparable to other members. These produce primitives (int/bool), so they conform exactly.
    /// </summary>
    [SkippableTheory]
    [InlineData("(int)Status.Active")]     // -> 0
    [InlineData("(int)Status.Pending")]    // -> 1
    [InlineData("(int)Status.Inactive")]   // -> 2
    [InlineData("(Status)1 == Status.Pending")]    // int -> enum, compared by member -> true
    [InlineData("(Status)2 == Status.Pending")]    // -> false
    [InlineData("(int)(Status)2")]                 // round-trip -> 2
    public void EnumCasts_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression, Prelude);
    }
}
