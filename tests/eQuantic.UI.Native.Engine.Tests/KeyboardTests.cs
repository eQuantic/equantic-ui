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

/// <summary>
/// Tab through a form that is taller than its window.
/// <para>
/// The clip rule that keeps a pointer from clicking what it cannot see does NOT apply to the
/// keyboard: with it applied, seven of these twelve fields had no tab stop at all and the order
/// looped over the five that happened to be showing — half a form unreachable without a mouse, and
/// nothing on screen to suggest it.
/// </para>
/// </summary>
public class ScrolledFocusTests
{
    private const int Fields = 12;

    private static (PhotonHost Host, List<string> Values) Open()
    {
        var values = new List<string>();
        var column = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        for (var i = 0; i < Fields; i++)
        {
            var index = i;
            column.Add(new TextEntry("", value => values.Add($"{index}:{value}")) { Placeholder = $"field {index}" });
        }
        var host = new PhotonHost(new ScrollView(column), PhotonTheme.Instance, ThemeMode.Light, 300, 200);
        host.RenderFrame(new DisplayListBuilder());
        return (host, values);
    }

    [Fact]
    public void EveryFieldHasATabStop_IncludingTheOnesBelowTheFold()
    {
        var (host, _) = Open();
        var frame = host.RenderFrame(new DisplayListBuilder());

        frame.FocusStops.Should().HaveCount(Fields, "Tab has to reach the whole form, not the visible part of it");
    }

    [Fact]
    public void TabbingPastTheFold_ScrollsTheFieldIntoView()
    {
        var (host, values) = Open();

        // Far enough down that it started off screen. The clock advances because the scroll GLIDES:
        // frames all stamped with the same instant would leave it forever mid-flight.
        var now = 0f;
        for (var i = 0; i <= 8; i++)
        {
            host.KeyDown("Tab");
            host.RenderFrame(new DisplayListBuilder(), now += 16);
        }

        host.TextTarget?.Placeholder.Should().Be("field 8");

        for (var i = 0; i < 60; i++) host.RenderFrame(new DisplayListBuilder(), now += 16);
        var frame = host.RenderFrame(new DisplayListBuilder(), now += 16);
        var visible = frame.TextRegions.Select(r => r.Entry.Placeholder).ToArray();
        visible.Should().Contain("field 8", "the view has to follow the caret, or it blinks off screen");

        // And it is genuinely editable once there.
        host.TextInput("x");
        values.Should().Contain("8:x");
    }
}

/// <summary>A field that asked for the caret — the search box of a panel that just opened.</summary>
public class AutofocusTests
{
    private static PhotonHost Open(bool autofocus)
    {
        var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
        column.Add(new TextEntry("", _ => { }) { Placeholder = "Search", Autofocus = autofocus });
        column.Add(new TextEntry("", _ => { }) { Placeholder = "Other" });
        var host = new PhotonHost(column, PhotonTheme.Instance, ThemeMode.Light, 300, 200);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    [Fact]
    public void AFieldThatAsksForTheCaret_GetsIt()
    {
        Open(autofocus: true).TextTarget?.Placeholder.Should().Be("Search");
        Open(autofocus: false).TextTarget.Should().BeNull("nothing asked");
    }

    [Fact]
    public void AndCanStillBeLeft()
    {
        var host = Open(autofocus: true);

        host.KeyDown("Escape");
        host.RenderFrame(new DisplayListBuilder());

        host.TextTarget.Should().BeNull("honouring it every frame would make the field impossible to leave");
    }
}

/// <summary>
/// Escape closes what is on top. Universal enough that its absence reads as the app being stuck:
/// a dialog you can only leave with the mouse, in a window you opened with the keyboard.
/// </summary>
public class EscapeDismissTests
{
    private static PhotonHost Open(VisualNode root) 
    {
        var host = new PhotonHost(root, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());
        return host;
    }

