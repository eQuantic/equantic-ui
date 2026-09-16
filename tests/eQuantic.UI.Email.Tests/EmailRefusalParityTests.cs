using System.Runtime.CompilerServices;
using System.Text;
using eQuantic.UI.Email;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Email.Tests;

/// <summary>
/// The two bodies of one message agree about what the vocabulary means.
///
/// <para>
/// A message has an HTML part and a <c>text/plain</c> part, built from the SAME tree so a reader on
/// either side sees the same content. They used to disagree about the edge of the medium: the HTML
/// walker's switch ended in a loud default arm, and the plain-text walker's switch ended in nothing
/// at all — a node neither supports fell out of it in silence. In a whole message that never showed,
/// because the HTML is built first and throws before the text walk runs; it was a divergence waiting
/// for the day the order changed.
/// </para>
///
/// <para>
/// Both walks are visitors over one shared refusal set now (<c>EmailWalk</c>), so the agreement is
/// structural. This suite is what says it stayed that way, and it asks the two visitors DIRECTLY —
/// through <c>EmailRenderer</c> the text side is unreachable for exactly the reason above.
/// </para>
///
/// <para>
/// The nodes are taken from the ASSEMBLY, never listed: the point of the whole exercise is that a
/// node added to the vocabulary cannot be missed, and a hand-written list here would be the one
/// place it still could be.
/// </para>
/// </summary>
public class EmailRefusalParityTests
{
    /// <summary>The seven words this medium carries. Everything else in the vocabulary is refused.</summary>
    private static readonly string[] Carried =
        ["Column", "Row", "Text", "Box", "Image", "Link", "UiComponent"];

    /// <summary>
    /// The vocabulary, asked of the assembly. An instance with no constructor run is enough and is
    /// the honest sample here: a refusal reads the node's TYPE and nothing else, so building a valid
    /// one would be ceremony that could only make the test pass for the wrong reason.
    /// </summary>
    public static IEnumerable<object[]> RefusedNodes() =>
        typeof(VisualNode).Assembly.GetExportedTypes()
            .Where(t => t is { IsAbstract: false, IsPublic: true } && typeof(VisualNode).IsAssignableFrom(t))
            .Where(t => !Carried.Contains(t.Name))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => new object[] { t.Name });

    private static VisualNode Sample(string name) =>
        (VisualNode)RuntimeHelpers.GetUninitializedObject(
            typeof(VisualNode).Assembly.GetExportedTypes().Single(t => t.Name == name));

    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    /// <summary>
    /// THE FACT: each refused node, through both alternatives, refused the same way. Not merely
    /// "both throw" — the same message, because a reader who is told a Pressable cannot be mailed
    /// should be told it once, in one voice, whichever part of the message hit it.
    /// </summary>
    [Theory]
    [MemberData(nameof(RefusedNodes))]
    public void BothAlternatives_RefuseTheSameNode_WithTheSameReason(string nodeName)
    {
        var html = Record.Exception(() =>
            Sample(nodeName).Accept(new EmailVisitor(new ComponentContext(Theme), new StringBuilder()), default));
        var text = Record.Exception(() =>
            Sample(nodeName).Accept(new EmailTextVisitor(Theme, new StringBuilder()), default));

        html.Should().BeOfType<NotSupportedException>(
            $"the HTML alternative has no word for {nodeName} and must say so");
        text.Should().BeOfType<NotSupportedException>(
            $"the text alternative has no word for {nodeName} either — and used to skip it in silence");
        text!.Message.Should().Be(html!.Message,
            "one medium, one reason: the two parts of a message must not explain the same edge differently");
        html.Message.Should().Contain(nodeName, "a refusal that does not name the node is a puzzle");
    }

    /// <summary>
    /// The instrument, checked against itself. A theory over an empty set passes, and a
    /// <see cref="Carried"/> entry naming a node that no longer exists would quietly shrink the set
    /// this suite covers.
    /// </summary>
    [Fact]
    public void TheRefusedSet_IsTheVocabularyMinusTheSevenTheMediumCarries()
    {
        var vocabulary = typeof(VisualNode).Assembly.GetExportedTypes()
            .Where(t => t is { IsAbstract: false, IsPublic: true } && typeof(VisualNode).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToArray();

        Carried.Should().BeSubsetOf(vocabulary.Append("UiComponent"),
            "a carried node that left the vocabulary would silently widen what this suite calls refused");
        RefusedNodes().Should().HaveCount(vocabulary.Length - Carried.Count(vocabulary.Contains),
            "every node the medium does not carry is refused, and this suite sees all of them");
        RefusedNodes().Should().HaveCountGreaterThan(20,
            "the vocabulary has to be real for this to mean anything");
    }
}
