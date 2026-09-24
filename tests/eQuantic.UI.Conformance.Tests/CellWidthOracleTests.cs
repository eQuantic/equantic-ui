using System.Text;
using System.Text.Json;
using eQuantic.UI.Code;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// How many cells a character takes on a monospace grid is not a property .NET or JavaScript
/// exposes, so the code engine writes its own table (<see cref="CodeLineCells.IsWide"/> and
/// <see cref="CodeLineCells.IsZeroWidth"/>) and its own cluster rules. This compares BOTH sides of
/// it, the C# engine and its twin in the bundle the Server serves, with the SDK's own embedded Bun,
/// whose <c>Bun.stringWidth</c> follows the terminal convention the table does (East Asian Wide and
/// Fullwidth, emoji, zero-width format characters), pinned with the Bun the repository ships. The
/// table was written by hand and missed characters, 🟰 among them, counted text-default emoji such
/// as 🌡 as two cells, and nothing compared it with anything.
/// <para>
/// Every character that can begin a text element is compared, which leaves out marks, controls,
/// surrogates and unassigned and private characters. Two places where the oracle and the grid part
/// on purpose are named rather than hidden: <see cref="OracleErrors"/>, and conjoining Hangul jamo,
/// which Bun counts one by one where a font draws the syllable they make in two cells.
/// </para>
/// </summary>
public class CellWidthOracleTests
{
    /// <summary>Three LETTERS Bun counts as zero-width, which every font that has them draws.</summary>
    private static readonly HashSet<int> OracleErrors = [0x0980, 0x0C80, 0x0D3A];

    /// <summary>Two lines, one character per code point from U+0020: the oracle's width, then the
    /// twin's, and <c>x</c> for a character that cannot begin an element.</summary>
    private static string EveryCharacter(string runtime) => $$"""
        import { CodeLineCells } from '{{runtime}}';
        const skip = /^[\p{Mn}\p{Me}\p{Mc}\p{Cc}\p{Cs}\p{Cn}\p{Co}]$/u;
        const oracle = [];
        const twin = [];
        for (let cp = 0x20; cp <= 0x10FFFF; cp++) {
          const s = cp >= 0xD800 && cp <= 0xDFFF ? '' : String.fromCodePoint(cp);
          if (s === '' || skip.test(s)) { oracle.push('x'); twin.push('x'); continue; }
          oracle.push(String(Bun.stringWidth(s)));
          twin.push(String(new CodeLineCells(s, 4).width));
        }
        console.log(oracle.join(''));
        console.log(twin.join(''));
        """;

    [SkippableFact]
    public void EveryCharacterIsAsWideAsTheOracleSays_OnBothSides()
    {
        JsExecutor.RequireBun();   // the oracle is Bun.stringWidth
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");

        var lines = JsExecutor.Run(EveryCharacter(runtime), timeoutMs: 180_000).Split('\n');
        var (oracle, twin) = (lines[0].TrimEnd(), lines[1].TrimEnd());

        var mismatches = new StringBuilder();
        var count = 0;
        for (var cp = 0x20; cp <= 0x10FFFF; cp++)
        {
            var expected = oracle[cp - 0x20];
            if (expected == 'x' || OracleErrors.Contains(cp)) continue;
            var dotnet = (char)('0' + new CodeLineCells(char.ConvertFromUtf32(cp), 4).Width);
            var web = twin[cp - 0x20];
            if (dotnet == expected && web == expected) continue;
            if (count++ < 40)
                mismatches.Append($"\n  U+{cp:X4}: the oracle says {expected}, .NET {dotnet}, the web {web}");
        }

        count.Should().Be(0, $"every character takes the cells the oracle gives it, on both sides:{mismatches}");
    }

    /// <summary>Every mark, one character per code point from U+0300: the twin's width alone, and
    /// the kind of mark (<c>n</c> nonspacing or enclosing, <c>c</c> spacing), <c>x</c> for the rest.</summary>
    private static string EveryMark(string runtime) => $$"""
        import { CodeLineCells } from '{{runtime}}';
        const combining = /^[\p{Mn}\p{Me}]$/u;
        const spacing = /^\p{Mc}$/u;
        const kinds = [];
        const widths = [];
        for (let cp = 0x300; cp <= 0x10FFFF; cp++) {
          const s = cp >= 0xD800 && cp <= 0xDFFF ? '' : String.fromCodePoint(cp);
          const kind = s === '' ? 'x' : combining.test(s) ? 'n' : spacing.test(s) ? 'c' : 'x';
          kinds.push(kind);
          widths.push(kind === 'x' ? 'x' : String(new CodeLineCells(s, 4).width));
        }
        console.log(kinds.join(''));
        console.log(widths.join(''));
        """;

