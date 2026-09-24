using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// A local function's name through the emitter a plain class takes, where the conformance harness
/// cannot go: it emits records and structs, not classes. The runs themselves are in the
/// conformance suite (<c>LocalNameConformanceTests</c>).
/// </summary>
public class LocalFunctionNameTests
{
    [Fact]
    public void InAConstructor_ItDoesNotRedeclareTheParameterTheEmitterAdds()
    {
        // A constructor with no parameters of its own takes `props` in JavaScript, and `Props`
        // camel-cased is that name: `const props` beside the parameter did not parse.
        var ts = TestHelper.ConvertClass(
            "public int Value { get; set; } public Setup() { int Props() => 5; Value = Props(); }", "Setup");

        ts.Should().Contain("const props$ = ").And.Contain("this.value = props$()");
        ts.Should().NotContain("const props = ");
    }

    [Fact]
    public void ANameNothingHolds_KeepsItsCasing()
    {
        // The rename is for a collision only: a function whose cased name is free reads as authored.
        var ts = TestHelper.ConvertClass("public int Area(int side) { int Square(int n) => n * n; return Square(side); }");

        ts.Should().Contain("const square = ").And.Contain("return square(side)");
    }
}
