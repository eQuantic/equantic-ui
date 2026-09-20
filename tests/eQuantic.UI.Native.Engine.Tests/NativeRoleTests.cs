using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The one place a role's native answers can be READ by a test — which is the whole reason
/// <see cref="NativeRole"/> exists. The three bridges held those answers before, in
/// <c>net10.0-ios</c> and <c>net10.0-android</c> assemblies no test project can reference, behind
/// switches that ended in a catch-all spelled exactly like the
/// <see cref="SemanticRole.StaticText"/> row (#249).
/// <para>
/// The compiler is the first instrument and this is the second: appending a role with no row fails
/// the BUILD by name (CS8509). What is left for a test is everything the compiler cannot see — that
/// the row somebody wrote actually says something, and that it does not say the one thing a missing
/// row used to say.
/// </para>
/// </summary>
public class NativeRoleTests
{
    private static SemanticRole[] AllRoles => Enum.GetValues<SemanticRole>();

    /// <summary>
    /// The defect itself. A role with no arm was announced as a paragraph on all three targets, and
    /// nothing could tell that apart from a role deliberately mapped to one — so the rule is that
    /// only <see cref="SemanticRole.StaticText"/> may claim static text, on every target at once.
    /// </summary>
    [Fact]
    public void NoRoleButStaticTextIsAnnouncedAsStaticText()
    {
        var scanned = 0;
        foreach (var role in AllRoles)
        {
            if (role == SemanticRole.StaticText) continue;
            scanned++;
            var native = NativeRole.Of(role);

            native.AppKit.Should().NotBe("AXStaticText",
                $"{role} would reach VoiceOver on macOS as a paragraph");
            native.Android.Should().NotBe("android.widget.TextView",
                $"{role} would reach TalkBack as a paragraph");
            native.UIKit.Should().NotBe(UIKitTrait.StaticText,
                $"{role} would reach VoiceOver on iOS as a paragraph");
        }

        // A sweep that passes by having nothing to sweep is the shape this repository keeps being
        // caught by, so the count is asserted rather than assumed.
        scanned.Should().BeGreaterThan(10, "the vocabulary really does hold that many roles");
    }

    /// <summary>
    /// Every row says something on every target. This is what catches the half-written row — the
    /// arm added to stop the build, with an empty string or a copied neighbour's name in it.
    /// </summary>
    [Fact]
    public void EveryRoleIsNamedOnEveryTarget()
    {
        foreach (var role in AllRoles)
        {
            var native = NativeRole.Of(role);

            native.AppKit.Should().StartWith("AX", $"{role}'s NSAccessibility role is an AX constant");
            native.Android.Should().Contain(".", $"{role}'s Android class is a fully qualified name");
        }
    }

