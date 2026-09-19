using System.Reflection;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A node that carries a LABEL must reach the semantics tree, and the vocabulary is enumerated
/// rather than listed — because this family has now been found by hand three times.
///
/// <para>
/// <c>Icon.Label</c> arrived with a case. <c>Image.Label</c> did not: "a photo with alt text emitted
/// NOTHING on Photon and was invisible to VoiceOver and TalkBack — while the web has carried
/// <c>&lt;img alt&gt;</c> all along", found by an audit. <c>Canvas.Label</c> did not either, and was
/// found by the first consumer of this SDK labelling a grip, counting the accessibility nodes, and
/// getting one fewer than they expected.
/// </para>
///
/// <para>
/// Three of the same defect is a pattern, and the pattern is that the semantics walk is a
/// <c>switch</c> nobody rechecks when the vocabulary grows. So this asks the ASSEMBLY which nodes
/// carry a label, and fails on any it cannot account for: the next node to grow one fails a test
/// rather than waiting for someone to notice a screen reader saying nothing. Silence is the one
/// defect that never announces itself.
/// </para>
/// </summary>
public class LabelledNodesReachSemanticsTests
{
    /// <summary>
    /// The vocabulary's labelled nodes, asked of the assembly. A <c>Label</c> on a visual node is a
    /// promise to assistive technology, whoever declares it.
    /// </summary>
    private static IEnumerable<Type> LabelledNodes() =>
        typeof(VisualNode).Assembly.GetExportedTypes()
            .Where(t => t is { IsAbstract: false } && typeof(VisualNode).IsAssignableFrom(t))
            .Where(t => t.GetProperty("Label", BindingFlags.Public | BindingFlags.Instance)
                ?.PropertyType == typeof(string))
            .OrderBy(t => t.Name);

    /// <summary>
    /// One built sample per labelled node. Hand-written on purpose: a generic constructor call would
    /// make this suite pass for types it could not really build, which is the empty pass this repo
    /// has been caught by before.
    /// </summary>
    private static readonly Dictionary<string, Func<string, VisualNode>> Samples = new()
    {
        ["Icon"] = label => new Icon(Icons.Search, IconSize.Md, label: label),
        ["Image"] = label => new Image("photo.png", 120, 80) { Label = label },
        ["Canvas"] = label => new Canvas(_ => { }) { Label = label },
        ["Vector"] = label => new Vector(CuratedIcons.Resolve(Icons.Search), 24, label: label),
        ["Drawing"] = label => new Drawing(new VectorDrawing(0, 0, 24, 24, []), 24, 24) { Label = label },
        ["CameraPreview"] = label => new CameraPreview(null, 120, 80) { Label = label },
        ["Navigable"] = label => new Navigable([new Text("row", TypeRole.BodyM)], _ => { }) { Label = label },
        ["Overlay"] = label => new Overlay(new Text("over", TypeRole.BodyM)) { Label = label },
        ["LiveRegion"] = label => new LiveRegion(new Text("region", TypeRole.BodyM)) { Label = label },
        ["Adjustable"] = label => new Adjustable(new Text("x", TypeRole.BodyM), _ => { }) { Label = label },
        ["Progress"] = label => new Progress(new Text("bar", TypeRole.BodyM)) { Label = label },
        // Real surfaces with minimal controllers. These two were a `Text` standing in for them —
        // a sample that builds the WRONG TYPE proves nothing and, worse, let
        // `EveryLabelledNode_IsAccountedFor` report complete coverage over a hole. Both are cheap to
        // build; there was no reason for the stand-in beyond my not looking.
        ["CodeSurface"] = label => new CodeSurface(new Text("code", TypeRole.BodyM),
            new CodeEditorController("x")) { Label = label },
        ["SheetSurface"] = label => new SheetSurface(new Text("sheet", TypeRole.BodyM),
            new SheetController(rows: 2, cols: 2)) { Label = label },
        ["TextEntry"] = label => new TextEntry("value") { Label = label },
        ["Pressable"] = label => new Pressable(new Text("press", TypeRole.BodyM), () => { }) { Label = label },
        ["Link"] = label => new Link("https://example.test", new Text("link", TypeRole.BodyM)) { Label = label },
    };

