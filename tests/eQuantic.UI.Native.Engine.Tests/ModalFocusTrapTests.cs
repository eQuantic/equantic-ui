using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A modal layer holds the Tab walk (#1066). While a modal and open layer is shown, Tab and Shift+Tab walk
/// the stops of the topmost such layer only and wrap inside it, as the web's focus trap cycles
/// (<c>dom/focus-trap.ts</c>). The host walked every stop of the frame in paint order, so Tab from a
/// dialog's last control landed on the page's button behind the scrim. The control that has the focus
/// still answers Tab first: the <c>modal-focus</c> scenarios run here as they run in the browser.
/// </summary>
public class ModalFocusTrapTests
{
    private static PhotonHost Host(VisualNode root) =>
        new(root, PhotonTheme.Instance, ThemeMode.Light, 600, 400)
        {
            TextRasterizer = new FixedWidthRasterizer(),
            Density = Density.Compact,
            SmoothScroll = false,
        };

    private static RealizeResult Settle(PhotonHost host)
    {
        host.RenderFrame(new DisplayListBuilder());
        return host.RenderFrame(new DisplayListBuilder());
    }

    private static Pressable Button(string label) =>
        new(new Box(new BoxStyle { Width = SizeValue.Fixed(80), Height = SizeValue.Fixed(32) }), () => { })
        {
            Label = label,
        };

    private static Column Controls(params VisualNode[] children)
    {
        var column = new Column(gap: 8);
        foreach (var child in children) column.Add(child);
        return column;
    }

    /// <summary>A page whose button sits behind <paramref name="layers"/>, laid out and settled.</summary>
    private static PhotonHost Page(params VisualNode[] layers)
    {
        var page = new Stack();
        page.Add(Button("Behind"));
        foreach (var layer in layers) page.Add(layer);
        var host = Host(page);
        Settle(host);
        return host;
    }

    private static Overlay Dialog(params VisualNode[] controls) => new(Controls(controls)) { Label = "Dialog" };

    /// <summary>The labels Tab lands on, <paramref name="presses"/> times, from where the focus is.</summary>
    private static List<string?> Walk(PhotonHost host, int presses, KeyModifiers modifiers = KeyModifiers.None)
    {
        var landed = new List<string?>();
        for (var i = 0; i < presses; i++)
        {
            host.KeyDown("Tab", modifiers).Should().BeTrue("a modal layer's Tab is always the trap's");
            Settle(host);
            landed.Add(host.Focused?.Label);
        }
        return landed;
    }

    [Fact]
    public void Tab_WrapsInsideTheDialog_AndNeverReachesTheControlBehindIt()
    {
        var host = Page(Dialog(Button("First"), Button("Second")));

        Walk(host, 4).Should().Equal(["First", "Second", "First", "Second"]);
    }

    [Fact]
    public void ShiftTab_WrapsInsideTheDialog_TheOtherWay()
    {
        var host = Page(Dialog(Button("First"), Button("Second")));

        Walk(host, 4, KeyModifiers.Shift).Should().Equal(["Second", "First", "Second", "First"]);
    }

    [Fact]
    public void ANonModalLayer_TrapsNothing()
    {
        var host = Page(new Overlay(Controls(Button("Toast"))) { Modal = false });

        Walk(host, 3).Should().Equal(["Behind", "Toast", "Behind"], "a toast is no dialog, and the web traps none");
    }

    [Fact]
    public void TheTopmostDialog_HoldsTheKeyboard_OverTheOneUnderIt()
    {
        var host = Page(Dialog(Button("Under")), Dialog(Button("Top"), Button("Confirm")));

        Walk(host, 3).Should().Equal(["Top", "Confirm", "Top"]);
    }

    [Fact]
    public void ToastsOverADialog_StayOutOfItsWalk()
    {
        var host = Page(Dialog(Button("First"), Button("Second")), new Overlay(Controls(Button("Toast"))) { Modal = false });

        Walk(host, 3).Should().Equal(["First", "Second", "First"], "the web's trap holds the dialog's controls alone");
    }

    [Fact]
    public void AMenuOpenInADialog_IsInsideItsWalk()
    {
        // Photon builds the menu's panel in a layer of its own, which the web never does (its panel sits in
        // the dialog): the panel's layer is built under the dialog's, so the trap holds it.
        var menu = new Anchored(Button("Pick"), Controls(Button("Option"))) { Open = true };
        var host = Page(Dialog(Button("First"), menu));

        Walk(host, 4).Should().Equal(["First", "Pick", "Option", "First"]);
    }

    [Fact]
    public void ADialogWithNoControl_HoldsTheKeyboard_AndWalksNothingBehindIt()
    {
        var host = Page(Dialog(new Box(new BoxStyle { Width = SizeValue.Fixed(80), Height = SizeValue.Fixed(32) })));

        host.KeyDown("Tab").Should().BeTrue("the layer holds the focus, as the web's trap focuses the layer itself");
        Settle(host);

        host.Focused.Should().BeNull("nothing in the dialog takes the focus, and nothing behind it may");
    }

    // ---- the modal-focus scenarios, on Photon -------------------------------------------------------
    // The control that has the focus answers Tab first, and the trap cycles only a Tab nothing took
    // (openspec/specs/modal-focus). The browser proves them in focus-trap's spec; these are the host's.

    /// <summary>A dialog with a button and a code editor, the editor <paramref name="last"/> or first,
    /// with the keyboard in the editor.</summary>
    private static (PhotonHost Host, CodeEditor Editor) EditorInADialog(bool last, string text)
    {
        var editor = new CodeEditor(text, "csharp")
        {
            ShowLineNumbers = false,
            Height = SizeValue.Fixed(120),
        };
        var host = Page(last ? Dialog(Button("Button"), editor) : Dialog(editor, Button("Button")));
        var region = host.LastFrame!.CodeRegions.Single().Bounds;
        host.PressDown(region.X + region.Width / 2, region.Y + region.Height / 2);
        host.PressUp(region.X + region.Width / 2, region.Y + region.Height / 2);
        Settle(host);
        host.CodeTarget.Should().NotBeNull("a press on the code gives it the keyboard");
        return (host, editor);
    }

    [Fact]
    public void ACodeEditorLastInADialog_IndentsOnTab_AndKeepsTheKeyboard()
    {
        var (host, editor) = EditorInADialog(last: true, "x");

        host.KeyDown("Tab").Should().BeTrue();

        editor.Editor.Document.Line(0).Should().NotBe("x", "the editor takes the Tab, and indents at the caret");
        host.CodeTarget.Should().NotBeNull("the editor keeps the keyboard");
    }

    [Fact]
    public void ACodeEditorFirstInADialog_OutdentsOnShiftTab_AndKeepsTheKeyboard()
    {
        var (host, editor) = EditorInADialog(last: false, "    x");

        host.KeyDown("Tab", KeyModifiers.Shift).Should().BeTrue();

        editor.Editor.Document.Line(0).Should().Be("x", "the line is outdented");
        host.CodeTarget.Should().NotBeNull("the editor keeps the keyboard");
    }

    [Fact]
    public void AfterEscape_TabFromACodeEditorLastInADialog_CyclesToTheDialogsFirstControl()
    {
        var (host, editor) = EditorInADialog(last: true, "x");

        host.KeyDown("Escape");
        host.KeyDown("Tab").Should().BeTrue();

        (host.Focused?.Label).Should().Be("Button", "the dialog cycles, and the page behind it is not the next stop");
        editor.Editor.Document.Line(0).Should().Be("x", "the line is left as it was");
    }

    [Fact]
    public void AfterEscape_ShiftTabFromACodeEditorFirstInADialog_CyclesToTheDialogsLastControl()
    {
        var (host, editor) = EditorInADialog(last: false, "    x");

        host.KeyDown("Escape");
        host.KeyDown("Tab", KeyModifiers.Shift).Should().BeTrue();

        (host.Focused?.Label).Should().Be("Button", "the dialog cycles, and the page behind it is not the next stop");
        editor.Editor.Document.Line(0).Should().Be("    x", "the line is left as it was");
    }
}