    [Fact]
    public void EscapeClosesADismissibleDialog()
    {
        var closed = 0;
        var host = Open(new Dialog("Delete this?", "It cannot be undone.", [new DialogAction("Delete", () => { })], dismissible: true, onDismiss: () => closed++));

        host.KeyDown("Escape").Should().BeTrue();
        closed.Should().Be(1);
    }

    [Fact]
    public void ButNotOneThatRefusesToBeDismissed()
    {
        var closed = 0;
        var host = Open(new Dialog("Confirm payment", "€40 will be charged.", [new DialogAction("Pay", () => { })], dismissible: false, onDismiss: () => closed++));

        host.KeyDown("Escape");
        closed.Should().Be(0, "a dialog that says it cannot be dismissed means it");
    }

    [Fact]
    public void TheTOPMOSTLayerTakesTheKey()
    {
        var dialogClosed = 0;
        var sheetClosed = 0;
        var stack = new Stack { Width = SizeValue.Fill, Height = SizeValue.Fill };
        stack.Add(new Dialog("Under", "Body", [new DialogAction("Ok", () => { })], dismissible: true, onDismiss: () => dialogClosed++));
        stack.Add(new BottomSheet(new Text("Over"), onDismiss: () => sheetClosed++));

        var host = Open(stack);
        host.KeyDown("Escape");

        sheetClosed.Should().Be(1);
        dialogClosed.Should().Be(0, "the one on top is the one the user is looking at");
    }

    [Fact]
    public void AnAnchoredPanelClosesToo()
    {
        var closed = 0;
        var anchored = new Anchored(new Button("Menu"), new Text("Item"))
        {
            Open = true,
            OnDismiss = () => closed++,
        };
        var host = Open(anchored);

        host.KeyDown("Escape").Should().BeTrue();
        closed.Should().Be(1, "a menu opened with the keyboard has to close with it");
    }
}

/// <summary>
/// Selecting text — which the field could not do at all: a click landed the caret at the end of
/// whatever was there, and there was no way to select a word, let alone replace one.
/// </summary>
public class SelectionTests
{
    private sealed class Field : Primitives.StatefulComponent
    {
        public string Value = "hello world";

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: 0) { Width = SizeValue.Fill };
            column.Add(new TextEntry(Value, v => SetState(() => Value = v)) { Placeholder = "Name" });
            return column;
        }
    }

    private static (PhotonHost Host, Field Field, Rect Bounds) Open()
    {
        var field = new Field();
        var host = new PhotonHost(field, PhotonTheme.Instance, ThemeMode.Light, 400, 100);
        var frame = host.RenderFrame(new DisplayListBuilder());
        return (host, field, frame.TextRegions.Single().Bounds);
    }

    private static void Press(PhotonHost host, string key, KeyModifiers modifiers = KeyModifiers.None)
    {
        host.KeyDown(key, modifiers);
        host.RenderFrame(new DisplayListBuilder());
    }

    [Fact]
    public void AClickLandsTheCaretWhereItWasAimed()
    {
        var (host, _, bounds) = Open();

        // Far left: before the first character. Far right: after the last.
        host.PressDown(bounds.X + 1, bounds.Center.Y);
        host.PressUp(bounds.X + 1, bounds.Center.Y);
        host.CaretIndex.Should().Be(0);

        host.PressDown(bounds.X + bounds.Width - 1, bounds.Center.Y);
        host.PressUp(bounds.X + bounds.Width - 1, bounds.Center.Y);
        host.CaretIndex.Should().Be("hello world".Length, "past the end of the text is the end of it");
    }

    [Fact]
    public void DraggingSelects_AndTypingReplacesWhatWasSelected()
    {
        var (host, field, bounds) = Open();

        host.PressDown(bounds.X + 1, bounds.Center.Y);
        host.PointerMove(bounds.X + bounds.Width - 1, bounds.Center.Y);
        host.RenderFrame(new DisplayListBuilder());
        host.PressUp(bounds.X + bounds.Width - 1, bounds.Center.Y);

        host.HasSelection.Should().BeTrue("the pointer drew a range");
        host.Selection.Should().Be((0, "hello world".Length));

        host.TextInput("x");
        field.Value.Should().Be("x", "typing over a selection replaces it");
    }

    [Fact]
    public void ShiftArrowsExtendFromWhereYouWere()
    {
        var (host, field, _) = Open();
        Press(host, "Tab");                       // caret at the end
        Press(host, "ArrowLeft", KeyModifiers.Shift);
        Press(host, "ArrowLeft", KeyModifiers.Shift);
        Press(host, "ArrowLeft", KeyModifiers.Shift);

        host.Selection.Should().Be((8, 11));

        Press(host, "Backspace");
        field.Value.Should().Be("hello wo", "backspace over a selection deletes the range, not one character");
    }

    [Fact]
    public void CommandASelectsEverything()
    {
        var (host, field, _) = Open();
        Press(host, "Tab");

        Press(host, "a", KeyModifiers.Command);
        host.Selection.Should().Be((0, "hello world".Length));

        Press(host, "Delete");
        field.Value.Should().BeEmpty();
    }

    [Fact]
    public void AnArrowWithoutShiftCollapsesToTheEdgeYouMovedTowards()
    {
        var (host, _, _) = Open();
        Press(host, "Tab");
        Press(host, "a", KeyModifiers.Command);

        Press(host, "ArrowLeft");
        host.HasSelection.Should().BeFalse();
        host.CaretIndex.Should().Be(0, "left of a selection is its start, not one before the caret");
    }
}

