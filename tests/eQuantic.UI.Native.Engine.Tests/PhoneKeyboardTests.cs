using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// A phone's keyboard at the host (#770). Android's input connection and iOS's text input edit a
/// DOCUMENT: they hand back the field's whole new state, and the host turns it into the one edit the
/// app hears about. These tests drive what both phone shells drive, the editing value and the
/// session, and what the keyboard's rising does to the layout.
/// </summary>
public class PhoneKeyboardTests
{
    private sealed class Form : Primitives.StatefulComponent
    {
        public string Value = "";
        public readonly List<string> Changes = [];
        public Func<string, string> Rewrite = value => value;
        public bool ReadOnly;
        public int Submitted;

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
            column.Add(new TextEntry(Value, ReadOnly ? null : v => SetState(() =>
            {
                Changes.Add(v);
                Value = Rewrite(v);
            }))
            {
                Placeholder = "Name",
                OnSubmit = () => Submitted++,
            });
            column.Add(new TextEntry("", _ => { }) { Placeholder = "Other" });
            return column;
        }
    }

    private static (PhotonHost Host, Form Form) Open(string initial = "", bool readOnly = false)
    {
        var form = new Form { Value = initial, ReadOnly = readOnly };
        var host = new PhotonHost(form, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        var frame = host.RenderFrame(new DisplayListBuilder());
        Tap(host, frame.TextRegions[0].Bounds.Center);
        host.RenderFrame(new DisplayListBuilder(), 16);
        return (host, form);
    }

    private static void Tap(PhotonHost host, Point at)
    {
        host.PressDown(at.X, at.Y);
        host.PressUp(at.X, at.Y, PointerKind.Touch);
    }

    // ---- the editing value -----------------------------------------------------------------------

    [Fact]
    public void TheKeyboardsEdit_ReachesTheAppOnce_AsTheFieldsNewValue()
    {
        var (host, form) = Open();

        host.UpdateEditingValue(TextEditingValue.Collapsed("hello", 5)).Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 32);

        form.Changes.Should().Equal(["hello"]);
        host.EditingValue.Should().Be(TextEditingValue.Collapsed("hello", 5));
    }

    /// <summary>
    /// The word a keyboard is still suggesting for is IN the value, as on the web and in Flutter: a
    /// search field filters as the word is typed, not when the space after it is. Only the range
    /// marks it.
    /// </summary>
    [Fact]
    public void AComposition_IsPartOfTheValue_AndOnlyMarked()
    {
        var (host, form) = Open("say ");

        host.UpdateEditingValue(new TextEditingValue("say hel", 7, 7, 4, 7));
        host.RenderFrame(new DisplayListBuilder(), 32);

        form.Value.Should().Be("say hel");
        host.EditingValue.Should().Be(new TextEditingValue("say hel", 7, 7, 4, 7),
            "the composition survives the frame the app's value is drawn in");
    }

    [Fact]
    public void ACaretTheHostMoves_EndsTheComposition()
    {
        var (host, _) = Open();
        host.UpdateEditingValue(new TextEditingValue("hel", 3, 3, 0, 3));
        host.RenderFrame(new DisplayListBuilder(), 32);

        host.KeyDown("ArrowLeft");

        host.EditingValue.Should().Be(TextEditingValue.Collapsed("hel", 2));
    }

    [Fact]
    public void TheSelection_ComesBackAsTheKeyboardSentIt_EndsInOrder()
    {
        var (host, _) = Open("hello");

        host.UpdateEditingValue(new TextEditingValue("hello", 4, 1));

        host.Selection.Should().Be((1, 4));
        host.CaretIndex.Should().Be(1, "the extent is the end that moves");
        host.EditingValue.Should().Be(new TextEditingValue("hello", 4, 1));
    }

    [Fact]
    public void ASingleLineField_FlattensALineBreak_AsAPasteDoes()
    {
        var (host, form) = Open();

        host.UpdateEditingValue(TextEditingValue.Collapsed("a\nb", 3));

        form.Changes.Should().Equal(["a b"]);
    }

    /// <summary>A pasted CRLF becomes one space, and the caret sent after it lands after the same
    /// characters: it was left one past them.</summary>
    [Fact]
    public void AFlattenedLineBreak_KeepsTheSelectionOnItsText()
    {
        var (host, form) = Open();

        host.UpdateEditingValue(new TextEditingValue("a\r\nbc", 3, 3)).Should().BeTrue();

        form.Changes.Should().Equal(["a bc"]);
        host.CaretIndex.Should().Be(2, "the caret was before the b");
    }

    /// <summary>
    /// iOS autocorrects "teh" by deleting it and typing the fix, all before a frame. Read off the
    /// frame, each edit started from the value before all of them, and the field ended "tehthe"-ish
    /// rubbish; each now starts from the one before it.
    /// </summary>
    [Fact]
    public void EditsBetweenTwoFrames_EachStartFromTheOneBefore()
    {
        var (host, form) = Open("teh");

        host.KeyDown("Backspace");
        host.KeyDown("Backspace");
        host.KeyDown("Backspace");
        host.TextInput("the ");
        host.RenderFrame(new DisplayListBuilder(), 32);

        form.Value.Should().Be("the ");
        host.CaretIndex.Should().Be(4);
    }

    /// <summary>
    /// The frame that shows a typed character draws the caret after it. It was placed against the
    /// last frame's value, one character shorter, and drew a character behind until the next blink.
    /// </summary>
    [Fact]
    public void TheCaret_IsDrawnWhereTheEditPutIt_InTheFrameThatShowsIt()
    {
        var (host, bounds, _) = OpenMeasured("ab");
        host.TextInput("c");

        var drawn = Drawn(host, 32);

        CaretX(drawn).Should().Be(bounds.X + 3 * 8);
    }

    /// <summary>The keyboard's composition is underlined across its own range of the value.</summary>
    [Fact]
    public void AComposition_IsDrawnUnderlined_AcrossItsRange()
    {
        var (host, bounds, _) = OpenMeasured("say ");
        host.UpdateEditingValue(new TextEditingValue("say hel", 7, 7, 4, 7));

        var underline = Drawn(host, 32).Single(command =>
            command.Kind == DrawCommandKind.FillRRect && MathF.Abs(command.Shape.Rect.Height - 1.5f) < 0.01f);

        underline.Shape.Rect.X.Should().Be(bounds.X + 4 * 8);
        underline.Shape.Rect.Width.Should().Be(3 * 8);
    }

    /// <summary>
    /// A composition is a range of the text the keyboard composed in. An app that rewrites that text
    /// ends it, as setting a controller's text does in Flutter: the keyboard is about to be told a
    /// text it never composed in, and an underline over it would mark a word nobody is suggesting.
    /// </summary>
    [Fact]
    public void AnAppThatRewritesTheText_EndsTheComposition()
    {
        var (host, _, form) = OpenMeasured("");
        form.Rewrite = value => value.ToUpperInvariant();
        host.UpdateEditingValue(new TextEditingValue("hel", 3, 3, 0, 3));

        var drawn = Drawn(host, 32);

        drawn.Should().NotContain(command => command.Kind == DrawCommandKind.FillRRect
            && MathF.Abs(command.Shape.Rect.Height - 1.5f) < 0.01f, "the field shows HEL, which nobody composed");
        host.EditingValue.Should().Be(TextEditingValue.Collapsed("HEL", 3));
    }

    private sealed class FixedWidthRasterizer : Framework.ITextRasterizer
    {
        public Framework.TextRaster? Rasterize(string content, TypeStyle style, float typeScale,
            float maxWidth, int maxLines, float scale, TextAlignment align)
        {
            if (string.IsNullOrEmpty(content)) return null;
            var width = Math.Max(1, (int)Math.Min(content.Length * 8 * scale, maxWidth * scale));
            var height = Math.Max(1, (int)(style.LineHeight * scale));
            return new Framework.TextRaster(width, height, new byte[width * height]);
        }
    }

    private static (PhotonHost Host, Rect Bounds, Form Form) OpenMeasured(string initial)
    {
        var form = new Form { Value = initial };
        var host = new PhotonHost(form, PhotonTheme.Instance, ThemeMode.Light, 400, 300)
        {
            TextRasterizer = new FixedWidthRasterizer(),
        };
        var field = host.RenderFrame(new DisplayListBuilder()).TextRegions[0].Bounds;
        Tap(host, new Point(field.Right - 1, field.Center.Y));
        host.RenderFrame(new DisplayListBuilder(), 16);
        return (host, field, form);
    }

    private static IReadOnlyList<DrawCommand> Drawn(PhotonHost host, float timeMs)
    {
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs);
        return builder.Build().Commands.ToArray();
    }

    private static float? CaretX(IReadOnlyList<DrawCommand> commands) => commands
        .Where(command => command.Kind == DrawCommandKind.FillRRect
            && MathF.Abs(command.Shape.Rect.Width - 2f) < 0.01f && command.Shape.Rect.Height >= 8)
        .Select(command => (float?)command.Shape.Rect.X)
        .FirstOrDefault();

    [Fact]
    public void NoFieldBeingEdited_HasNoEditingValue_AndTakesNoEdit()
    {
        var form = new Form();
        var host = new PhotonHost(form, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());

        host.EditingValue.Should().BeNull();
        host.UpdateEditingValue(TextEditingValue.Collapsed("x", 1)).Should().BeFalse();
        form.Changes.Should().BeEmpty();
    }

    // ---- the session -----------------------------------------------------------------------------

    [Fact]
    public void TheSession_OpensOnAField_AndClosesWhenTheEditEnds()
    {
        var form = new Form { Value = "hi" };
        var host = new PhotonHost(form, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        var session = new TextInputSession(host);
        var frame = host.RenderFrame(new DisplayListBuilder());
        session.Poll().Should().Be(TextInputChange.None);

        Tap(host, frame.TextRegions[0].Bounds.Center);
        host.RenderFrame(new DisplayListBuilder(), 16);
        session.Poll().Should().Be(TextInputChange.Began);
        session.Target.Should().Be(TextInputTarget.Text);
        session.Value.Text.Should().Be("hi");
        session.Poll().Should().Be(TextInputChange.None, "each answer is given once");

        host.EndEditing();
        host.RenderFrame(new DisplayListBuilder(), 32);
        session.Poll().Should().Be(TextInputChange.Ended);
        session.Target.Should().Be(TextInputTarget.None);
    }

    [Fact]
    public void TheSession_StartsOver_WhenTheCaretMovesToAnotherField()
    {
        var (host, _) = Open();
        var session = new TextInputSession(host);
        session.Poll().Should().Be(TextInputChange.Began);

        host.KeyDown("Tab");
        host.RenderFrame(new DisplayListBuilder(), 32);

        session.Poll().Should().Be(TextInputChange.Retargeted);
        host.TextTarget!.Placeholder.Should().Be("Other");
    }

    /// <summary>What the keyboard sent is what it has: echoing it back would restart its word.</summary>
    [Fact]
    public void AnEditTheAppTakes_IsNeverEchoedToTheKeyboard()
    {
        var (host, _) = Open();
        var session = new TextInputSession(host);
        session.Poll();

        session.Update(new TextEditingValue("hel", 3, 3, 0, 3)).Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 32);

        session.Poll().Should().Be(TextInputChange.None);
    }

    [Fact]
    public void AnEditTheAppRewrites_ReachesTheKeyboard()
    {
        var (host, form) = Open();
        form.Rewrite = value => value.ToUpperInvariant();
        var session = new TextInputSession(host);
        session.Poll();

        session.Update(TextEditingValue.Collapsed("abc", 3));
        host.RenderFrame(new DisplayListBuilder(), 32);

        session.Poll().Should().Be(TextInputChange.ValueChanged);
        session.Value.Should().Be(TextEditingValue.Collapsed("ABC", 3));
    }

    [Fact]
    public void AReadOnlyField_PutsTheKeyboardBack()
    {
        var (host, _) = Open("fixed", readOnly: true);
        var session = new TextInputSession(host);
        session.Poll();

        session.Update(TextEditingValue.Collapsed("fixedx", 6));
        host.RenderFrame(new DisplayListBuilder(), 32);

        session.Poll().Should().Be(TextInputChange.ValueChanged);
        session.Value.Text.Should().Be("fixed");
    }

    /// <summary>
    /// A dictation into one field finishes after the person tapped another. Its text is the first
    /// field's, and it went into the second; it now goes nowhere, and the keyboard starts over on the
    /// field the caret is in.
    /// </summary>
    [Fact]
    public void AnEditThatArrivesAfterTheCaretMoved_GoesNowhere()
    {
        var (host, form) = Open("first");
        var session = new TextInputSession(host);
        session.Poll();
        Tap(host, host.LastFrame!.TextRegions[1].Bounds.Center);

        session.Update(TextEditingValue.Collapsed("first words", 11)).Should().BeFalse();
        session.Submit().Should().BeFalse();

        host.EditingValue!.Value.Text.Should().BeEmpty("the field tapped holds what it held");
        form.Changes.Should().BeEmpty();
        host.RenderFrame(new DisplayListBuilder(), 32);
        session.Poll().Should().Be(TextInputChange.Retargeted);
    }

    /// <summary>A phone's Done submits the field and puts the keyboard away with the edit.</summary>
    [Fact]
    public void TheActionKey_SubmitsTheField_AndEndsTheEdit()
    {
        var (host, form) = Open("hi");
        var session = new TextInputSession(host);
        session.Poll();

        session.Submit().Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 32);

        form.Submitted.Should().Be(1);
        session.Poll().Should().Be(TextInputChange.Ended);
    }

        [Fact]
    public void AKeyTheHostEdits_ReachesTheKeyboard()
    {
        var (host, _) = Open("abc");
        var session = new TextInputSession(host);
        session.Poll();

        host.KeyDown("Backspace");
        host.RenderFrame(new DisplayListBuilder(), 32);

        session.Poll().Should().Be(TextInputChange.ValueChanged);
        session.Value.Should().Be(TextEditingValue.Collapsed("ab", 2));
    }

    private sealed class Editor : Primitives.StatefulComponent
    {
        public readonly CodeEditorController Controller = new("x");

        public override VisualNode Build(ComponentContext context) =>
            new CodeSurface(new Text("code", TypeRole.BodyM), Controller) { Autofocus = true };
    }

    /// <summary>A code surface keeps its text to itself: the keyboard types keys into it.</summary>
    [Fact]
    public void ACodeSurface_TakesTheKeyboardsKeys_NotADocument()
    {
        var editor = new Editor();
        var host = new PhotonHost(editor, PhotonTheme.Instance, ThemeMode.Light, 400, 300);
        host.RenderFrame(new DisplayListBuilder());
        var session = new TextInputSession(host);

        session.Poll().Should().Be(TextInputChange.Began);
        session.Target.Should().Be(TextInputTarget.Keys);
        host.EditingValue.Should().BeNull();
        session.Update(TextEditingValue.Collapsed("y", 1)).Should().BeFalse();

        host.TextInput("y").Should().BeTrue("committed text types through the surface's model");
        host.KeyDown("Backspace").Should().BeTrue();
    }

    // ---- the keyboard's area ---------------------------------------------------------------------

    private sealed class LongForm : Primitives.StatefulComponent
    {
        public override VisualNode Build(ComponentContext context)
        {
            var list = new Column(gap: Space.S4) { Width = SizeValue.Fill };
            for (var i = 0; i < 12; i++) list.Add(new TextEntry("", _ => { }) { Placeholder = $"F{i}" });
            return new ScrollView(list) { Width = SizeValue.Fill, Height = SizeValue.Fill };
        }
    }

    /// <summary>
    /// The keyboard covers the bottom of the window: the tree lays out in what is left, and the
    /// field being typed in is scrolled into it. Before, a field low on the screen was typed into
    /// under the keyboard, unseen.
    /// </summary>
    [Fact]
    public void TheKeyboardRising_LaysTheTreeOutAboveIt_AndRevealsTheField()
    {
        var host = new PhotonHost(new LongForm(), PhotonTheme.Instance, ThemeMode.Light, 390, 600);
        var frame = host.RenderFrame(new DisplayListBuilder());
        var low = frame.TextRegions.Last(region => region.Bounds.Bottom <= 600);
        Tap(host, low.Bounds.Center);
        host.RenderFrame(new DisplayListBuilder(), 16);

        host.ViewInsets = new EdgeInsets(0, 0, 0, 300);
        frame = host.RenderFrame(new DisplayListBuilder(), 32);
        frame.ScrollRegions.Single().Bounds.Bottom.Should().BeApproximately(300, 0.5f,
            "the view lays out in what the keyboard leaves");

        frame = host.RenderFrame(new DisplayListBuilder(), 48);
        var field = frame.TextRegions.Single(region => region.Path == low.Path);
        field.Bounds.Bottom.Should().BeLessThanOrEqualTo(300, "the field is scrolled above the keyboard");
        field.Bounds.Top.Should().BeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// Where the system makes room for the keyboard by resizing the window (Android before it went
    /// edge to edge) the keyboard reaches the host as a shorter window, and the field is revealed
    /// all the same. So it is when a phone turns on its side.
    /// </summary>
    [Fact]
    public void AWindowThatShrinks_RevealsTheFieldBeingEdited()
    {
        var host = new PhotonHost(new LongForm(), PhotonTheme.Instance, ThemeMode.Light, 390, 600);
        var frame = host.RenderFrame(new DisplayListBuilder());
        var low = frame.TextRegions.Last(region => region.Bounds.Bottom <= 600);
        Tap(host, low.Bounds.Center);
        host.RenderFrame(new DisplayListBuilder(), 16);

        host.Resize(390, 300);
        host.RenderFrame(new DisplayListBuilder(), 32);
        frame = host.RenderFrame(new DisplayListBuilder(), 48);

        var field = frame.TextRegions.Single(region => region.Path == low.Path);
        field.Bounds.Bottom.Should().BeLessThanOrEqualTo(300);
        field.Bounds.Top.Should().BeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// The frame laid out above the rising keyboard clips the field under it before the reveal
    /// scrolls it back. It is still the field being edited: the keyboard stays a text keyboard, and
    /// what is typed in that frame reaches it.
    /// </summary>
    [Fact]
    public void AFieldTheKeyboardCovers_IsStillTheOneBeingEdited()
    {
        var host = new PhotonHost(new LongForm(), PhotonTheme.Instance, ThemeMode.Light, 390, 600);
        var session = new TextInputSession(host);
        var frame = host.RenderFrame(new DisplayListBuilder());
        var low = frame.TextRegions.Last(region => region.Bounds.Bottom <= 600);
        Tap(host, low.Bounds.Center);
        host.RenderFrame(new DisplayListBuilder(), 16);
        session.Poll().Should().Be(TextInputChange.Began);

        host.ViewInsets = new EdgeInsets(0, 0, 0, 300);
        frame = host.RenderFrame(new DisplayListBuilder(), 32);
        frame.TextRegions.Should().NotContain(region => region.Path == low.Path,
            "the field is under the keyboard in this frame");

        session.Poll().Should().Be(TextInputChange.None);
        session.Target.Should().Be(TextInputTarget.Text);
        host.TextInput("x").Should().BeTrue();
    }

    /// <summary>A screen that does not scroll, with its field low on it: the Wallet's onboarding
    /// step, or any form on a phone turned on its side.</summary>
    private sealed class FixedForm : Primitives.StatefulComponent
    {
        public string Value = "abcdef";

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: 8) { Width = SizeValue.Fill };
            column.Add(new Box(new BoxStyle { Width = SizeValue.Fill, Height = 360 }));
            column.Add(new TextEntry(Value, v => SetState(() => Value = v)) { Placeholder = "Key" });
            return column;
        }
    }

    /// <summary>
    /// Where nothing scrolls, nothing could bring the field into what the keyboard leaves, and it was
    /// typed into under the keyboard. The screen is lifted until it shows, as Android does for a
    /// window with nothing to scroll; a press lands where the screen is drawn, the caret and the
    /// semantics are reported there, and the screen comes back down with the keyboard.
    /// </summary>
    [Fact]
    public void AFieldNothingScrolls_IsLiftedAboveTheKeyboard()
    {
        var host = new PhotonHost(new FixedForm(), PhotonTheme.Instance, ThemeMode.Light, 390, 600)
        {
            TextRasterizer = new FixedWidthRasterizer(),
        };
        var laid = host.RenderFrame(new DisplayListBuilder()).TextRegions.Single().Bounds;
        Tap(host, laid.Center);
        host.RenderFrame(new DisplayListBuilder(), 16);

        host.ViewInsets = new EdgeInsets(0, 0, 0, 300);
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.RenderFrame(new DisplayListBuilder(), 48);

        var caret = host.CaretRect()!.Value;
        caret.Bottom.Should().BeLessThanOrEqualTo(300, "the field is drawn above the keyboard");
        var lift = laid.Y - caret.Y;
        lift.Should().BeGreaterThan(0);
        host.Semantics().Single(node => node.Role == SemanticRole.TextField).Bounds.Y
            .Should().BeApproximately(laid.Y - lift, 0.5f, "a screen reader finds the field where it is drawn");

        Tap(host, new Point(laid.X + 2 * 8 + 1, laid.Center.Y - lift));
        host.HasTextFocus.Should().BeTrue("the press landed on the field where it is drawn");
        host.CaretIndex.Should().Be(2);

        host.ViewInsets = default;
        host.RenderFrame(new DisplayListBuilder(), 64);
        host.CaretRect()!.Value.Y.Should().BeApproximately(laid.Y, 0.5f, "the screen comes back down");
    }

    /// <summary>
    /// Android before 15, for an app not drawn edge to edge, makes room for the keyboard by
    /// shortening the window: no inset arrives, and the field is lifted all the same.
    /// </summary>
    [Fact]
    public void AFieldAShorterWindowCovers_IsLiftedToo()
    {
        var host = new PhotonHost(new FixedForm(), PhotonTheme.Instance, ThemeMode.Light, 390, 600);
        var laid = host.RenderFrame(new DisplayListBuilder()).TextRegions.Single().Bounds;
        Tap(host, laid.Center);
        host.RenderFrame(new DisplayListBuilder(), 16);

        host.Resize(390, 300);
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.RenderFrame(new DisplayListBuilder(), 48);

        host.CaretRect()!.Value.Bottom.Should().BeLessThanOrEqualTo(300);

        host.Resize(390, 600);
        host.RenderFrame(new DisplayListBuilder(), 64);
        host.CaretRect()!.Value.Y.Should().BeApproximately(laid.Y, 4, "the screen comes back down");
    }

    /// <summary>
    /// A window that was always too short for its form, with nothing taken from it by a keyboard: a
    /// desktop's. Its layout is the app's to scroll, and the screen is not moved under it.
    /// </summary>
    [Fact]
    public void AWindowThatWasAlwaysTooShort_IsNotLifted()
    {
        var host = new PhotonHost(new FixedForm(), PhotonTheme.Instance, ThemeMode.Light, 390, 300);
        var frame = host.RenderFrame(new DisplayListBuilder());
        host.KeyDown("Tab").Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 16);
        frame = host.RenderFrame(new DisplayListBuilder(), 32);

        host.HasTextFocus.Should().BeTrue();
        var field = frame.FocusStops.Single(stop => stop.Path == host.FocusedPath).Bounds;
        field.Bottom.Should().BeGreaterThan(300, "the field is below the window");
        host.Semantics().Single(node => node.Role == SemanticRole.TextField).Bounds.Y
            .Should().BeApproximately(field.Y, 0.5f, "the screen is drawn where it was laid out");
    }

    /// <summary>
    /// With the keyboard up, the person scrolls the field being edited down under it. A scroll view
    /// holds it, and the scroll is theirs: the screen is not lifted to fight it, as Flutter leaves
    /// it.
    /// </summary>
    [Fact]
    public void AFieldScrolledUnderTheKeyboard_IsNotLifted()
    {
        var host = new PhotonHost(new LongForm(), PhotonTheme.Instance, ThemeMode.Light, 390, 600)
        {
            SmoothScroll = false,
        };
        var frame = host.RenderFrame(new DisplayListBuilder());
        var low = frame.TextRegions.Last(region => region.Bounds.Bottom <= 600);
        Tap(host, low.Bounds.Center);
        host.RenderFrame(new DisplayListBuilder(), 16);
        host.ViewInsets = new EdgeInsets(0, 0, 0, 300);
        host.RenderFrame(new DisplayListBuilder(), 32);
        host.RenderFrame(new DisplayListBuilder(), 48);

        host.ScrollBy(100, 100, -1000).Should().BeTrue();
        host.RenderFrame(new DisplayListBuilder(), 64);
        frame = host.RenderFrame(new DisplayListBuilder(), 80);

        frame.FocusStops.Single(stop => stop.Path == low.Path).Bounds.Top
            .Should().BeGreaterThan(300, "the field is under the keyboard, where it was scrolled");
        host.Semantics().Where(node => node.Role == SemanticRole.TextField).Min(node => node.Bounds.Y)
            .Should().BeApproximately(frame.TextRegions.Min(region => region.Bounds.Y), 0.5f,
                "the screen is drawn where it was laid out");
    }

    /// <summary>A field a scroll view brought into view is above the keyboard already: the screen
    /// around it stays where it is.</summary>
    [Fact]
    public void AFieldAScrollViewReveals_IsNotLifted()
    {
        var host = new PhotonHost(new LongForm(), PhotonTheme.Instance, ThemeMode.Light, 390, 600);
        var frame = host.RenderFrame(new DisplayListBuilder());
        var low = frame.TextRegions.Last(region => region.Bounds.Bottom <= 600);
        Tap(host, low.Bounds.Center);
        host.RenderFrame(new DisplayListBuilder(), 16);

        host.ViewInsets = new EdgeInsets(0, 0, 0, 300);
        host.RenderFrame(new DisplayListBuilder(), 32);
        frame = host.RenderFrame(new DisplayListBuilder(), 48);
        host.RenderFrame(new DisplayListBuilder(), 64);

        var field = frame.TextRegions.Single(region => region.Path == low.Path).Bounds;
        host.CaretRect()!.Value.Y.Should().BeApproximately(field.Y, 0.5f);
    }

    private sealed class Padded : Primitives.StatefulComponent
    {
        public override VisualNode Build(ComponentContext context) =>
            new SafeArea(new Box(new BoxStyle { Width = SizeValue.Fill, Height = SizeValue.Fill }));
    }

    /// <summary>
    /// The home indicator's inset is under the keyboard once it rises, as Flutter's padding is under
    /// its view insets: a SafeArea at the bottom pads what the keyboard leaves of it, not both.
    /// </summary>
    [Fact]
    public void TheSafeAreaBelowTheKeyboard_CountsOnce()
    {
        var host = new PhotonHost(new Padded(), PhotonTheme.Instance, ThemeMode.Light, 390, 600)
        {
            SafeAreaInsets = new EdgeInsets(0, 47, 0, 34),
        };
        var frame = host.RenderFrame(new DisplayListBuilder());
        BoxOf(frame).Bounds.Bottom.Should().BeApproximately(600 - 34, 0.5f);

        host.ViewInsets = new EdgeInsets(0, 0, 0, 300);
        frame = host.RenderFrame(new DisplayListBuilder(), 16);

        BoxOf(frame).Bounds.Bottom.Should().BeApproximately(300, 0.5f,
            "the keyboard already covers the home indicator");
    }

    private static Framework.LayoutNode BoxOf(RealizeResult frame)
    {
        var node = frame.Root;
        while (node.Source is not Box && node.Children.Count > 0) node = node.Children[0];
        return node;
    }
}
