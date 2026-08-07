using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// IME composition at the host: a dead key or a CJK candidate is TEXT COMPOSED OVER SEVERAL
/// KEYSTROKES, held as marked text (shown inline, underlined) without the value changing, and
/// entering the field only on commit. These tests conduct the path the macOS shell's
/// NSTextInputClient methods drive — the platform decides WHEN to mark and commit; the host owns
/// WHAT that means.
/// </summary>
public class ImeCompositionTests
{
    private sealed class Page : Primitives.StatefulComponent
    {
        public string Value = "";

        public override VisualNode Build(ComponentContext context)
        {
            var column = new Column(gap: Space.S2) { Width = SizeValue.Fill };
            column.Add(new TextEntry(Value, v => SetState(() => Value = v)) { Placeholder = "Name" });
            return column;
        }
    }

    private static (PhotonHost Host, Page Page) Open(string initial = "")
    {
        var page = new Page { Value = initial };
        var host = new PhotonHost(page, PhotonTheme.Instance, ThemeMode.Light, 400, 200);
        var frame = host.RenderFrame(new DisplayListBuilder());
        var field = frame.TextRegions.Single().Bounds;
        host.PressDown(field.Center.X, field.Center.Y);
        host.PressUp(field.Center.X, field.Center.Y);
        return (host, page);
    }

    // ---- the contract ----------------------------------------------------------------------------

    [Fact]
    public void ADeadKeySequence_ComposesWithoutTouchingTheValue_AndCommitsOnce()
    {
        var (host, page) = Open();

        // Option+E on a US layout: the acute accent arrives as MARKED text…
        host.SetMarkedText("´").Should().BeTrue();
        host.MarkedText.Should().Be("´");
        page.Value.Should().Be("", "a composition in flight is the platform's text, not the field's");

        // …and the next key resolves the composition into one committed character.
        host.CommitText("á").Should().BeTrue();

        page.Value.Should().Be("á");
        host.HasMarkedText.Should().BeFalse();
    }

    [Fact]
    public void CancellingAComposition_LeavesTheFieldExactlyAsItWas()
    {
        var (host, page) = Open("abc");

        host.SetMarkedText("¨");
        host.SetMarkedText("");

        page.Value.Should().Be("abc");
        host.HasMarkedText.Should().BeFalse();
    }

    [Fact]
    public void ComposingOverASelection_ReplacesIt_TheSameRuleTypingHas()
    {
        var (host, page) = Open("abc");
        host.KeyDown("a", KeyModifiers.Command);           // select all

        host.SetMarkedText("は");
        host.RenderFrame(new DisplayListBuilder(), 16);

        page.Value.Should().Be("", "the selection went the way typing over it would take it");
        host.MarkedText.Should().Be("は");

        host.CommitText("葉");
        page.Value.Should().Be("葉");
    }

    [Fact]
    public void TheCompositionRidesTheCaret_NotTheEndOfTheField()
    {
        var (host, page) = Open("ab");
        host.KeyDown("ArrowLeft", KeyModifiers.None);      // caret between a and b

        host.SetMarkedText("´");
        host.MarkedRange.Should().Be((1, 1));

        host.CommitText("é");
        page.Value.Should().Be("aéb");
    }

    [Fact]
    public void LeavingTheField_DropsTheComposition()
    {
        var (host, _) = Open();
        host.SetMarkedText("´");

        host.KeyDown("Escape", KeyModifiers.None);         // leaves the field

        host.HasMarkedText.Should().BeFalse();
        host.HasTextFocus.Should().BeFalse();
    }

    [Fact]
    public void TheRangesAnswerWhatThePlatformAsks()
    {
        var (host, _) = Open("abcd");
        host.KeyDown("ArrowLeft", KeyModifiers.Shift);     // select the d

        host.SelectionRange.Should().Be((3, 1));
        host.MarkedRange.Should().Be((-1, 0), "nothing is being composed");
    }

    [Fact]
    public void TheCaretRect_AnchorsTheCandidateWindow()
    {
        var (host, _) = Open("abc");

        var rect = host.CaretRect();

        rect.Should().NotBeNull("the CJK candidate picker appears under the composition, not in a corner");
        var frame = host.RenderFrame(new DisplayListBuilder(), 32);
        var field = frame.TextRegions.Single().Bounds;
        rect!.Value.X.Should().BeInRange(field.Left, field.Right);
        rect.Value.Y.Should().Be(field.Top);
    }

    [Fact]
    public void AFrameWithACompositionInFlight_StillRegistersTheField()
    {
        var (host, _) = Open("abc");
        host.SetMarkedText("´");

        var frame = host.RenderFrame(new DisplayListBuilder(), 16);

        // The composed frame renders and the field keeps its region — the composition is drawing
        // state, never structure.
        frame.TextRegions.Should().HaveCount(1);
        host.MarkedText.Should().Be("´");
    }
}