    /// <summary>
    /// A MARK that begins an element (at the start of a line, or after a tab) takes the cells its
    /// kind says, on both sides: none when it is nonspacing or enclosing, which combine with whatever
    /// is drawn before them, and its advance when it is spacing. Bun is no oracle here, since it
    /// counts some marks as one cell and the voiced sound mark as two, so this compares each side
    /// with the rule, by its own platform's categories.
    /// </summary>
    [SkippableFact]
    public void AMarkThatBeginsAnElementTakesTheCellsItsKindSays_OnBothSides()
    {
        JsExecutor.RequireBun();   // the web side runs in the embedded Bun
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");

        var lines = JsExecutor.Run(EveryMark(runtime), timeoutMs: 180_000).Split('\n');
        var (kinds, widths) = (lines[0].TrimEnd(), lines[1].TrimEnd());

        var wrong = new StringBuilder();
        var count = 0;
        for (var cp = 0x300; cp <= 0x10FFFF; cp++)
        {
            if (cp is >= 0xD800 and <= 0xDFFF) continue;
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(cp);
            var dotnet = new CodeLineCells(char.ConvertFromUtf32(cp), 4).Width;
            var dotnetRight = category switch
            {
                System.Globalization.UnicodeCategory.NonSpacingMark or System.Globalization.UnicodeCategory.EnclosingMark => dotnet == 0,
                System.Globalization.UnicodeCategory.SpacingCombiningMark => dotnet > 0,
                _ => true,
            };
            var kind = kinds[cp - 0x300];
            var web = widths[cp - 0x300];
            var webRight = kind switch { 'n' => web == '0', 'c' => web != '0', _ => true };
            if (dotnetRight && webRight) continue;
            if (count++ < 40) wrong.Append($"\n  U+{cp:X4} ({category}): .NET {dotnet}, the web {web}");
        }

        count.Should().Be(0, $"a mark takes the cells its kind says, on both sides:{wrong}");
    }

    /// <summary>The widths that belong to a CLUSTER rather than to its first character.</summary>
    [SkippableTheory]
    [InlineData("\U0001F1E7\U0001F1F7")]                                                   // a flag
    [InlineData("\U0001F1E7")]                                                             // one regional indicator
    [InlineData("\U0001F1E7\U0001F1F7\U0001F1F5")]                                         // a flag and one left over
    [InlineData("1\uFE0F\u20E3")]                                                          // a keycap
    [InlineData("\U0001F468\u200D\U0001F469\u200D\U0001F467")]                             // a family
    [InlineData("\U0001F44D\U0001F3FD")]                                                   // a skin tone
    [InlineData("\u270C\U0001F3FB")]                                                       // a skin tone on a text-default base
    [InlineData("\u2764\uFE0F")]                                                           // a presentation selector
    [InlineData("\U0001F3F4\U000E0067\U000E0062\U000E0065\U000E006E\U000E0067\U000E007F")] // a tag sequence flag
    [InlineData("e\u0301")]                                                                // a mark on its base
    [InlineData("a\u200Db")]                                                               // a joiner between letters
    [InlineData("x\u200By")]                                                               // a zero-width space
    public void AClusterIsAsWideAsTheOracleSays_OnBothSides(string text)
    {
        JsExecutor.RequireBun();   // the oracle is Bun.stringWidth
        var runtime = ConformanceRunner.RuntimeJsUrl() ?? throw new InvalidOperationException("No served runtime.js.");
        var literal = JsonSerializer.Serialize(text);

        var answers = JsExecutor.Run(
            $"import {{ CodeLineCells }} from '{runtime}';\n"
            + $"console.log(Bun.stringWidth({literal}), new CodeLineCells({literal}, 4).width);").Split(' ');

        var expected = int.Parse(answers[0]);
        new CodeLineCells(text, 4).Width.Should().Be(expected, ".NET agrees with the oracle");
        int.Parse(answers[1]).Should().Be(expected, "the web agrees with the oracle");
    }
}
