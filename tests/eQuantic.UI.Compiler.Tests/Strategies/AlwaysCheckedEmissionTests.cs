using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// What the members that check overflow whatever the context are written as (#1808), each beside the real
/// that pays nothing for it. The conformance suite (<c>AlwaysCheckedConformanceTests</c>) runs them on both
/// sides; these pin the shape: an int's and a long's sum add through the runtime's checked addition, which
/// the bound overload's value type selects, a nullable's null is the zero of its type, and a signed
/// integer's <c>Abs</c> and <c>CopySign</c> go through the runtime, which throws for its MinValue.
/// </summary>
public class AlwaysCheckedEmissionTests
{
    [Theory]
    // The type is the bound overload's: the selector's result, or the source's element.
    [InlineData("list.Sum(x => x.Value)", "this.list.reduce(($sum, x) => $eq.num.addInt($sum, x.value), 0)")]
    [InlineData("list.Sum(x => (long)x.Value)", "this.list.reduce(($sum, x) => $eq.num.addLong($sum, $eq.num.long(x.value)), 0n)")]
    // A nullable's null leaves the total as it was.
    [InlineData("list.Sum(x => (int?)x.Value)", "this.list.reduce(($sum, x) => $eq.num.addInt($sum, x.value ?? 0), 0)")]
    [InlineData("list.Sum(x => (long?)x.Value)", "this.list.reduce(($sum, x) => $eq.num.addLong($sum, $eq.num.long(x.value) ?? 0n), 0n)")]
    // A real adds with `+`, as it did: nothing to check, and no call per element.
    [InlineData("list.Sum(x => x.Amount)", "this.list.reduce(($sum, x) => $sum + x.amount, 0)")]
    [InlineData("list.Sum(x => (double?)x.Amount)", "this.list.reduce(($sum, x) => $sum + (x.amount ?? 0), 0)")]
    public void ASum_AddsByTheTypeOfItsValues(string code, string expected) =>
        TestHelper.ConvertExpression(code).Should().Be(expected);

    [Theory]
    [InlineData("Math.Abs(Value)", "$eq.num.abs(this.value, 32)")]
    [InlineData("int.Abs(Value)", "$eq.num.abs(this.value, 32)")]
    [InlineData("Math.Abs((short)Value)", "$eq.num.abs(")]
    [InlineData("Math.Abs((long)Value)", "$eq.num.absLong($eq.num.long(this.value))")]
    [InlineData("int.CopySign(Value, -1)", "$eq.num.copySign(this.value, -1, 32)")]
    [InlineData("long.CopySign((long)Value, 1L)", "$eq.num.copySignLong($eq.num.long(this.value), 1n)")]
    public void ASignedIntegersMagnitude_IsTheRuntimes(string code, string expected) =>
        TestHelper.ConvertExpression(code).Should().StartWith(expected);

    [Theory]
    [InlineData("Math.Abs(Amount)")]
    [InlineData("double.Abs(Amount)")]
    [InlineData("double.CopySign(Amount, -1.0)")]
    public void ARealsMagnitude_StaysMathAbs(string code) =>
        TestHelper.ConvertExpression(code).Should().Contain("Math.abs(").And.NotContain("$eq.num");
}