/// <summary>⌘C/⌘X/⌘V against a fake pasteboard — the field's half of the contract.</summary>
public class ClipboardTests
{
    private sealed class FakeClipboard : Framework.ITextClipboard
    {
        public string? Content;
        public string? Read() => Content;
        public void Write(string text) => Content = text;
    }

    private sealed class Field(bool obscure) : Primitives.StatefulComponent
    {
        public string Value = "hello world";

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: 0) { Width = SizeValue.Fill };
            column.Add(new TextEntry(Value, v => SetState(() => Value = v)) { Obscure = obscure });
            return column;
        }
    }

    private static (PhotonHost Host, Field Field, FakeClipboard Clipboard) Open(bool obscure = false)
    {
        var field = new Field(obscure);
        var clipboard = new FakeClipboard();
        var host = new PhotonHost(field, PhotonTheme.Instance, ThemeMode.Light, 400, 100)
        {
            Clipboard = clipboard,
        };
        host.RenderFrame(new DisplayListBuilder());
        return (host, field, clipboard);
    }

    private static void Press(PhotonHost host, string key, KeyModifiers modifiers = KeyModifiers.None)
    {
        host.KeyDown(key, modifiers);
        host.RenderFrame(new DisplayListBuilder());
    }

    [Fact]
    public void CopyCutAndPaste_RoundTrip()
    {
        var (host, field, clipboard) = Open();
        Press(host, "Tab");
        Press(host, "a", KeyModifiers.Command);

        Press(host, "c", KeyModifiers.Command);
        clipboard.Content.Should().Be("hello world");
        field.Value.Should().Be("hello world", "copy leaves the text alone");

        Press(host, "x", KeyModifiers.Command);
        field.Value.Should().BeEmpty("cut removes what it copied");

        Press(host, "v", KeyModifiers.Command);
        field.Value.Should().Be("hello world");

        Press(host, "v", KeyModifiers.Command);
        field.Value.Should().Be("hello worldhello world", "paste inserts at the caret");
    }

    [Fact]
    public void APastedNewlineBecomesASpace()
    {
        var (host, field, clipboard) = Open();
        Press(host, "Tab");
        Press(host, "a", KeyModifiers.Command);
        clipboard.Content = "two\nlines";

        Press(host, "v", KeyModifiers.Command);
        field.Value.Should().Be("two lines", "a single-line field cannot show the newline it was handed");
    }

    [Fact]
    public void CommandVWithoutAClipboard_DoesNotTypeAV()
    {
        var (host, field, _) = Open();
        host.Clipboard = null;
        Press(host, "Tab");
        Press(host, "a", KeyModifiers.Command);

        Press(host, "v", KeyModifiers.Command);
        field.Value.Should().Be("hello world",
            "an unclaimed ⌘V would fall through as the letter v over the selection");
    }

    [Fact]
    public void APasswordNeverReachesTheClipboard()
    {
        var (host, field, clipboard) = Open(obscure: true);
        clipboard.Content = "mine";
        Press(host, "Tab");
        Press(host, "a", KeyModifiers.Command);

        Press(host, "c", KeyModifiers.Command);
        clipboard.Content.Should().Be("mine", "the secret is not the field's to hand out — nor to clear over");

        Press(host, "x", KeyModifiers.Command);
        clipboard.Content.Should().Be("mine");
        field.Value.Should().BeEmpty("cut still deletes; it just does not export");
    }
}

