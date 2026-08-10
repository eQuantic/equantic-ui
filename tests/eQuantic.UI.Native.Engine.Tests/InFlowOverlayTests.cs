using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The native half of <c>InFlow</c> — the same node, the same five components, drawn as GPU
/// commands. A gallery that showed a dialog on a documentation site and a blank on the desktop app
/// would be two answers to one question.
/// </summary>
public class InFlowOverlayTests
{
    private static readonly IAppTheme Theme = PhotonTheme.Instance;

    private static DisplayList Draw(VisualNode node)
    {
        var builder = new DisplayListBuilder();
        PhotonRealizer.Realize(node, 400, 300, Theme, ThemeMode.Light, builder);
        return builder.Build();
    }

    private static bool Paints(DisplayList list, ColorToken token)
    {
        var color = token.Resolve(ThemeMode.Light);
        foreach (var command in list.Commands)
            if (command.Kind == DrawCommandKind.FillRRect && command.Paint.Color == color) return true;
        return false;
    }

    private static Dialog ADialog() =>
        new("Delete this?", "It cannot be undone.", [new DialogAction("Delete")],
            dismissible: true, onDismiss: () => { });

    [Fact]
    public void ADialogInFlow_DrawsItsCardWithoutItsScrim()
    {
        Paints(Draw(ADialog()), Theme.Scrim).Should().BeTrue("a real dialog covers the screen");
        Paints(Draw(new InFlow(ADialog())), Theme.Scrim).Should().BeFalse("in the flow there is no layer");
        Paints(Draw(new InFlow(ADialog())), Theme.Surface).Should().BeTrue("the card is still drawn");
    }

    /// <summary>In the flow there is no open or closed: the panel is placed, therefore it is
    /// there.</summary>
    [Fact]
    public void AClosedDrawerInFlow_StillDrawsItsPanel()
    {
        var drawer = new Drawer(new Text("Menu", TypeRole.BodyM), open: false);

        Paints(Draw(drawer), Theme.Surface).Should().BeFalse("closed, it is nothing");
        Paints(Draw(new InFlow(drawer)), Theme.Surface).Should().BeTrue();
    }

    /// <summary>The intent ends with the node — a dialog opened for real beside a previewed one
    /// still covers the screen.</summary>
    [Fact]
    public void TheIntentEndsWithTheNode()
    {
        var column = new Column(gap: 8);
        column.Add(new InFlow(ADialog()));
        column.Add(ADialog());

        Paints(Draw(column), Theme.Scrim).Should().BeTrue("the sibling is still a real dialog");
    }
}
