using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Filling in a form without a mouse: Tab between the controls, type, correct a character, submit.
/// <para>
/// EVERY test here renders a frame between the keystrokes, and that is the point rather than
/// ceremony. Each keystroke calls back into the app, the app calls SetState, and Build hands back
/// brand new nodes — so anything the host remembers as an OBJECT is pointing at a corpse by the
/// time the next key arrives. A test that presses two keys against one frame passes on code where
/// the second key goes nowhere, which is exactly how a form that nobody could fill in shipped.
/// </para>
/// </summary>
public class KeyboardTests
{
    /// <summary>A small form that behaves like a real one: it rebuilds on every change.</summary>
    private sealed class Form : Primitives.StatefulComponent
    {
        public string Name = "";
        public string Email = "";
        public int Saved;
        public int Submitted;
        public readonly List<string> FocusEvents = [];

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
            column.Add(new TextEntry(Name, value => SetState(() => Name = value))
            {
                Placeholder = "Full name",
                OnFocusChanged = focused => FocusEvents.Add($"name:{focused}"),
                OnSubmit = () => Submitted++,
            });
            column.Add(new TextEntry(Email, value => SetState(() => Email = value))
            {
                Placeholder = "Email",
                OnFocusChanged = focused => FocusEvents.Add($"email:{focused}"),
            });
            column.Add(new Button("Save", onPressed: () => SetState(() => Saved++)));
            return column;
        }
    }

    private static (PhotonHost Host, Form Form) Open()
    {
        var form = new Form();
        var host = new PhotonHost(form, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());
        return (host, form);
    }

    /// <summary>Types the way a keyboard does: a key, a frame, a key, a frame.</summary>
    private static void Type(PhotonHost host, string text)
    {
        foreach (var character in text)
        {
            host.TextInput(character.ToString());
            host.RenderFrame(new DisplayListBuilder());
        }
    }

    private static void Press(PhotonHost host, string key, KeyModifiers modifiers = KeyModifiers.None)
    {
        host.KeyDown(key, modifiers);
        host.RenderFrame(new DisplayListBuilder());
    }

    [Fact]
    public void TabReachesTheFieldsAndTheButton_InTreeOrder()
    {
        var (host, form) = Open();

        Press(host, "Tab");
        host.TextTarget?.Placeholder.Should().Be("Full name");

        Press(host, "Tab");
        host.TextTarget?.Placeholder.Should().Be("Email", "Tab must not skip the second field");

        Press(host, "Tab");
        host.TextTarget.Should().BeNull("the button is not a text field");
        host.Focused.Should().NotBeNull("Tab reaches buttons and fields as ONE sequence");

        // And the button it reached is the live one: pressing it runs the app's handler.
        Press(host, "Enter");
        form.Saved.Should().Be(1);
    }

    [Fact]
    public void TabGoesBackwards_WithShift()
    {
        var (host, _) = Open();

        Press(host, "Tab");
        Press(host, "Tab");
        host.TextTarget?.Placeholder.Should().Be("Email");

        Press(host, "Tab", KeyModifiers.Shift);
        host.TextTarget?.Placeholder.Should().Be("Full name", "overshooting a field must be undoable");
    }

    [Fact]
    public void TypingReachesTheApp_AcrossTheRebuildEachKeystrokeCauses()
    {
        var (host, form) = Open();
        Press(host, "Tab");

        Type(host, "Ana");

        // The whole word, not just its first letter: after the first key the app called SetState and
        // every node in the tree was replaced. This is the assertion the old code fails.
        form.Name.Should().Be("Ana");
        host.CaretIndex.Should().Be(3, "the caret advances with the text");
    }

    [Fact]
    public void BackspaceAndArrows_EditWhereTheCaretIs()
    {
        var (host, form) = Open();
        Press(host, "Tab");
        Type(host, "Anaa");

        Press(host, "Backspace");
        form.Name.Should().Be("Ana");

        Press(host, "ArrowLeft");
        Press(host, "ArrowLeft");
        Type(host, "i");
        form.Name.Should().Be("Aina", "a character lands where the caret is, not at the end");

        Press(host, "Home");
        Type(host, "M");
        form.Name.Should().Be("MAina");

        Press(host, "End");
        Type(host, "!");
        form.Name.Should().Be("MAina!");
    }

    [Fact]
    public void BackspaceAtTheStart_IsSwallowedRatherThanEscaping()
    {
        var (host, form) = Open();
        Press(host, "Tab");

        host.KeyDown("Backspace").Should().BeTrue(
            "a field claims Backspace even with nothing to delete — otherwise it reaches the app as Back");
        form.Name.Should().BeEmpty();
    }

    [Fact]
    public void EnterSubmits_AndLeavesTheField()
    {
        var (host, form) = Open();
        Press(host, "Tab");
        Type(host, "Ana");

        Press(host, "Enter");

        form.Submitted.Should().Be(1);
        host.TextTarget.Should().BeNull("staying in the field after Enter hides whether anything happened");
    }

    [Fact]
    public void OnlyOneFieldIsEverFocused()
    {
        var (host, form) = Open();

        Press(host, "Tab");
        Press(host, "Tab");

        form.FocusEvents.Should().Equal(["name:True", "name:False", "email:True"],
            "the field being left has to hear that it lost focus, or every box it passed keeps its ring");
    }

    [Fact]
    public void EscapeLeavesTheField()
    {
        var (host, _) = Open();
        Press(host, "Tab");
        host.TextTarget.Should().NotBeNull();

        Press(host, "Escape");
        host.TextTarget.Should().BeNull();
    }

    [Fact]
    public void AnAppsOwnChordWins_OverTheFocusedControl()
    {
        var opened = 0;
        var field = new TextEntry("", _ => { }) { Placeholder = "Search" };
        var root = new Shortcut(field, new KeyChord("k", KeyModifiers.Command), () => opened++);

        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());
        host.KeyDown("Tab");
        host.RenderFrame(new DisplayListBuilder());

        host.KeyDown("k", KeyModifiers.Command).Should().BeTrue();
        opened.Should().Be(1, "⌘K belongs to the app even while a field has the caret");
    }

    [Fact]
    public void SpaceRunsTheFocusedButton_RatherThanTypingIntoTheLastField()
    {
        var (host, form) = Open();
        Press(host, "Tab");   // name
        Press(host, "Tab");   // email
        Press(host, "Tab");   // button

        Press(host, " ");

        form.Saved.Should().Be(1);
        form.Email.Should().BeEmpty("the space must not fall through into the field Tab just left");
    }
}