    /// <summary>
    /// The columns that are not names, tied to the frame instead of to an opinion: for every control
    /// the realizer made REACHABLE — a tap lands on it, or Tab does — the role it announces has to
    /// afford the same thing to a screen reader, AND the action it advertises has to actually run.
    /// An offer nothing performs is worse than no offer: it is "double tap to activate" followed by
    /// nothing happening.
    /// <para>
    /// Three answers were wrong when this was written. A calendar day had no row on any target, so it
    /// was static text that nothing could activate. An <see cref="Adjustable"/>'s two swipe actions
    /// sat INSIDE the Android click gate, whose predicate said false for the only role that reaches
    /// them. And a text field was advertised as activatable while <c>ActivatePath</c> searched hit
    /// regions alone, where a field never appears — and then, once that was fixed, over a
    /// <see cref="SheetSurface"/>, whose stop carries neither an entry nor a code surface, because
    /// the repair had enumerated the kinds it knew instead of naming the two answered elsewhere.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryControlTheFrameMakesReachableAffordsWhatItAdvertises()
    {
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new Pressable(new Text("press", TypeRole.Label), () => { }) { Label = "Press" });
        page.Add(new TextEntry("42", _ => { }) { Placeholder = "Search" });
        page.Add(new Adjustable(new Text("x", TypeRole.Label), _ => { }) { Label = "Volume" });
        page.Add(new Pressable(new Text("14", TypeRole.Label), () => { })
            { Label = "14 July", Role = PressableRole.GridCell });
        page.Add(new Pressable(new Text("on", TypeRole.Label), () => { })
            { Label = "Wi-Fi", Role = PressableRole.Switch });
        page.Add(new Pressable(new Text("c", TypeRole.Label), () => { })
            { Label = "Agree", Role = PressableRole.Checkbox });
        // Both editable SURFACES, because both announce as CodeField and they register different
        // kinds of stop — a sample holding only one of them cannot see the other go unanswered.
        page.Add(new CodeSurface(new Text("code", TypeRole.BodyM), new CodeEditorController("x"))
            { Label = "Source" });
        page.Add(new SheetSurface(new Text("sheet", TypeRole.BodyM), new SheetController(rows: 2, cols: 2))
            { Label = "Budget" });

        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 600);
        var frame = host.RenderFrame(new DisplayListBuilder());
        var reachable = frame.HitRegions.Select(region => region.Path)
            .Concat(frame.FocusStops.Select(stop => stop.Path))
            .ToHashSet();

        var reached = host.Semantics().Where(node => reachable.Contains(node.Path)).ToList();

        reached.Select(node => node.Role).Should().Contain(
            [SemanticRole.Button, SemanticRole.TextField, SemanticRole.Slider,
             SemanticRole.GridCell, SemanticRole.Switch, SemanticRole.Checkbox,
             SemanticRole.CodeField],
            "the sample really does put one of each within reach");
        reached.Count(node => node.Role == SemanticRole.CodeField).Should().Be(2,
            "BOTH surfaces that announce as one are in the sample, not just the first");

        foreach (var node in reached)
        {
            var native = NativeRole.Of(node.Role);
            var what = $"'{node.Label}' ({node.Role})";

            (native.Activatable || native.Adjustable).Should().BeTrue(
                $"a user reaches {what}, so a screen reader has to be offered something as well");

            if (native.Activatable)
                host.ActivatePath(node.Path).Should().BeTrue(
                    $"the table tells every bridge {what} can be activated");
            if (native.Adjustable)
                host.AdjustPath(node.Path, +1).Should().BeTrue(
                    $"the table tells every bridge {what} takes the adjust gesture");
        }
    }

    /// <summary>
    /// What "activatable" MEANS for an editable surface, which answering <c>true</c> does not say:
    /// the keyboard is now THEIRS. The three routes have to agree, because landing somewhere is one
    /// behaviour — a pointer, the Tab walk and a screen reader's activate all reach the same state.
    /// <para>
    /// They did not. The pointer route set <c>_textPath</c> (<c>BeginCodeEditing</c>), which is what
    /// <see cref="PhotonHost.CodeTarget"/> and <see cref="PhotonHost.SheetTarget"/> resolve from, and
    /// the Tab walk set a focus RING instead — so Tab onto a code editor looked like arrival and
    /// every keystroke went nowhere. A reader's activate inherited that gap the moment it shared the
    /// landing, and returning <c>true</c> hid it: the first version of this test asked only that.
    /// </para>
    /// </summary>
    [Fact]
    public void ActivatingAnEditableSurfaceHandsItTheKeyboard()
    {
        var code = new CodeEditorController("x");
        var sheet = new SheetController(rows: 2, cols: 2);
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new CodeSurface(new Text("code", TypeRole.BodyM), code) { Label = "Source" });
        page.Add(new SheetSurface(new Text("sheet", TypeRole.BodyM), sheet) { Label = "Budget" });

        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 600);
        var frame = host.RenderFrame(new DisplayListBuilder());

        // The CODE surface: activate, then type — the controller is where the text has to land.
        host.ActivatePath(frame.CodeRegions[0].Path).Should().BeTrue();
        host.CodeTarget.Should().NotBeNull("activating a CodeField makes it the editing target");
        host.TextInput("Z").Should().BeTrue();
        code.Document.Text.Should().Contain("Z", "the keystroke reached the surface, not just its ring");

        // The SPREADSHEET: the same claim, through the other kind of stop — the one that carries
        // neither an entry nor a code surface, and was skipped entirely until this round.
        host.ActivatePath(frame.SheetRegions[0].Path).Should().BeTrue();
        host.SheetTarget.Should().NotBeNull("activating a SheetSurface makes it the editing target");
        host.TextInput("7").Should().BeTrue();
        sheet.Draft.Should().Contain("7");
    }

    /// <summary>
    /// ARRIVING and ENTERING are one thing for a text field and two for a surface that owns the
    /// keyboard — the distinction this landed on the hard way. Tab ARRIVES: the ring shows and the
    /// next Tab travels on. Activating ENTERS, which is what a pointer press does and what a
    /// reader's double tap means.
    /// <para>
    /// Sharing the landing outright looked right and was not: the caret went into the code editor
    /// on Tab, <c>CodeKeymap</c> then took the NEXT Tab as an indent, and the Studio's own walk
    /// caught a focus ring that never came back round. An editor you cannot Tab out of is the same
    /// defect its Escape branch already names.
    /// </para>
    /// </summary>
    [Fact]
    public void TabArrivesAtACodeSurfaceAndOnlyActivateEntersIt()
    {
        var code = new CodeEditorController("x");
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new CodeSurface(new Text("code", TypeRole.BodyM), code) { Label = "Source" });
        page.Add(new Pressable(new Text("after", TypeRole.Label), () => { }) { Label = "After" });

        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        var frame = host.RenderFrame(new DisplayListBuilder());
        var codePath = frame.CodeRegions[0].Path;

        host.FocusNext().Should().BeTrue();
        host.FocusedPath.Should().Be(codePath, "Tab arrives at the surface");
        host.CodeTarget.Should().BeNull("arriving is not entering — the keys still belong to the page");

        host.KeyDown("Tab").Should().BeTrue();
        host.FocusedPath.Should().NotBe(codePath,
            "and the next Tab TRAVELS ON rather than being eaten as an indent");

        host.ActivatePath(codePath).Should().BeTrue();
        host.CodeTarget.Should().NotBeNull("activating enters it, which is what the table advertises");
        host.TextInput("Z").Should().BeTrue();
        code.Document.Text.Should().Contain("Z");
    }

    /// <summary>
    /// The KEYBOARD reaches the same door. Tab arrives and leaves the keys with the page, so Enter
    /// is the only way in for somebody with no pointer — and it ran nothing at all, which made the
    /// ring as far as a keyboard could get into a code editor. Asserted beside the reader's route
    /// rather than apart from it: they are one behaviour through two doors, and the version of this
    /// work that fixed only the reader's would have been the odder half.
    /// </summary>
    [Fact]
    public void EnterOnAnArrivedCodeSurfaceEntersItAsWell()
    {
        var code = new CodeEditorController("x");
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new CodeSurface(new Text("code", TypeRole.BodyM), code) { Label = "Source" });

        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 400);
        host.RenderFrame(new DisplayListBuilder());

        host.FocusNext().Should().BeTrue();
        host.CodeTarget.Should().BeNull();

        host.KeyDown("Enter").Should().BeTrue();
        host.CodeTarget.Should().NotBeNull();
        host.TextInput("Z").Should().BeTrue();
        code.Document.Text.Should().Contain("Z");
    }

    /// <summary>
    /// WHAT THAT PIN SAID WAS MISSING, closed (#255). A <see cref="Link"/> was announced — a role, a
    /// name, a place in reading order — and then reachable by neither Tab nor a screen reader's
    /// activate: it sat in no <c>FocusStops</c> entry, and <c>ActivatePath</c> found no region at its
    /// path. The web gets both from <c>&lt;a href&gt;</c> for nothing, so this was a write-once
    /// promise only one realizer kept.
    /// <para>
    /// The rule is the handoff's, not this test's: "Tab reaches every interactive control"
    /// (Foundations · Keyboard conventions), with activation on Space/Enter/double-tap.
    /// </para>
    /// <para>
    /// Mutation: drop the <c>FocusStop</c> from <c>EmitLink</c> and both halves fail — the stop
    /// assertion by name, and the activate because <c>ActivatePath</c> resolves links through it.
    /// </para>
    /// </summary>
    [Fact]
    public void ALinkIsATabStopAndActivateFollowsIt()
    {
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new Link("/home", new Text("home", TypeRole.Label)));

        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 200);
        var frame = host.RenderFrame(new DisplayListBuilder());
        string? followed = null;
        host.NavigationRequested = destination => followed = destination;

        var link = host.Semantics().Should().ContainSingle(node => node.Role == SemanticRole.Link)
            .Which;
        NativeRole.Of(SemanticRole.Link).Activatable.Should().BeTrue(
            "every platform announces a link as something you follow");

        frame.FocusStops.Should().ContainSingle(stop => stop.Path == link.Path)
            .Which.Destination.Should().Be("/home",
                "the stop carries what following it needs, since the other shape has no node");
        host.ActivatePath(link.Path).Should().BeTrue();
        followed.Should().Be("/home");
    }

    /// <summary>
    /// The keyboard's own half, which costs nothing extra once the stop exists: Tab LANDS and Enter
    /// FOLLOWS, because <c>ActivateFocused</c> ends in <c>ActivatePath</c> — the same seam the
    /// reader's double tap takes. Space too, which the handoff names beside Enter.
    /// </summary>
    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void TabLandsOnALinkAndEnterFollowsIt(string key)
    {
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new Link("/pricing", new Text("pricing", TypeRole.Label)));

        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 200);
        host.RenderFrame(new DisplayListBuilder());
        string? followed = null;
        host.NavigationRequested = destination => followed = destination;

        host.KeyDown("Tab").Should().BeTrue("a link is an interactive control, so Tab reaches it");
        host.KeyDown(key).Should().BeTrue();
        followed.Should().Be("/pricing");
    }

    /// <summary>
    /// THE EMIT WALK AND THE SEMANTICS WALK AGREE ABOUT WHAT ONE LINK IS. <c>Visit(Link)</c>
    /// announces and CONSUMES its subtree, so a paragraph inside a link is not announced — and the
    /// emit walk has to say the same thing, or a run inside that paragraph registers a stop nothing
    /// names. Measured before the fence went on:
    /// <code>
    /// STOPS            r/0 -> /outer ,  r/0/0#0 -> /inner
    /// ANNOUNCED-LINKS  r/0
    /// </code>
    /// A link IS the stop for its subtree, the same shape <c>Adjustable</c> and <c>Navigable</c>
    /// use, and for the same reason. Found by the review's SUMMARY, with no comment posted for it.
    /// <para>Mutation: drop <c>WithoutFocusStops</c> from <c>EmitLink</c> and the counts diverge
    /// again — two stops, one announcement.</para>
    /// </summary>
    [Fact]
    public void ALinkOverALinkedRunIsOneStopAndOneAnnouncement()
    {
        var paragraph = new Text("", TypeRole.BodyM)
        {
            Spans = [new TextRun("read the "), new TextRun("guide") { Destination = "/inner" }],
        };
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new Link("/outer", paragraph));

        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 200);
        var frame = host.RenderFrame(new DisplayListBuilder());

        var announced = host.Semantics().Where(node => node.Role == SemanticRole.Link).ToList();
        announced.Should().ContainSingle("the outer link consumes what it wraps").Which
            .Path.Should().Be("r/0");

        frame.FocusStops.Where(stop => stop.Destination is not null).Should()
            .ContainSingle("a stop the reader never names is an offer nothing performs")
            .Which.Destination.Should().Be("/outer");
    }
}
