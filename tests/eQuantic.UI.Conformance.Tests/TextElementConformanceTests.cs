using eQuantic.UI.Conformance.Tests.Infrastructure;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// TEXT ELEMENTS, executed on both sides: <c>StringInfo</c>'s grapheme clusters under .NET, and the
/// same calls transpiled to <c>$eq.text</c> over the platform's segmenter. A code editor steps and
/// deletes by these, so an emoji is one step and one Backspace. The cases avoid the rules Unicode
/// changed recently (Indic conjuncts), where the two platforms may carry different versions.
/// </summary>
public class TextElementConformanceTests
{
    private const string Starts = "string.Join(\",\", System.Globalization.StringInfo.ParseCombiningCharacters(";

    [SkippableTheory]
    [InlineData(Starts + "\"abc\"))")]                                                          // "0,1,2"
    [InlineData(Starts + "\"\"))")]                                                             // ""
    [InlineData(Starts + "\"e\\u0301a\"))")]                                                    // "0,2" — a mark stays with its base
    [InlineData(Starts + "\"\\U0001F600b\"))")]                                                 // "0,2" — a surrogate pair is one
    [InlineData(Starts + "\"\\U0001F468\\u200D\\U0001F469\\u200D\\U0001F467x\"))")]             // "0,8" — a family is one
    [InlineData(Starts + "\"\\U0001F1E7\\U0001F1F7\\U0001F1F5\\U0001F1F9\"))")]                 // "0,4" — two flags
    [InlineData(Starts + "\"\\U0001F44D\\U0001F3FD!\"))")]                                      // "0,4" — a skin tone stays
    [InlineData(Starts + "\"a\\r\\nb\"))")]                                                     // "0,1,3" — CR LF is one
    [InlineData(Starts + "\"\\u2764\\uFE0F.\"))")]                                              // "0,2" — the variation selector stays
    [InlineData("System.Globalization.StringInfo.GetNextTextElementLength(\"e\\u0301a\", 0)")]  // 2
    [InlineData("System.Globalization.StringInfo.GetNextTextElementLength(\"e\\u0301a\", 2)")]  // 1
    [InlineData("System.Globalization.StringInfo.GetNextTextElementLength(\"ab\", 2)")]         // 0 — the end
    [InlineData("System.Globalization.StringInfo.GetNextTextElementLength(\"\\U0001F600b\")")]  // 2
    public void TextElements_MatchDotNet(string expression)
    {
        Skip.IfNot(JsExecutor.IsAvailable, "No JS engine available.");
        ConformanceRunner.AssertSameAsDotNet(expression);
    }
}
