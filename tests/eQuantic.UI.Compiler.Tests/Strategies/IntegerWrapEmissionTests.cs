using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// The shapes an int's and a long's arithmetic takes now that it wraps in the default context, as C#
/// wraps it (#1807). The conformance suite proves the values on both sides; these pin what the module
/// reads like, so a change that adds a call to every sum, or drops the wrap from one operator, shows here.
/// </summary>
public class IntegerWrapEmissionTests
{
    [Theory]
    [InlineData("int a = 1; int b = 2; var r = a + b;", "let r = a + b | 0;")]
    [InlineData("int a = 1; int b = 2; var r = a - b;", "let r = a - b | 0;")]
    // A product past 2^53 has lost its low bits as a double: Math.imul keeps them.
    [InlineData("int a = 1; int b = 2; var r = a * b;", "let r = Math.imul(a, b);")]
    // JavaScript's `<<` already answers in signed 32 bits, as C# does.
    [InlineData("int a = 1; var r = a << 3;", "let r = a << 3;")]
    [InlineData("int a = 1; var r = -a;", "let r = -a | 0;")]
    [InlineData("long a = 1; long b = 2; var r = a + b;", "let r = BigInt.asIntN(64, a + b);")]
    // A constant C#'s compiler folded is in range, or it would not have compiled (CS0220).
    [InlineData("var r = -5;", "let r = -5;")]
    [InlineData("const int k = 1 << 30; var r = k - 1 + k;", "let r = k - 1 + k;")]
    // Inside `unchecked` the compiler folded it wrapped, and so does the browser.
    [InlineData("var r = unchecked(int.MaxValue + 1);", "let r = 2147483647 + 1 | 0;")]
    public void AnIntsArithmetic_WrapsWhereCSharpWraps(string code, string expected)
    {
        TestHelper.ConvertCodeBlock(code).Should().Contain(expected);
    }

    [Fact]
    public void ALoopCounter_StepsAndWraps()
    {
        TestHelper.ConvertCodeBlock("var sum = 0; for (var i = 0; i < 10; i++) sum += i;")
            .Should().Contain("for (let i = 0; i < 10; i = i + 1 | 0)")
            .And.Contain("sum = sum + i | 0;");
    }

    /// <summary>A lambda whose delegate returns nothing discards its body's value, so a postfix step
    /// there is the assignment, with no function around it to answer the value before the step.</summary>
    [Fact]
    public void AStepAVoidLambdaHolds_IsTheAssignment()
    {
        var js = TestHelper.ConvertCodeBlock("int c = 0; System.Action step = () => c++;");
        js.Should().Contain("() => c = c + 1 | 0");
        js.Should().NotContain("$old");
    }

    [Fact]
    public void AStepALambdaAnswers_AnswersTheValueBefore()
    {
        TestHelper.ConvertCodeBlock("int c = 0; System.Func<int> step = () => c++;")
            .Should().Contain("$old");
    }
}
