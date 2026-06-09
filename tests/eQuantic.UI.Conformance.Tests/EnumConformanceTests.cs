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
}
