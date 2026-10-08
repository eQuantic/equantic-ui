using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// A builder's call names the runtime method its overload is (#650). The runtime's methods take each
/// overload by its count of arguments, and the <c>char[]</c> overloads of <c>Append</c> and
/// <c>Insert</c> are methods of their own, because a null array is refused in words a null string is
/// not. What each answers is proved on both sides by StringBuilderConformanceTests.
/// </summary>
public class StringBuilderStrategyTests
{
    [Theory]
    [InlineData("new System.Text.StringBuilder().Append(new[] { 'a', 'b' })", "$eq.text.stringBuilder().appendChars(['a', 'b'])")]
    [InlineData("new System.Text.StringBuilder().Append(new[] { 'a', 'b' }, 0, 1)", "$eq.text.stringBuilder().appendChars(['a', 'b'], 0, 1)")]
    [InlineData("new System.Text.StringBuilder().Insert(0, new[] { 'a', 'b' })", "$eq.text.stringBuilder().insertChars(0, ['a', 'b'])")]
    [InlineData("new System.Text.StringBuilder().Insert(0, new[] { 'a', 'b' }, 1, 1)", "$eq.text.stringBuilder().insertChars(0, ['a', 'b'], 1, 1)")]
    public void ACharArrayOverload_IsItsOwnMethod(string code, string expected)
    {
        TestHelper.ConvertExpression(code).Should().Be(expected);
    }

    [Theory]
    [InlineData("new System.Text.StringBuilder().Append('x', 3)", "$eq.text.stringBuilder().append('x', 3)")]
    [InlineData("new System.Text.StringBuilder().Append(\"abc\", 1, 2)", "$eq.text.stringBuilder().append('abc', 1, 2)")]
    [InlineData("new System.Text.StringBuilder().Insert(0, \"ab\", 2)", "$eq.text.stringBuilder().insert(0, 'ab', 2)")]
    [InlineData("new System.Text.StringBuilder().Replace(\"a\", \"b\", 0, 1)", "$eq.text.stringBuilder().replace('a', 'b', 0, 1)")]
    [InlineData("new System.Text.StringBuilder().ToString(0, 1)", "$eq.text.stringBuilder().toString(0, 1)")]
    public void AnyOtherOverload_PassesItsArgumentsToTheMethodOfItsName(string code, string expected)
    {
        TestHelper.ConvertExpression(code).Should().Be(expected);
    }
}
