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
        ["Adjustable"] = label => new Adjustable(new Text("x", TypeRole.BodyM), _ => { }) { Label = label },
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
    /// The containers that do not speak yet, named ONCE and read by both the theory below and the
    /// baseline at the bottom. Two copies of this list is how one of them keeps a node excluded
    /// after the other has declared it fixed.
    /// </summary>
    private static readonly string[] StillOwedAContainerRole = ["Navigable", "Overlay"];

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
            foreach (var type in LabelledNodes())
                if (!StillOwedAContainerRole.Contains(type.Name)) data.Add(type.Name);
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
    /// The two that are NOT fixed, named so they cannot be forgotten and so fixing one fails this
    /// test rather than passing quietly.
    ///
    /// <para>
    /// <see cref="Navigable"/> and <see cref="Overlay"/> are CONTAINERS. Every case in the walk
    /// today adds one node and returns — "one stop for the whole control" — and doing that to a
    /// navigable grid would hide every row from assistive tech, which is worse than the missing
    /// label. What they need is a role that says "a labelled group, keep walking", and
    /// <see cref="SemanticRole"/> has no such member: it is ten leaf roles. Adding one is a
    /// vocabulary decision with a bridge on each platform behind it, so it is a decision to take
    /// rather than a line to write.
    /// </para>
    ///
    /// <para>
    /// The web already honours both (`WebRealizer.cs:1356` and `:1198`), so this is a real gap and
    /// not a deliberate silence. The list may only SHRINK.
    /// </para>
    /// </summary>
    [Fact]
    public void TheContainersStillOwedARole_AreExactlyTheseTwo()
    {
        var stillSilent = StillOwedAContainerRole
            .Where(node => !Describe(Samples[node]("a labelled group")).Any(s => s.Label == "a labelled group"))
            .ToArray();

        stillSilent.Should().Equal(StillOwedAContainerRole,
            "when a container role lands and one of these starts speaking, remove it from this list "
            + "— a baseline that only shrinks is the only kind that stays true");
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