/// <summary>
/// What the pointer looks like where it is. The web has answered this since it had a mouse, and the
/// native window answered "arrow" everywhere — over buttons, over fields, over disabled controls.
/// </summary>
public class CursorTests
{
    private static PhotonHost Open()
    {
        var column = new Column(gap: Space.S4) { Padding = EdgeInsets.All(Space.S4), Width = SizeValue.Fill };
        column.Add(new Button("Save", onPressed: () => { }));
        column.Add(new TextEntry("", _ => { }) { Placeholder = "Email" });
        column.Add(new Button("Nope") { Disabled = true });
        var host = new PhotonHost(column, PhotonTheme.Instance, ThemeMode.Light, 300, 300);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    [Fact]
    public void EachKindOfSurfaceAnswersWithItsOwnShape()
    {
        var host = Open();
        var frame = host.RenderFrame(new DisplayListBuilder());

        var button = frame.HitRegions.First(r => !r.Node.Disabled).Bounds;
        host.CursorAt(button.Center.X, button.Center.Y).Should().Be(CursorShape.Pointer);

        var field = frame.TextRegions.Single().Bounds;
        host.CursorAt(field.Center.X, field.Center.Y).Should().Be(CursorShape.Text);

        var disabled = frame.HitRegions.First(r => r.Node.Disabled).Bounds;
        host.CursorAt(disabled.Center.X, disabled.Center.Y).Should().Be(CursorShape.NotAllowed,
            "the pointer is the only warning before a click that does nothing");

        host.CursorAt(2, 2).Should().Be(CursorShape.Default, "empty space is not a control");
    }
}
