using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A value whose static type does not say what it holds, an interface or a type parameter, writes its
/// text as the value does, in the culture in force, as one held as <c>object</c> does: JavaScript's own
/// string is invariant, so a pt-BR page read <c>1.5</c> where .NET writes <c>1,5</c> (#965).
/// </summary>
public class HeldValueTextConformanceTests
{
    private const string Types = """
        public record Tagged(string Name) : IComparable { public int CompareTo(object other) => 0; public override string ToString() => "tag " + Name; }
        """;

    [SkippableTheory]
    // The rows of #965.
    [InlineData("IFormattable x = 1.5; return $\"{x}\";")]
    [InlineData("string Show<T>(T v) => $\"{v}\"; return Show(1.5);")]
    [InlineData("IFormattable d = new DateTime(2024, 1, 15, 21, 30, 5); return $\"{d}\";")]
    [InlineData("IComparable c = 2.5; return \"v=\" + c;")]
    [InlineData("string Join<T>(T v) => \"v=\" + v; return Join(2.5);")]
    [InlineData("IFormattable x = 1.5; return x.ToString();")]
    // A negative integer, a decimal and a float, each by its own text.
    [InlineData("string Show<T>(T v) => $\"{v}\"; return Show(-5) + \"|\" + Show(2.5m) + \"|\" + Show(1.25f);")]
    // A value with nothing to do with the culture still writes .NET's text.
    [InlineData("string Show<T>(T v) => \"v=\" + v; return Show(true) + \"|\" + Show(\"a\") + \"|\" + Show('c');")]
    [InlineData("string Show<T>(T v) where T : class => \"v=\" + v; string none = null; return Show(none);")]
    // A type of the app's behind the interface reaches its own ToString.
    [InlineData("IComparable c = new Tagged(\"a\"); return \"v=\" + c + \"|\" + $\"{c}\" + \"|\" + c.ToString();")]
    public void AValueHeldAsAnInterfaceOrATypeParameter_WritesItsText(string statements)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(statements, Types, "pt-BR");
    }
}
