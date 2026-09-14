using System.Reflection;
using System.Text.RegularExpressions;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Every node in the vocabulary is accounted for by every dispatch that walks it, or it is named
/// here as a deliberate absence.
///
/// <para>
/// The SDK has ONE visual vocabulary and several places that decide what each word means — how big
/// it is, what the GPU draws, what a screen reader says. Each is a switch over the node type, each
/// covers a different subset, and until this file nothing made them agree. A deliberate omission and
/// a forgotten one looked identical to a reader and to the build.
/// </para>
///
/// <para>
/// That is not a hypothetical. Six defects arrived through this gap, and only two were found here:
/// <c>Canvas.Label</c> silent to VoiceOver (found by a consumer counting accessibility elements),
/// <c>Image.Label</c> emitting nothing on Photon while the web had carried <c>alt</c> all along,
/// <c>Vector</c>/<c>Drawing</c>/<c>CameraPreview</c> the same (all three in one sweep, once the
/// question was asked of the ASSEMBLY instead of a reader's memory), <c>Text.Align</c> dropped by
/// all three native text services for months, and <c>VisualNode.Key</c> documented as reconciler
/// identity since the vocabulary existed and read by neither realizer.
/// </para>
///
/// <para>
/// The transpiler solves this same problem — dispatch over an open set of constructs, two
/// implementations that must agree — with a registry and a coverage suite that enumerates by
/// reflection against a baseline that may only shrink. CLAUDE.md names the transpiler as the bar;
/// this is that bar applied to the half of the SDK where the vocabulary actually lives.
/// </para>
///
/// <para>
/// WHY SOURCE AND NOT REFLECTION: a switch arm is not a member, so there is nothing to reflect over.
/// The patterns matched below are the two shapes C# offers for type dispatch — <c>case Text t:</c>
/// and <c>Text t =&gt;</c> — and never a bare name, so a node mentioned only in a COMMENT does not
/// count as handled. That distinction is the whole difference between this test and a grep.
/// </para>
/// </summary>
public class VocabularyCoverageTests
{
    /// <summary>The vocabulary, asked of the assembly rather than listed.</summary>
    private static IReadOnlyList<string> Vocabulary() =>
        typeof(VisualNode).Assembly.GetExportedTypes()
            .Where(t => t is { IsAbstract: false, IsPublic: true } && typeof(VisualNode).IsAssignableFrom(t))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

    private static readonly string Root = RepositoryRoot();

    /// <summary>
    /// A dispatch, its source, and the nodes it is allowed not to know about. The exempt list may
    /// only SHRINK: a node added to it needs a reason in the same commit, and a node that starts
    /// being handled fails this test until it comes out.
    /// </summary>
    private sealed record Dispatch(string Name, string Path, string Question, params string[] Exempt);

    private static readonly Dispatch[] Dispatches =
    [
        new("LayoutEngine",
            "src/eQuantic.UI.Native.Framework/Layout/LayoutEngine.cs",
            "how big is it, and where",
            // An Overlay gets a Layout call OF ITS OWN — the realizer runs one per layer — so it is
            // never reached as a child. Navigable is a web-only keyboard container today, and
            // WebFrame is the DOM escape hatch, which cannot cross at all.
            "Navigable", "Overlay", "WebFrame"),

        new("PhotonRealizer",
            "src/eQuantic.UI.Native.Components/PhotonRealizer.cs",
            "what does the GPU draw",
            // The realizer paints LAID-OUT nodes. Everything the layout pass already resolved into
            // geometry arrives as a rectangle with children, so containers and positioners never
            // need a case of their own — they have nothing left to draw.
            "AdaptiveNode", "Anchored", "Column", "DragDismiss", "Draggable", "Flexible", "Grid",
            "InFlow", "Link", "LoopMotion", "Navigable", "Overlay", "Pinned", "Positioned",
            "Presence", "Row", "SafeArea", "ScrollView", "SheetSurface", "Spacer", "Stack",
            "WebFrame"),

        new("Semantics",
            "src/eQuantic.UI.Native.Components/Semantics.cs",
            "what does a screen reader say",
            // Pure layout and pure behaviour announce nothing of their own: a Row is not an object
            // to a screen reader, and a Shortcut, a Hoverable or a Simulated wraps a child without
            // being anything itself.
            "AdaptiveNode", "Anchored", "Box", "Column", "DragDismiss", "Draggable", "Flexible",
            "Grid", "Hoverable", "InFlow", "InView", "Pinned", "Positioned", "Presence", "Row",
            "SafeArea", "ScrollView", "Shortcut", "Simulated", "Spacer", "Stack", "WebFrame",
            // DECORATIVE BY AGREEMENT, and this is where that agreement is written down. The web
            // marks both `aria-hidden`, on the reasoning that a busy indicator is ornament and the
            // surrounding copy is what announces the wait. Photon reaches the same answer by having
            // no case at all — the two agree, and until this line nothing recorded that they were
            // supposed to.
            "LoopMotion", "Spinner",
            // A REAL GAP WEARING AN EXEMPTION, and the only two rows here that should not be here.
            // The web honours both (WebRealizer.cs:1356 and :1198) and Photon is silent, because
            // every case in the walk adds one node and returns — "one stop for the whole control" —
            // and doing that to a navigable grid would hide every row inside it. What they need is
            // a role that means "a labelled group, keep walking", and SemanticRole has none: it is
            // ten leaf roles. That is a vocabulary decision with a bridge per platform behind it.
            "Navigable", "Overlay"),
    ];

