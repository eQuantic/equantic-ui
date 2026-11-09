using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// A culture comparison that ignores case keeps the half and full widths and the kana types apart, as a
/// primary difference right after the other form, which is what .NET's collator is told to do and
/// <c>Intl.Collator</c> cannot be (#533).
/// </summary>
public class WidthAndKanaConformanceTests
{
    private const string Ignoring = "StringComparison.InvariantCultureIgnoreCase";

    [SkippableTheory]
    // The rows of #533.
    [InlineData("\\uFF21", "A")]
    [InlineData("\\uFF41", "a")]
    [InlineData("\\uFF71", "\\u30A2")]
    [InlineData("\\u30A2", "\\u3042")]
    // Case is still ignored between two of one width.
    [InlineData("\\uFF21", "\\uFF41")]
    // The order of a form against what follows it, which decides how the rule meets the collator.
    [InlineData("\\uFF21a", "Ab")]
    [InlineData("\\uFF21a", "AB")]
    [InlineData("a\\uFF21", "ab")]
    [InlineData("Ab", "\\uFF21a")]
    [InlineData("B", "\\uFF21")]
    [InlineData("\\u3042x", "\\u30A2a")]
    [InlineData("\\u3042a", "\\u30A2x")]
    [InlineData("\\uFF21", "A\\u0301")]
    [InlineData("\\u309D", "\\u30FD")]
    [InlineData("\\uFF76\\uFF9E", "\\u30AC")]
    // What the rule leaves alone: an ideographic space, and the forms the sweep below does not separate.
    [InlineData("\\u3000", " ")]
    [InlineData("\\uFF3C", "\\\\")]
    public void TwoForms_CompareAsDotNetComparesThem(string a, string b)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            $"var a = \"{a}\"; var b = \"{b}\"; return string.Equals(a, b, {Ignoring}) + \"|\" + string.Compare(a, b, {Ignoring}) + \"|\" + string.Compare(b, a, {Ignoring}) + \"|\" + a.Equals(b, {Ignoring}) + \"|\" + a.CompareTo(b);");
    }

    /// <summary>Every character of the two blocks against a letter, a digit, a hiragana and a katakana:
    /// an order the rule gets wrong for one of them changes the text both sides write.</summary>
    [SkippableTheory]
    [InlineData(0xFF01, 0xFF5E)]
    [InlineData(0xFF61, 0xFF9F)]
    [InlineData(0xFFA0, 0xFFDC)]
    [InlineData(0xFFE0, 0xFFE6)]
    [InlineData(0x3041, 0x3096)]
    [InlineData(0x30A1, 0x30FE)]
    public void EachCharacterOfABlock_ComparesAsDotNetComparesIt(int first, int last)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertStatementsSameAsDotNet(
            $"var text = \"\"; var probes = new[] {{ \"A\", \"m\", \"5\", \"\\u3042\", \"\\u30A2\", \"\\uFF71\" }}; for (var u = {first}; u <= {last}; u++) {{ var c = ((char)u).ToString(); foreach (var p in probes) text += string.Compare(c, p, {Ignoring}); }} return text;");
    }
}