/// <summary>Double-click picks the word, triple the line — how nearly everyone actually selects.</summary>
public class WordSelectionTests
{
    private static (PhotonHost Host, Rect Bounds) Open(string value = "hello brave world")
    {
        var column = new Column(gap: 0) { Width = SizeValue.Fill };
        column.Add(new TextEntry(value, _ => { }));
        var host = new PhotonHost(column, PhotonTheme.Instance, ThemeMode.Light, 400, 100);
        var frame = host.RenderFrame(new DisplayListBuilder());
        return (host, frame.TextRegions.Single().Bounds);
    }

    [Fact]
    public void DoubleClickSelectsTheWordUnderThePoint()
    {
        var (host, bounds) = Open();
        // Aim inside "brave": the width of "hello br", measured by the same measurer the host
        // uses. A fraction of the FIELD would miss — the field fills 400dp, the text does not.
        var style = PhotonTheme.Instance.Type(TypeRole.BodyL);
        var x = bounds.X + Framework.ApproximateTextMeasurer.Instance
            .Measure("hello br", style, 1f, float.PositiveInfinity, 1).Lines[0].Width;
        host.PressDown(x, bounds.Center.Y, clickCount: 2);
        host.PressUp(x, bounds.Center.Y);

        host.Selection.Should().Be((6, 11), "the word under the pointer, not the characters around it");
    }

    [Fact]
    public void TripleClickSelectsEverything()
    {
        var (host, bounds) = Open();
        host.PressDown(bounds.Center.X, bounds.Center.Y, clickCount: 3);
        host.PressUp(bounds.Center.X, bounds.Center.Y);

        host.Selection.Should().Be((0, "hello brave world".Length));
    }
}

/// <summary>
/// Choosing from a Select or a Menu without a mouse: open, arrow to the option, Enter. The chords
/// are declared as Shortcut nodes around the OPEN panel, so a closed control owns no keys at all —
/// and every arrow press rebuilds the tree, which is exactly what these walk through.
/// </summary>
public class DropdownKeyboardTests
{
    private static (PhotonHost Host, List<int> Chosen) OpenSelect(int selected = 0)
    {
        var chosen = new List<int>();
        var column = new Column(gap: 0) { Width = 300 };
        column.Add(new Select(["Checking", "Savings", "Card"], selected, chosen.Add));
        var host = new PhotonHost(column, PhotonTheme.Instance, ThemeMode.Light, 300, 400);
        host.RenderFrame(new DisplayListBuilder());

        // Open with the pointer on the trigger — the first hit region.
        var trigger = host.RenderFrame(new DisplayListBuilder()).HitRegions[0].Bounds;
        host.PressDown(trigger.Center.X, trigger.Center.Y);
        host.PressUp(trigger.Center.X, trigger.Center.Y);
        host.RenderFrame(new DisplayListBuilder());
        return (host, chosen);
    }

