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

    /// <summary>AppendFormat and AppendJoin append what string.Format and string.Join write, bound
    /// by the same parameters and under the same culture policy (#679).</summary>
    [Theory]
    [InlineData("new System.Text.StringBuilder().AppendFormat(\"{0}-{1}\", 1, 2)", "$eq.text.stringBuilder().append($eq.text.stringFormat('{0}-{1}', 1, 2))")]
    [InlineData("new System.Text.StringBuilder().AppendFormat(System.Globalization.CultureInfo.InvariantCulture, \"{0:N2}\", Amount)", "$eq.text.stringBuilder().append($eq.text.stringFormatInvariant('{0:N2}', this.amount))")]
    [InlineData("new System.Text.StringBuilder().AppendJoin(\",\", items)", "$eq.text.stringBuilder().append($eq.text.join(',', this.items))")]
    [InlineData("new System.Text.StringBuilder().AppendJoin(';', \"a\", \"b\")", "$eq.text.stringBuilder().append(['a', 'b'].join(';'))")]
    public void AppendFormatAndAppendJoin_AppendWhatFormatAndJoinWrite(string code, string expected)
    {
        TestHelper.ConvertExpression(code).Should().Be(expected);
    }

    [Theory]
    [InlineData("new System.Text.StringBuilder().Equals(new System.Text.StringBuilder())", "$eq.text.stringBuilder().equalsBuilder($eq.text.stringBuilder())")]
    [InlineData("new System.Text.StringBuilder()[0]", "$eq.text.stringBuilder().item(0)")]
    [InlineData("new System.Text.StringBuilder().Capacity", "$eq.text.stringBuilder().capacity")]
    [InlineData("new System.Text.StringBuilder().EnsureCapacity(10)", "$eq.text.stringBuilder().ensureCapacity(10)")]
    [InlineData("new System.Text.StringBuilder().Append(System.Globalization.CultureInfo.CurrentCulture, $\"x{Id}\")", "$eq.text.stringBuilder().append(`x${this.id}`)")]
    public void AMemberThePageReaches_IsTheTwinsOwn(string code, string expected)
    {
        TestHelper.ConvertExpression(code).Should().Be(expected);
    }

    /// <summary>The indexer's write is the twin's setItem, through the place every writer takes.</summary>
    [Fact]
    public void TheIndexersWrite_IsSetItem()
    {
        TestHelper.ConvertCodeBlock("var sb = new System.Text.StringBuilder(\"12\"); sb[0] = 'x';")
            .Should().Contain("sb.setItem(0, 'x');");
    }

    /// <summary>What cannot cross is refused at the build: a culture an interpolation does not format
    /// in, and GetChunks.</summary>
    [Theory]
    [InlineData("new System.Text.StringBuilder().Append(System.Globalization.CultureInfo.InvariantCulture, $\"x{Id}\")", "EQ2108")]
    [InlineData("new System.Text.StringBuilder().GetChunks()", "")]
    [InlineData("new System.Text.StringBuilder().AppendJoin(\",\", (System.ReadOnlySpan<object?>)[1, \"a\"])", "EQ1004")]
    public void WhatCannotCross_IsRefused(string code, string id)
    {
        var diagnostics = TestHelper.DiagnosticsFor(code);
        diagnostics.Should().NotBeEmpty();
        if (id.Length > 0) diagnostics.Should().Contain(diagnostic => diagnostic.Code == id);
    }
}
