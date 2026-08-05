using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Compiler.Tests.Strategies;

/// <summary>
/// SLICING. `text[a..b]` is the most ordinary thing a tokenizer, a formatter, or a parser writes,
/// and it used to transpile to <c>text[{ start: a, end: b }]</c> — an object handed to the index
/// operator, which is <c>undefined</c> every time, silently. It was found by writing a syntax
/// highlighter in C# and realizing it on the web, where every token came out blank.
/// </summary>
public class RangeIndexerTests
{
    [Theory]
    // The shapes JavaScript can say directly, at no runtime cost.
    [InlineData("text[1..4]", "text.slice(1, 4)")]
    [InlineData("text[start..end]", "text.slice(start, end)")]
    [InlineData("text[2..]", "text.slice(2)")]
    [InlineData("text[..4]", "text.slice(0, 4)")]
    [InlineData("text[..]", "text.slice(0)")]
    // `^` is what a negative argument to slice already means.
    [InlineData("text[..^1]", "text.slice(0, -1)")]
    [InlineData("text[^3..]", "text.slice(-3)")]
    [InlineData("text[1..^1]", "text.slice(1, -1)")]
    public void ARangeIndexIsASlice(string csharp, string expected)
    {
        TestHelper.ConvertExpression(csharp).Should().Be(expected);
    }

    /// <summary>
    /// The one case a negative index gets WRONG: `^0` is the end of the value, but `slice(0, -0)`
    /// is `slice(0, 0)` — empty. Anything that is not a positive literal after `^` could be zero,
    /// so it resolves against the length the way Index.GetOffset does.
    /// </summary>
    [Theory]
    [InlineData("text[..^0]", "$eq.slice(text, 0, false, 0, true)")]
    [InlineData("text[..^n]", "$eq.slice(text, 0, false, n, true)")]
    [InlineData("text[^n..]", "$eq.slice(text, n, true, null, false)")]
    public void AnEndpointThatMightBeZeroFromTheEnd_GoesThroughTheRuntime(string csharp, string expected)
    {
        TestHelper.ConvertExpression(csharp).Should().Be(expected);
    }

    [Fact]
    public void TheReceiverIsEvaluatedONCE_HoweverComplexItIs()
    {
        TestHelper.ConvertExpression("Line(index)[at..end]")
            .Should().Be("line(index).slice(at, end)",
                "a slice that called the method twice would do the work twice — and disagree with "
                + "itself when the method is not pure");
    }
}