    /// <summary>
    /// The labelled CONTAINERS — the nodes that announce a group and then keep walking, which is
    /// what <see cref="SemanticRole.Group"/> made expressible (#187). This list replaced one named
    /// <c>StillOwedAContainerRole</c> that held <c>Navigable</c> and <c>Overlay</c> and was allowed
    /// only to shrink; it reached zero, so what is worth writing down is no longer who is silent but
    /// what a container has to do.
    /// </summary>
    private static readonly string[] LabelledContainers = ["LiveRegion", "Navigable", "Overlay"];

    /// <summary>
    /// The one container whose subtree cannot be reached on Photon — and NOT because of the role.
    /// <c>MeasureVisitor.Visit(Navigable)</c> measures the node and none of its <c>Rows</c> ("web-only
    /// today", in that arm's own words), so the group is announced over nothing. The role is still
    /// the right answer: VoiceOver now says the composite's name where it said nothing at all. It is
    /// the second half of the contract that has nothing to assert about here yet.
    /// <para>
    /// MAY ONLY SHRINK, and the theory below pins the emptiness rather than skipping it — the day
    /// those rows lay out, this fails and the entry goes.
    /// </para>
    /// </summary>
    private static readonly string[] SubtreeDoesNotLayOutOnPhoton = ["Navigable"];