    private static void Press(PhotonHost host, string key)
    {
        host.KeyDown(key);
        host.RenderFrame(new DisplayListBuilder());
    }

    [Fact]
    public void ArrowsMoveAndEnterChooses_AcrossTheRebuildEachArrowCauses()
    {
        var (host, chosen) = OpenSelect(selected: 0);

        Press(host, "ArrowDown");
        Press(host, "ArrowDown");
        Press(host, "Enter");

        chosen.Should().Equal([2], "two steps down from the selected option is the third");
    }

    [Fact]
    public void TheHighlightStartsOnTheValue_SoEnterAloneRecommits()
    {
        var (host, chosen) = OpenSelect(selected: 1);

        Press(host, "Enter");

        chosen.Should().Equal([1], "opening and confirming must not move the value");
    }

    [Fact]
    public void AClosedSelectOwnsNoKeys()
    {
        var chosen = new List<int>();
        var column = new Column(gap: 0) { Width = 300 };
        column.Add(new Select(["A", "B"], 0, chosen.Add));
        var host = new PhotonHost(column, PhotonTheme.Instance, ThemeMode.Light, 300, 400);
        host.RenderFrame(new DisplayListBuilder());

        host.KeyDown("ArrowDown").Should().BeFalse("nothing is open to hear it");
        host.KeyDown("Enter").Should().BeFalse();
        chosen.Should().BeEmpty();
    }

    [Fact]
    public void EscapeClosesInsteadOfChoosing()
    {
        var (host, chosen) = OpenSelect();

        Press(host, "ArrowDown");
        Press(host, "Escape");
        chosen.Should().BeEmpty("Escape is leave-without-committing");

        host.KeyDown("ArrowDown").Should().BeFalse("closed again, no keys owned");
    }

    [Fact]
    public void AMenuSkipsItsDisabledItems()
    {
        var picked = new List<int>();
        var column = new Column(gap: 0) { Width = 300 };
        column.Add(new Menu(new Button("Actions"),
        [
            new MenuItem("Rename"),
            new MenuItem("Locked") { Disabled = true },
            new MenuItem("Delete") { Destructive = true },
        ], picked.Add));
        var host = new PhotonHost(column, PhotonTheme.Instance, ThemeMode.Light, 300, 400);
        host.RenderFrame(new DisplayListBuilder());

        var trigger = host.RenderFrame(new DisplayListBuilder()).HitRegions[0].Bounds;
        host.PressDown(trigger.Center.X, trigger.Center.Y);
        host.PressUp(trigger.Center.X, trigger.Center.Y);
        host.RenderFrame(new DisplayListBuilder());

        Press(host, "ArrowDown");   // over Locked, onto Delete
        Press(host, "Enter");

        picked.Should().Equal([2], "the highlight never lands where Enter would refuse to run");
    }
}

/// <summary>
/// The field is a WINDOW onto its text: when what you typed is wider than the box, the text slides
/// so the caret never leaves view. Without this, typing past the edge kept appending characters
/// nobody could see — the caret rode off the right side and stayed there.
/// </summary>
public class CaretFollowTests
{
    /// <summary>8dp per character, deterministic — long text vs a 100dp field is the scenario.</summary>
    private sealed class WideRasterizer : Framework.ITextRasterizer
    {
        public Framework.TextRaster? Rasterize(string content, TypeStyle style, float typeScale,
            float maxWidth, int maxLines, float scale)
        {
            if (string.IsNullOrEmpty(content)) return null;
            var width = Math.Max(1, (int)Math.Min(content.Length * 8 * scale, maxWidth * scale));
            var height = Math.Max(1, (int)(style.LineHeight * scale));
            return new Framework.TextRaster(width, height, new byte[width * height]);
        }
    }

