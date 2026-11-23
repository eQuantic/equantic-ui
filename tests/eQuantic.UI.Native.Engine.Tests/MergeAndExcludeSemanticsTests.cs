using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Testing;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A SUBTREE IS HEARD AS ONE, OR NOT AT ALL (#1567): <see cref="MergeSemantics"/> and
/// <see cref="ExcludeSemantics"/>, read on Photon through <see cref="PhotonHost.Semantics"/> and on the web
/// through <see cref="ComponentTester"/>, which reads the markup the server writes. The parity suite holds the
/// two readings alike; this one says what each of them IS, and that the one stop a merge becomes is the
/// control a person presses.
/// </summary>
public class MergeAndExcludeSemanticsTests
{
    private const float Width = 800f;
    private const float Height = 600f;
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    [Fact]
    public void ASwitchBesideItsLabel_IsOneSwitchNamedByTheRow_AndPressingTheStopPressesTheSwitch()
    {
        var presses = 0;
        VisualNode Build() => new MergeSemantics(Line(new Text("Wi-Fi", TypeRole.BodyM), new Switch(true, () => presses++)));

        var host = Photon(Build());
        var stop = host.Semantics().Should().ContainSingle().Subject;
        stop.Role.Should().Be(SemanticRole.Switch);
        stop.Label.Should().Be("Wi-Fi");
        stop.Checked.Should().Be(SemanticCheck.On);
        // The box a reader outlines is the whole row it hears, not the track alone.
        stop.Bounds.Width.Should().BeGreaterThan(60, "the merged stop has the row's bounds");
        host.Activate(stop.Id).Should().BeTrue("the bridge's activate reaches the switch the stop is");
        presses.Should().Be(1);

        using var web = Web(Build());
        var heard = web.Semantics.Should().ContainSingle().Subject;
        (heard.Role, heard.Name, heard.Checked).Should().Be((SemanticRole.Switch, "Wi-Fi", SemanticCheck.On));
        web.Tap(SemanticRole.Switch, "Wi-Fi");
        presses.Should().Be(2, "on the web the stop named by the row is the switch's own element");
    }

    [Fact]
    public void OnTheWeb_TheControlNamesItselfByThePartsItHides_InReadingOrder()
    {
        using var web = Web(new MergeSemantics(Line(
            new Checkbox(true, label: "Agree"), new Text("to the terms", TypeRole.BodyM))));

        web.Html.Should().MatchRegex("aria-labelledby=\"(eq-merge-[a-z0-9]+-c) (eq-merge-[a-z0-9]+-0)\"",
            "the checkbox lists itself first, where it stands, then the text after it");
        web.Html.Should().MatchRegex("id=\"eq-merge-[a-z0-9]+-0\"[^>]*aria-hidden=\"true\"|aria-hidden=\"true\"[^>]*id=\"eq-merge-[a-z0-9]+-0\"",
            "the text is hidden as a stop and read as part of the name");
        var heard = web.Semantics.Should().ContainSingle().Subject;
        (heard.Role, heard.Name, heard.Checked).Should().Be((SemanticRole.Checkbox, "Agree to the terms", SemanticCheck.On));
    }

    [Fact]
    public void AMergeWithNoControl_IsOneGroupNamedByWhatItSays()
    {
        VisualNode Build() => new MergeSemantics(Line(
            new Icon(Icons.Check, IconSize.Sm, label: "Paid"), new Text("Total", TypeRole.BodyM), new Text("42 EUR", TypeRole.BodyM)));

        var photon = Photon(Build()).Semantics().Should().ContainSingle().Subject;
        (photon.Role, photon.Label).Should().Be((SemanticRole.Group, "Paid Total 42 EUR"));

        using var web = Web(Build());
        var heard = web.Semantics.Should().ContainSingle().Subject;
        (heard.Role, heard.Name).Should().Be((SemanticRole.Group, "Paid Total 42 EUR"));
    }

    [Fact]
    public void ASecondControl_StaysAStopOfItsOwn_AfterTheMergedOne()
    {
        VisualNode Build() => new MergeSemantics(Line(
            new Text("Volume", TypeRole.BodyM), new Slider(0.5f, _ => { }), new Button("Mute")));

        Photon(Build()).Semantics().Select(stop => (stop.Role, stop.Label)).Should().Equal(
            (SemanticRole.Slider, "Volume"), (SemanticRole.Button, "Mute"));

        using var web = Web(Build());
        web.Semantics.Select(stop => (stop.Role, stop.Name)).Should().Equal(
            (SemanticRole.Slider, "Volume"), (SemanticRole.Button, "Mute"));
    }

    [Fact]
    public void WhatAReaderPassesOver_StaysOutOfTheMergedName()
    {
        VisualNode Build() => new MergeSemantics(Line(
            new ExcludeSemantics(new Text("JD", TypeRole.BodyM)),
            new Text("Initials", TypeRole.BodyM) { ExcludeFromSemantics = true },
            new Text("Jane Doe", TypeRole.BodyM)));

        Photon(Build()).Semantics().Select(stop => (stop.Role, stop.Label)).Should().Equal((SemanticRole.Group, "Jane Doe"));

        using var web = Web(Build());
        web.Semantics.Select(stop => (stop.Role, stop.Name)).Should().Equal((SemanticRole.Group, "Jane Doe"));
    }

    [Fact]
    public void AnExcludedSubtree_IsHeardByNoOne_AndOneNotExcludingIsReadAsIfItWereNotThere()
    {
        VisualNode Build(bool excluding) => Line(
            new ExcludeSemantics(Line(new Icon(Icons.Check, IconSize.Sm, label: "Done"), new Text("Decoration", TypeRole.BodyM)))
            {
                Excluding = excluding,
            },
            new Text("Shown", TypeRole.BodyM));

        Photon(Build(true)).Semantics().Select(stop => stop.Label).Should().Equal("Shown");
        Photon(Build(false)).Semantics().Select(stop => stop.Label).Should().Equal("Done", "Decoration", "Shown");

        using (var web = Web(Build(true)))
            web.Semantics.Select(stop => stop.Name).Should().Equal("Shown");
        using (var web = Web(Build(false)))
            web.Semantics.Select(stop => stop.Name).Should().Equal("Done", "Decoration", "Shown");
    }

    [Fact]
    public void AnExclusion_LeavesTheControlsInsideItPressable_ThoughNoReaderHearsThem()
    {
        var presses = 0;
        var host = Photon(new ExcludeSemantics(new Pressable(new Text("Hidden", TypeRole.BodyM), () => presses++)));

        host.Semantics().Should().BeEmpty("a reader passes over what is excluded, whatever it holds");
        // Pressing works as before: excluding changes what is heard, never what is done.
        host.FocusNext().Should().BeTrue("the control inside is still a Tab stop");
        host.Activate(host.LastFrame!.FocusStops.Should().ContainSingle().Subject.Id).Should().BeTrue();
        presses.Should().Be(1);
    }

    private static PhotonHost Photon(VisualNode root)
    {
        var host = new PhotonHost(root, Theme, ThemeMode.Light, Width, Height);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    private static ComponentTester Web(VisualNode root)
    {
        var tester = new ComponentTester(Theme, width: Width);
        tester.Pump(root);
        return tester;
    }

    private static Row Line(params VisualNode[] children)
    {
        var row = new Row(gap: 8);
        foreach (var child in children) row.Add(child);
        return row;
    }
}
