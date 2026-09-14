using System.Reflection;
using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The vocabulary speaks no target's language, in its public surface as well as its prose.
///
/// <para>
/// The rule is CLAUDE.md's: a name here would not exist if the web did not, so it does not belong.
/// It has been broken four times and fixed four times — `Image.Alt`, `Positioned.ZIndex`,
/// `Pinned` (from `Sticky`), and `Navigator.Go(href)`, which survived the first three sweeps
/// because a PARAMETER name is not something anyone greps for. Each was found by a person reading,
/// which is the part this replaces.
/// </para>
///
/// <para>
/// It matches whole WORDS out of a PascalCase or camelCase identifier, not substrings: `Anchored`
/// is not `anchor`'s fault and `Diverging` is not `div`'s. And the list holds only words that are
/// unambiguously one target's, which is what lets it have no exemptions at all. `Hover`,
/// `Placeholder`, `Viewport` and `Alt` were tried and dropped — a pointer hovers on every target,
/// a `KeyModifiers.Alt` is a KEY, and a pin whose steady state is ten excuses teaches people to add
/// an eleventh.
/// </para>
///
/// <para>
/// The exception the rule already states: a type that IS the transcription of a target's artifact
/// keeps that artifact's words (`SvgDocument` parses SVG; `PhotonEntitlement` names a macOS
/// entitlement). None of those use a word below, which is why this needs no list of them either —
/// if one ever does, it is a decision to write down here rather than a line to delete.
/// </para>
/// </summary>
public class NeutralVocabularyTests
{
    /// <summary>
    /// Words that belong to ONE target and have no neutral meaning. A word earns its place here by
    /// being unusable in a sentence about a native window or an email.
    /// </summary>
    private static readonly string[] BorrowedWords =
    [
        // The DOM and CSS.
        "href", "zindex", "onclick", "innerhtml", "outerhtml", "classname", "tagname", "dom", "css",
        "stylesheet", "iframe", "srcset", "sticky", "colspan", "rowspan", "tbody", "thead",
        // Apple's frameworks.
        "nsview", "uiview", "uikit", "appkit", "nsstring", "cgrect", "uiviewcontroller",
        // Android's.
        "viewgroup", "activity", "fragmentmanager",
        // Win32.
        "hwnd", "win32", "wndproc",
    ];

    /// <summary>
    /// The words an identifier is made of, AND each adjacent pair joined — because half of these
    /// names are two words in the source and one word in the target's own spelling. `ZIndex` splits
    /// to [z, index] and matches nothing; `zindex` is what CSS calls it. Found by this file's own
    /// guard, which had `ZIndex` in it as a case that should be caught and was not.
    /// </summary>
    private static IEnumerable<string> WordsIn(string identifier)
    {
        // .NET's interface prefix is not a WORD, and joining it to the next one invents names:
        // `IFrameTicker` — the frame clock — read as `iframe`. Dropped before splitting, which is
        // the same thing a reader does. A Primitives type genuinely named `IFrame` would then read
        // as an interface called Frame and go unnoticed here; the DOM escape hatch lives in the web
        // realizer, and a `<iframe>` transcription in the VOCABULARY would be a design decision
        // long before it was a naming one.
        var name = Regex.IsMatch(identifier, "^I[A-Z]") ? identifier[1..] : identifier;
        var words = Regex.Matches(name, @"[A-Z]+(?![a-z])|[A-Z][a-z]*|[a-z]+|[0-9]+")
            .Select(match => match.Value.ToLowerInvariant())
            .ToList();
        foreach (var word in words) yield return word;
        for (var i = 0; i + 1 < words.Count; i++) yield return words[i] + words[i + 1];
    }

    [Fact]
    public void NoPublicNameInTheVocabularyBorrowsATargetsWord()
    {
        var borrowed = new List<string>();

        foreach (var type in typeof(VisualNode).Assembly.GetExportedTypes())
        {
            Check($"type {type.Name}", type.Name);
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance
                         | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                // A property's accessors repeat its name; the property itself is already checked.
                if (member.Name.StartsWith("get_", StringComparison.Ordinal)
                    || member.Name.StartsWith("set_", StringComparison.Ordinal)) continue;

                Check($"{type.Name}.{member.Name}", member.Name);
                if (member is MethodBase method)
                    foreach (var parameter in method.GetParameters())
                        Check($"{type.Name}.{member.Name}({parameter.Name})", parameter.Name ?? "");
            }
        }

        borrowed.Should().BeEmpty(
            "the vocabulary speaks no target's language — a name here would not exist if that "
            + "target did not, and the neutral word is what the rest of the SDK already uses "
            + "(Alt became Label, ZIndex became Layer, Sticky became Pinned, href became "
            + "destination): " + string.Join("; ", borrowed));

        void Check(string where, string name)
        {
            foreach (var word in WordsIn(name))
                if (BorrowedWords.Contains(word)) borrowed.Add($"{word} in {where}");
        }
    }

    /// <summary>
    /// The guard, and it is not decoration: the matcher is a regex over identifiers, and a regex
    /// that stopped splitting words would report nothing and pass. This hands it the name that was
    /// actually wrong until this branch.
    /// </summary>
    [Theory]
    [InlineData("Go", "href", true)]
    [InlineData("SetHref", "", true)]
    [InlineData("ZIndex", "", true)]
    // The two-word spellings, which are the ones a single-word matcher misses.
    [InlineData("InnerHtml", "", true)]
    [InlineData("ClassName", "", true)]
    [InlineData("NSView", "", true)]
    // .NET's interface prefix is not a word: `IFrameTicker` is the frame clock, and joining `i` to
    // `frame` invented an `iframe` that is not there. Caught by this guard before it shipped.
    [InlineData("IFrameTicker", "", false)]
    [InlineData("Anchored", "", false)]
    [InlineData("Diverging", "", false)]
    [InlineData("GridSpan", "", false)]
    // …and a pair that only LOOKS like one: a grid's columns and a table's colspan are not the
    // same word, and joining every adjacent pair must not invent one.
    [InlineData("ColumnSpacing", "", false)]
    public void TheMatcherSeesWholeWordsAndOnlyWholeWords(string member, string parameter, bool borrowed)
    {
        var words = WordsIn(member).Concat(WordsIn(parameter)).ToList();

        words.Any(BorrowedWords.Contains).Should().Be(borrowed,
            $"'{member}({parameter})' splits to [{string.Join(", ", words)}] — a substring matcher "
            + "would call Anchored and Diverging borrowed, and a matcher that stopped splitting "
            + "would call nothing borrowed at all");
    }
}