    /// <summary>
    /// The nodes this suite asserts about, taken from the ASSEMBLY rather than written down. The
    /// theories below were <c>[InlineData]</c> lists, which meant a node could be added to
    /// <see cref="Samples"/>, pass <see cref="EveryLabelledNode_IsAccountedFor"/>, and never be
    /// asked to speak — a reflection guard in name only. Found in review, and it is the same defect
    /// this file was written about: the instrument was never exercised on the case it exists for.
    /// </summary>
    public static TheoryData<string> Speaking
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var type in LabelledNodes()) data.Add(type.Name);
            return data;
        }
    }

    /// <summary>
    /// The guard that matters: a node grows a <c>Label</c> and nothing here knows about it. Without
    /// this the switch stays silent and so does the screen reader.
    /// </summary>
    [Fact]
    public void EveryLabelledNode_IsAccountedFor()
    {
        var unaccounted = LabelledNodes().Select(t => t.Name).Where(n => !Samples.ContainsKey(n)).ToArray();

        unaccounted.Should().BeEmpty(
            "a node that carries a Label promises assistive tech something; add it to Samples and "
            + "make the walk answer for it. Unaccounted: " + string.Join(", ", unaccounted));
    }

    [Theory]
    [MemberData(nameof(Speaking))]
    public void ALabelledNode_SaysWhatItIs(string node)
    {
        const string label = "what this node is";
        var semantics = Describe(Samples[node](label));

        semantics.Should().Contain(s => s.Label == label,
            $"{node}.Label is a promise to assistive technology, and Photon is where it was silent");
    }

    /// <summary>
    /// The container contract, and the ONE thing a group role has to do that no other role here
    /// does: announce AND keep walking.
    ///
    /// <para>
    /// <see cref="Navigable"/> and <see cref="Overlay"/> were both silent on Photon while the web
    /// honoured them, and the reason was not an oversight: every case in this walk added one node
    /// and RETURNED — "one stop for the whole control" — and doing that to a navigable grid hides
    /// every row inside it, which is worse than the missing label. #187 was the decision to add the
    /// role, with a bridge on each platform behind it.
    /// </para>
    ///
    /// <para>
    /// So the assertion is not that the debt list is empty — an empty list passes whatever the walk
    /// does. It is that the group is THERE and the child is STILL REACHABLE past it. Turn
    /// <c>AnnounceGroup</c> back into <c>Announce</c> and the first half still passes; the second
    /// half is what fails.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Containers))]
    public void ALabelledContainer_AnnouncesAGroupAndKeepsWalking(string node)
    {
        const string label = "a labelled group";
        var semantics = Describe(Samples[node](label));

        semantics.Should().Contain(s => s.Role == SemanticRole.Group && s.Label == label,
            $"{node} is a container: a reader stops on it and says its name");

        if (SubtreeDoesNotLayOutOnPhoton.Contains(node))
        {
            semantics.Should().ContainSingle(
                $"{node}'s subtree never lays out on Photon, so the group is announced over "
                + "nothing — remove it from SubtreeDoesNotLayOutOnPhoton when it does, and this "
                + "theory asserts the child the way it does for every other container");
            return;
        }

        semantics.Should().Contain(s => s.Role == SemanticRole.StaticText,
            $"{node}'s child has to survive the announcement — consuming the subtree is exactly "
            + "what kept this role from existing");
    }

    /// <summary>
    /// The written list and the walk's own answer, compared. Derived alone would call a container
    /// that stopped announcing a pass — it would simply leave the set; written alone drifts from
    /// what the walk does. Disagreeing is the finding either way.
    /// </summary>
    [Fact]
    public void TheLabelledContainers_AreExactlyTheNodesThatAnnounceAGroup()
    {
        var announcing = Samples.Keys
            .Where(n => Describe(Samples[n]("a labelled group")).Any(s => s.Role == SemanticRole.Group))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        announcing.Should().Equal(LabelledContainers,
            "a labelled node either announces a group and keeps walking, or announces a leaf role "
            + "and consumes it — and which one it is belongs in this file, not only in the walk");
    }

    /// <summary>
    /// A modal layer's group belongs to the LAYER, and this is the case the theory above cannot see.
    ///
    /// <para>
    /// It builds each container as the ROOT of the tree, where an <see cref="Overlay"/> looks
    /// harmless: its page-flow placeholder happens to be the whole viewport and there is no page
    /// content to be interleaved with. Put the same overlay INSIDE a column and the first version of
    /// this change announced a group at 400x0 — the placeholder's bounds, since
    /// <c>MeasureVisitor</c> gives that node no subtree — sitting between the text before it and the
    /// text after, while the dialog's own elements arrived later under <c>ov0</c> as unrelated
    /// siblings. A zero-height rect is a touch-exploration target that covers nothing.
    /// </para>
    ///
    /// <para>
    /// So the announcement happens at the overlay ROOT, and this asserts both halves of what that
    /// buys: the group has a real box, and the next stop after it is the dialog's own content.
    /// Found in review; the probe that passed is the theory above.
    /// </para>
    /// </summary>
    [Fact]
    public void AModalOverlaysGroup_HasTheLayersBoundsAndSitsWithItsOwnContent()
    {
        var dialog = new Column(gap: 0);
        dialog.Add(new Text("dialog title", TypeRole.BodyM));
        var page = new Column(gap: 0);
        page.Add(new Text("before", TypeRole.BodyM));
        page.Add(new Overlay(dialog) { Label = "Confirm" });
        page.Add(new Text("after", TypeRole.BodyM));

        var semantics = Describe(page);
        var group = semantics.Should().ContainSingle(s => s.Role == SemanticRole.Group).Which;

        group.Bounds.Height.Should().BeGreaterThan(0,
            "the page-flow placeholder measures nothing, and a reader outlines what the group says "
            + "it is — the LAYER, which is where the dialog actually laid out");

        semantics.SkipWhile(s => s.Role != SemanticRole.Group).Skip(1).Should()
            .StartWith(semantics.Single(s => s.Label == "dialog title"),
                "a group is a stop you walk INTO, so what follows it has to be what it holds — "
                + "announced in the page flow it was followed by the text after the overlay instead");
    }

    /// <summary>The containers, for the theory above.</summary>
    public static TheoryData<string> Containers
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in LabelledContainers) data.Add(name);
            return data;
        }
    }

    /// <summary>An unlabelled node stays decorative — the honest answer for pure ornament, and the
    /// reason this cannot simply emit a node for everything.</summary>
    [Theory]
    [MemberData(nameof(Speaking))]
    public void AnUnlabelledNode_StaysSilent(string node)
    {
        Describe(Samples[node]("")).Should().NotContain(s => s.Role == SemanticRole.Image);
    }

    private static IReadOnlyList<SemanticNode> Describe(VisualNode node)
    {
        var host = new PhotonHost(node, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());
        return host.Semantics();
    }
}