    /// <summary>
    /// THE GUARD: a node enters the vocabulary and a dispatch does not know about it. Without this
    /// the switch stays silent and so, in four of the six cases above, did the application.
    /// </summary>
    [Theory]
    [InlineData("LayoutEngine")]
    [InlineData("PhotonRealizer")]
    [InlineData("Semantics")]
    public void EveryNode_IsHandledOrNamedAsAnAbsence(string dispatchName)
    {
        var dispatch = Dispatches.Single(d => d.Name == dispatchName);
        var source = File.ReadAllText(Path.Combine(Root, dispatch.Path));
        var vocabulary = Vocabulary();
        vocabulary.Should().HaveCountGreaterThan(20, "the vocabulary has to be real for this to mean anything");

        var unaccounted = vocabulary
            .Where(node => !Handles(source, node) && !dispatch.Exempt.Contains(node))
            .ToArray();

        unaccounted.Should().BeEmpty(
            $"{dispatch.Name} decides {dispatch.Question} and these nodes reach it with no case of "
            + $"their own. Handle them, or add them to that dispatch's Exempt list with the reason. "
            + $"Unaccounted: {string.Join(", ", unaccounted)}");
    }

    /// <summary>
    /// The other direction, and the half an exemption list cannot do without: an exemption that has
    /// stopped being true. A node that is now handled must come OUT of the list, or the list
    /// slowly becomes a record of what somebody once believed.
    /// </summary>
    [Theory]
    [InlineData("LayoutEngine")]
    [InlineData("PhotonRealizer")]
    [InlineData("Semantics")]
    public void NoExemptionOutlivesTheGapItDescribes(string dispatchName)
    {
        var dispatch = Dispatches.Single(d => d.Name == dispatchName);
        var source = File.ReadAllText(Path.Combine(Root, dispatch.Path));

        var stale = dispatch.Exempt.Where(node => Handles(source, node)).ToArray();

        stale.Should().BeEmpty(
            $"{dispatch.Name} now handles these, so they are no longer absences. Remove them from "
            + $"its Exempt list: {string.Join(", ", stale)}");
    }

    /// <summary>An exemption naming a node that no longer exists is the same rot from the other end.</summary>
    [Fact]
    public void NoExemptionNamesANodeThatIsGone()
    {
        var vocabulary = Vocabulary();
        var ghosts = Dispatches
            .SelectMany(d => d.Exempt.Select(node => (d.Name, node)))
            .Where(e => !vocabulary.Contains(e.node))
            .Select(e => $"{e.Name}: {e.node}")
            .ToArray();

        ghosts.Should().BeEmpty("an exemption for a node the vocabulary no longer has is dead prose");
    }

    /// <summary>
    /// A node is HANDLED when it appears as a type pattern — the two shapes C# offers for dispatch.
    /// A bare name is not enough on purpose: <c>WebRealizer</c> mentions <c>SemanticRole</c> in a
    /// comment while sitting in an assembly that cannot reference it, which is exactly the kind of
    /// mention that must not count as coverage.
    /// </summary>
    private static bool Handles(string source, string node)
    {
        // The name may be QUALIFIED at the dispatch: `case Primitives.Image` is how the semantics
        // walk writes it, because `Image` collides with a type in scope there. The first version of
        // this matcher missed that and reported a node as unhandled three lines above its own case —
        // the instrument was wrong before the code was, which is the usual order.
        var name = $@"(?:\w+\.)*{Regex.Escape(node)}";
        return Regex.IsMatch(source, $@"\bcase {name}\b")
            || Regex.IsMatch(source, $@"\b{name} [a-z]\w* =>");
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the pin reads the engine's source, so it has to find the tree");
        return dir!.FullName;
    }
}
