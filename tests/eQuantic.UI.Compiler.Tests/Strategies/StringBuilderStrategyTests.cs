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
    public void AMemberThePageReaches_IsTheTwinsOwn(string code, string expected)
    {
        TestHelper.ConvertExpression(code).Should().Be(expected);
    }

    /// <summary>An interpolated <c>Append</c> or <c>AppendLine</c> appends each part in turn, as .NET's
    /// interpolation handler does, so a hole that reads the builder sees the parts before it; a hole's
    /// alignment pads in an append of its own, a provider that is the current culture adds nothing, and
    /// a hole prints as a plain interpolation's does: an enum as its member name, a bool as True. A
    /// string's append stays one append (#679's review).</summary>
    [Theory]
    [InlineData("new System.Text.StringBuilder().Append($\"a{Id}b\")", "$eq.text.stringBuilder().append('a').append(this.id).append('b')")]
    [InlineData("new System.Text.StringBuilder().AppendLine($\"{Name,5}|{Amount:F2}\")", "$eq.text.stringBuilder().appendAligned($eq.text.format(this.name, null), 5).append('|').append($eq.text.format(this.amount, 'F2')).appendLine()")]
    [InlineData("new System.Text.StringBuilder().Append($\"a{Id}\" + $\"b{Name}\")", "$eq.text.stringBuilder().append('a').append(this.id).append('b').append(this.name ?? '')")]
    [InlineData("new System.Text.StringBuilder().Append($\"{Size.Small}{Active}\")", "$eq.text.stringBuilder().append('Small').append($eq.text.format(this.active, null))")]
    [InlineData("new System.Text.StringBuilder().Append(System.Globalization.CultureInfo.CurrentCulture, $\"x{Id}\")", "$eq.text.stringBuilder().append('x').append(this.id)")]
    [InlineData("new System.Text.StringBuilder().Append(\"a\" + Id)", "$eq.text.stringBuilder().append('a' + this.id)")]
    public void AnInterpolatedAppend_AppendsEachPartInTurn(string code, string expected)
    {
        TestHelper.ConvertExpression(code).Should().Be(expected);
    }

    /// <summary><c>Append(StringBuilder)</c> is its own method, refused in words of its own; a builder
    /// typed as an object is a string's append, as C# binds it.</summary>
    [Theory]
    [InlineData("new System.Text.StringBuilder().Append(new System.Text.StringBuilder())", "$eq.text.stringBuilder().appendBuilder($eq.text.stringBuilder())")]
    [InlineData("new System.Text.StringBuilder().Append(new System.Text.StringBuilder(), 0, 0)", "$eq.text.stringBuilder().appendBuilder($eq.text.stringBuilder(), 0, 0)")]
    [InlineData("new System.Text.StringBuilder().Append((object)new System.Text.StringBuilder())", "$eq.text.stringBuilder().append($eq.text.stringBuilder())")]
    public void AppendOfABuilder_IsItsOwnMethod(string code, string expected)
    {
        TestHelper.ConvertExpression(code).Should().Be(expected);
    }

    /// <summary>The twin takes C#'s parameters by position, so a named argument is bound to its
    /// parameter wherever it is written, each still evaluated in the order written.</summary>
    [Theory]
    [InlineData("new System.Text.StringBuilder(capacity: 50, value: \"ab\")", "$eq.text.stringBuilder('ab', 50)")]
    [InlineData("new System.Text.StringBuilder(\"12\").Insert(value: Name, index: Id)", "(($0, $1, $2) => $0.insert($2, $1))($eq.text.stringBuilder('12'), this.name, this.id)")]
    [InlineData("new System.Text.StringBuilder(\"12\").Append(count: 1, startIndex: 0, value: Name)", "$eq.text.stringBuilder('12').append(this.name, 0, 1)")]
    public void ANamedArgument_IsBoundToItsParameter(string code, string expected)
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
