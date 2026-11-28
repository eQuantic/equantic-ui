using eQuantic.UI.Native.Components;
using eQuantic.UI.Primitives;
using eQuantic.UI.Testing;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A part beside two fields it could name is refused, rather than naming the first, with ONE sentence on every target
/// (#1916): the web's lowering here, Photon's semantics here, and the browser's lowering in <c>naming-parts.spec.ts</c>.
/// A form composed by hand is then told to give each field its own column, instead of having the wrong field named in
/// silence.
/// </summary>
public class NamingPartsRefusalTests
{
    private const string Refusal =
        "A LabelPart reaches a column holding 2 controls it could name, and it names none of them: give each field its "
        + "own Column or Group, with its label and its control inside (#1916).";

    private static VisualNode TwoFieldsOneLabel()
    {
        var column = new Column(gap: 8);
        column.Add(new LabelPart(new Text("Email", TypeRole.Label)));
        column.Add(new TextEntry("", _ => { }));
        column.Add(new TextEntry("", _ => { }));
        return column;
    }

    [Fact]
    public void TheWeb_RefusesAPartBesideTwoFields()
    {
        using var tester = new ComponentTester(PhotonTheme.Instance);
        var pump = () => tester.Pump(TwoFieldsOneLabel());
        pump.Should().Throw<InvalidOperationException>().Which.Message.Should().Be(Refusal);
    }

    [Fact]
    public void Photon_RefusesAPartBesideTwoFields_InTheWebsWords()
    {
        var host = new PhotonHost(TwoFieldsOneLabel(), PhotonTheme.Instance, ThemeMode.Light, 800, 600);
        host.RenderFrame(new DisplayListBuilder());
        var read = () => host.Semantics();
        read.Should().Throw<InvalidOperationException>().Which.Message.Should().Be(Refusal);
    }

    [Fact]
    public void AFieldThatNamesItself_IsNoCandidate_SoOneLabelBesideItAndAnotherIsNoRefusal()
    {
        var column = new Column(gap: 8);
        column.Add(new LabelPart(new Text("Email", TypeRole.Label)));
        column.Add(new TextEntry("", _ => { }));
        column.Add(new TextEntry("", _ => { }) { Label = "Phone" });

        var host = new PhotonHost(column, PhotonTheme.Instance, ThemeMode.Light, 800, 600);
        host.RenderFrame(new DisplayListBuilder());
        host.Semantics().Where(stop => stop.Role == SemanticRole.TextField).Select(stop => stop.Label)
            .Should().Equal("Email", "Phone");
    }
}