    private static (DrawCommand[] Commands, Rect Field) TypeInto(string value)
    {
        var column = new Column(gap: 0) { Width = 100 };
        column.Add(new TextEntry(value, _ => { }));
        var host = new PhotonHost(column, PhotonTheme.Instance, ThemeMode.Light, 100, 100)
        {
            TextRasterizer = new WideRasterizer(),
        };
        host.RenderFrame(new DisplayListBuilder());
        var frame = host.RenderFrame(new DisplayListBuilder());
        var field = frame.TextRegions.Single().Bounds;

        // Click in, then End: the caret is at the far end of the overflowing text. (End rather
        // than the click position: the stub rasterizer and the approximate measurer disagree about
        // widths, which the real platform cannot — CoreText serves both by construction.)
        host.PressDown(field.Center.X, field.Center.Y);
        host.PressUp(field.Center.X, field.Center.Y);
        host.KeyDown("End");
        host.RenderFrame(new DisplayListBuilder());

        var builder = new DisplayListBuilder();
        host.RenderFrame(builder);
        return (builder.Build().Commands.ToArray(), field);
    }

    [Fact]
    public void TheCaretStaysInsideTheField_HoweverLongTheText()
    {
        // 30 chars × 8dp = 240dp of text in a 100dp field.
        var (commands, field) = TypeInto(new string('m', 30));

        var caret = commands.Last(c => c.Kind == DrawCommandKind.FillRRect && c.Shape.Rect.Width == 2);
        caret.Shape.Rect.X.Should().BeInRange(field.X, field.X + field.Width,
            "a caret off the right edge is a field nobody can type into with confidence");

        // …and the text slid LEFT to make that true, clipped to the field's own window.
        var texture = commands.Single(c => c.Kind == DrawCommandKind.Texture);
        texture.Shape.Rect.X.Should().BeLessThan(field.X, "the start of the text is out of view");
        texture.Clip.Should().NotBeNull("what slid out must not paint over the neighbours");
    }

    [Fact]
    public void ShortTextDoesNotSlide()
    {
        var (commands, field) = TypeInto("abc");

        var texture = commands.Single(c => c.Kind == DrawCommandKind.Texture);
        texture.Shape.Rect.X.Should().Be(field.X, "nothing overflows, nothing moves");
    }
}

/// <summary>
/// The scroll must NOT stay stuck to the pointer after a release. Found in the field: click any
/// inert control inside a ScrollView (the Studio's variant matrix is a wall of handler-less
/// buttons) and from then on every bare mouse move scrolled the page — the pan candidate armed by
/// the press-down was never cleared, because the swallowed-press path returned before cleanup.
/// </summary>
public class PanLatchTests
{
    private static (PhotonHost Host, Rect Inert, string ScrollPath) Open()
    {
        var column = new Column(gap: Space.S4) { Width = SizeValue.Fill };
        column.Add(new Button("Inert specimen"));   // no handler: exactly the variant matrix
        for (var i = 0; i < 30; i++) column.Add(new Text($"row {i}", TypeRole.BodyM));
        var host = new PhotonHost(new ScrollView(column), PhotonTheme.Instance, ThemeMode.Light, 300, 200);
        var frame = host.RenderFrame(new DisplayListBuilder());
        return (host, frame.HitRegions[0].Bounds, frame.ScrollRegions[0].Path);
    }

