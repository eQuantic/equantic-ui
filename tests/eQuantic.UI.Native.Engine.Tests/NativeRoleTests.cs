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
    /// The one control this cannot yet be said of, pinned so the day it changes somebody is told.
    /// A <see cref="Link"/> IS announced — it has a role, a name and a place in reading order — and
    /// it is reachable by neither Tab nor a screen reader's activate: <c>LinkRegion</c> carries a
    /// destination and no path, deliberately (a linked run inside a sentence is a rectangle and a
    /// string, not a node), so there is nothing for either route to name. Closing it means deciding
    /// what identity a linked RUN has, which is an input-route change and not this table's (#255).
    /// </summary>
    [Fact]
    public void ALinkIsAnnouncedAndReachedByNeitherTabNorActivate()
    {
        var page = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        page.Add(new Link("/home", new Text("home", TypeRole.Label)));

        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 200);
        var frame = host.RenderFrame(new DisplayListBuilder());

        var link = host.Semantics().Should().ContainSingle(node => node.Role == SemanticRole.Link)
            .Which;
        NativeRole.Of(SemanticRole.Link).Activatable.Should().BeTrue(
            "every platform announces a link as something you follow");

        frame.FocusStops.Should().NotContain(stop => stop.Path == link.Path);
        host.ActivatePath(link.Path).Should().BeFalse();
    }
}