    [Fact]
    public void ReleasingOnAnInertControl_DoesNotLeaveTheScrollStuckToThePointer()
    {
        var (host, inert, scrollPath) = Open();

        host.PressDown(inert.Center.X, inert.Center.Y);
        host.PressUp(inert.Center.X, inert.Center.Y);

        // The bug: these moves — no button held — kept scrolling. UPWARD, deliberately: the
        // latched pan scrolls the content the opposite way, and a downward move lands on a
        // negative offset the clamp silently turns back into zero — a false pass.
        host.PointerMove(inert.Center.X, inert.Center.Y - 40);
        host.PointerMove(inert.Center.X, inert.Center.Y - 80);
        host.RenderFrame(new DisplayListBuilder());

        var frame = host.RenderFrame(new DisplayListBuilder());
        frame.ScrollRegions[0].Path.Should().Be(scrollPath);
        host.ScrollOffsetOf(scrollPath).Should().Be(0,
            "a released press is over — nothing may keep steering the scroll");
    }

    [Fact]
    public void ADeliberatePanStillScrolls_AndStillLetsGo()
    {
        var (host, inert, scrollPath) = Open();
        var startY = inert.Center.Y + 60;   // on the rows, not the button

        host.PressDown(inert.Center.X, startY);
        host.PointerMove(inert.Center.X, startY - 50);   // drag up = scroll down
        host.RenderFrame(new DisplayListBuilder());
        var mid = host.ScrollOffsetOf(scrollPath);
        mid.Should().BeGreaterThan(0, "a held drag pans the scroll");

        host.PressUp(inert.Center.X, startY - 50);
        host.PointerMove(inert.Center.X, startY);        // bare move after release
        host.RenderFrame(new DisplayListBuilder());

        host.ScrollOffsetOf(scrollPath).Should().Be(mid, "letting go lets go");
    }
}

/// <summary>
/// A slider under the keyboard: ONE Tab stop for the whole control, arrows nudge by its own step.
/// Before the Adjustable node, Tab stopped twice per slider (each track half is a pressable) and
/// the arrows did nothing at either stop.
/// </summary>
public class AdjustableTests
{
    private sealed class Page : Primitives.StatefulComponent
    {
        public float Value = 0.4f;

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S4) { Width = SizeValue.Fill };
            column.Add(new Slider(Value, v => SetState(() => Value = v)) { Step = 0.1f, Label = "Budget" });
            column.Add(new Button("Save", onPressed: () => { }));
            return column;
        }
    }

    private static (PhotonHost Host, Page Page) Open()
    {
        var page = new Page();
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 300, 200);
        host.RenderFrame(new DisplayListBuilder());
        return (host, page);
    }

    private static void Press(PhotonHost host, string key)
    {
        host.KeyDown(key);
        host.RenderFrame(new DisplayListBuilder());
    }

    [Fact]
    public void TheWholeSliderIsONETabStop()
    {
        var (host, _) = Open();
        var stops = host.RenderFrame(new DisplayListBuilder()).FocusStops;

        stops.Should().HaveCount(2, "the slider and the button — not the slider's two track halves");
        stops[0].Adjustable.Should().NotBeNull();
        stops[1].Pressable.Should().NotBeNull();
    }

    [Fact]
    public void ArrowsNudgeByTheSliderStep_AcrossTheRebuildEachNudgeCauses()
    {
        var (host, page) = Open();
        Press(host, "Tab");   // onto the slider

        Press(host, "ArrowRight");
        Press(host, "ArrowRight");
        page.Value.Should().BeApproximately(0.6f, 0.001f);

        Press(host, "ArrowLeft");
        page.Value.Should().BeApproximately(0.5f, 0.001f);
    }

    [Fact]
    public void TheTrackHalvesStillAnswerThePointer()
    {
        var (host, page) = Open();
        var frame = host.RenderFrame(new DisplayListBuilder());

        // The UNFILLED half sits right of the thumb; pressing it steps up — pointer behaviour is
        // untouched by the keyboard wrapper. Tree order: filled half, unfilled half, then Save.
        var right = frame.HitRegions[1];
        host.PressDown(right.Bounds.Center.X, right.Bounds.Center.Y);
        host.PressUp(right.Bounds.Center.X, right.Bounds.Center.Y);

        page.Value.Should().BeApproximately(0.5f, 0.001f);
    }
}
