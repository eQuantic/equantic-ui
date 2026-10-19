using eQuantic.UI.Code;
using eQuantic.UI.Components;
using eQuantic.UI.Native.Components;
using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Engine.Reference;
using eQuantic.UI.Native.Engine.Tests.Golden;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;
using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The problems a <see cref="CodeEditor"/> shows and the card that explains the code (#299, #1005),
/// driven as a person drives them: a pointer resting on a word and leaving it, F8, Escape, a press on
/// the card. What is drawn is read from the frame: the waves and the dots under the code, the marks in
/// the gutter, and the card where the surface's notes landed, against where the engine says the code
/// they explain stands.
/// <para>
/// The cards show the moment the pointer rests and the providers answer at once, so a test reads the
/// card on the frame after the move. Answers apply on the thread they arrive on, which needs the
/// process's dispatcher unarmed (<see cref="ProcessDispatcherCollection"/>).
/// </para>
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeEditorProblemsTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeEditorProblemsTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    // ---- the fixtures ---------------------------------------------------------------------------

    private sealed class FixedWidthRasterizer : ITextRasterizer
    {
        public TextRaster? Rasterize(string content, TypeStyle style, float typeScale,
            float maxWidth, int maxLines, float scale, TextAlignment align)
        {
            if (string.IsNullOrEmpty(content)) return null;
            var width = Math.Max(1, (int)Math.Min(content.Length * 8 * scale, maxWidth * scale));
            var height = Math.Max(1, (int)(style.LineHeight * scale));
            return new TextRaster(width, height, new byte[width * height]);
        }
    }

    /// <summary>Explains every word it is asked about, at once.</summary>
    private sealed class Explainer : ICodeHoverProvider
    {
        public Task<CodeHover?> HoverAsync(CodeDocument document, CodePosition position, CancellationToken cancellation)
        {
            var word = document.WordAt(position);
            if (word.IsEmpty) return Task.FromResult<CodeHover?>(null);
            var name = document.TextIn(word);
            return Task.FromResult<CodeHover?>(new CodeHover(word, $"About {name}.") { Signature = $"int {name}" });
        }
    }

    /// <summary>Reports what the test hands it, at once, and counts how often it was asked.</summary>
    private sealed class Checker(params CodeDiagnostic[] found) : ICodeDiagnosticProvider
    {
        public int Asked { get; private set; }

        public Task<IReadOnlyList<CodeDiagnostic>> DiagnoseAsync(CodeDocument document, CancellationToken cancellation)
        {
            Asked++;
            return Task.FromResult<IReadOnlyList<CodeDiagnostic>>(found);
        }
    }

    /// <summary>A parent that hands its editor the next configuration on a rebuild, which the editor's
    /// position adopts before it builds again.</summary>
    private sealed class Parent(CodeEditor first) : StatefulComponent
    {
        private CodeEditor _current = first;

        public void Hand(CodeEditor next) => SetState(() => _current = next);

        public override VisualNode Build(ComponentContext context) => _current;
    }

    private static PhotonHost Host(VisualNode root, float width = 600, float height = 400) =>
        new(root, PhotonTheme.Instance, ThemeMode.Light, width, height)
        {
            TextRasterizer = new FixedWidthRasterizer(),
            Density = Density.Compact,
            SmoothScroll = false,
        };

    private static RealizeResult Frame(PhotonHost host) => host.RenderFrame(new DisplayListBuilder());

    /// <summary>Two frames: the second knows how big the viewport turned out to be.</summary>
    private static RealizeResult Settle(PhotonHost host)
    {
        Frame(host);
        return Frame(host);
    }

    /// <summary>Twenty lines, each declaring a name a card can explain: line L is <c>var nameL = L;</c>.</summary>
    private static string Lines(int count = 20) =>
        string.Join("\n", Enumerable.Range(0, count).Select(line => $"var name{line} = {line};"));

    private static CodeEditor Editor(string code, bool explains = true) =>
        new(code, "csharp")
        {
            Height = SizeValue.Fill,
            HoverProviders = explains ? [new Explainer()] : [],
        };

    /// <summary>The editor's controller, with its waits taken out, so a card shows as the pointer rests.</summary>
    private static CodeEditorController Controller(CodeEditor editor)
    {
        var controller = editor.Editor;
        controller.Hover.Delay = TimeSpan.Zero;
        controller.Diagnostics.Delay = TimeSpan.Zero;
        return controller;
    }

    private static CodeDiagnostic Problem(int line, int from, int to, string message,
        CodeDiagnosticSeverity severity = CodeDiagnosticSeverity.Error) =>
        new(new CodeRange(new CodePosition(line, from), new CodePosition(line, to)), message, severity)
        {
            Code = "CS0103",
            Source = "csharp",
        };

    /// <summary>Where the character at <paramref name="position"/> stands on screen.</summary>
    private static Rect OnScreen(RealizeResult frame, CodeEditor editor, CodePosition position)
    {
        var region = frame.CodeRegions.Single();
        var at = editor.Editor.CaretRect(position);
        return at with { X = region.Bounds.X + at.X, Y = region.Bounds.Y + at.Y };
    }

    /// <summary>The pointer coming to rest on the middle of the character at <paramref name="line"/>,
    /// <paramref name="column"/>, and the frames after it.</summary>
    private static RealizeResult RestOn(PhotonHost host, CodeEditor editor, int line, int column)
    {
        var at = OnScreen(Frame(host), editor, new CodePosition(line, column));
        host.PointerMove(at.X + editor.Editor.Grid.Cell.Width / 2, at.Y + at.Height / 2);
        return Settle(host);
    }

    /// <summary>Puts the caret at the start of the code with a press, as a person does.</summary>
    private static void ClickInto(PhotonHost host, CodeEditor editor)
    {
        var at = OnScreen(Frame(host), editor, new CodePosition(0, 0));
        host.PressDown(at.X + 1, at.Y + at.Height / 2);
        host.PressUp(at.X + 1, at.Y + at.Height / 2);
        Settle(host);
    }

    private static RealizeResult Press(PhotonHost host, string key, KeyModifiers modifiers = KeyModifiers.None)
    {
        host.KeyDown(key, modifiers);
        return Settle(host);
    }

    private static IEnumerable<LayoutNode> Descendants(LayoutNode node)
    {
        yield return node;
        foreach (var child in node)
            foreach (var descendant in Descendants(child))
                yield return descendant;
    }

    /// <summary>The drawings laid out in the frame: the waves and the dots under the code.</summary>
    private static List<(Rect Bounds, Drawing Drawing)> Drawings(RealizeResult frame) =>
        [.. Descendants(frame.Root).Where(node => node.Source is Drawing).Select(node => (node.Bounds, (Drawing)node.Source))];

    /// <summary>The gutter's marks, by their colour: the dots a marker is drawn as.</summary>
    private static List<(Rect Bounds, ColorToken Color)> Marks(RealizeResult frame) =>
        [.. Descendants(frame.Root)
            .Where(node => node.Source is Box { Style.Background: { } } && node.Bounds is { Width: 7, Height: 7 })
            .Select(node => (node.Bounds, ((Box)node.Source).Style.Background!.Value))];

    private static List<string> Texts(RealizeResult frame) =>
        [.. Descendants(frame.Root).Select(node => node.Source).OfType<Text>().Select(text => text.Content)];

    private static ColorToken Destructive => PhotonTheme.Instance.Colors(Variant.Destructive).Base;

    private static ColorToken Warning => PhotonTheme.Instance.Colors(Variant.Warning).Base;

    // ---- what a problem draws -------------------------------------------------------------------

    [Fact]
    public void AnError_IsAWaveUnderItsRange_InTheErrorsColour()
    {
        var editor = Editor(Lines());
        editor.Diagnostics = [Problem(3, 4, 9, "The name 'name3' is unused.")];
        var host = Host(editor);

        var frame = Settle(host);

        var (bounds, wave) = Drawings(frame).Should().ContainSingle("one problem, one wave").Subject;
        wave.Tint.Should().Be(Destructive, "an error is drawn in the destructive colour");
        var start = OnScreen(frame, editor, new CodePosition(3, 4));
        bounds.X.Should().BeApproximately(start.X, 0.5f, "the wave starts under the range's first character");
        bounds.Width.Should().BeApproximately(5 * editor.Editor.Grid.Cell.Width, 0.5f, "and runs under its five");
        bounds.Bottom.Should().BeApproximately(start.Y + start.Height, 0.5f, "at the bottom of the line");
    }

    [Fact]
    public void AHint_IsThreeDots_AndMarksNoLineInTheGutter()
    {
        var editor = Editor(Lines());
        editor.Diagnostics = [Problem(2, 4, 9, "Could be a constant.", CodeDiagnosticSeverity.Hint)];
        var host = Host(editor);

        var frame = Settle(host);

        var (bounds, dots) = Drawings(frame).Should().ContainSingle().Subject;
        dots.Height.Should().Be(2, "a hint is the three dots, 2dp tall, not a wave");
        bounds.X.Should().BeApproximately(OnScreen(frame, editor, new CodePosition(2, 4)).X, 0.5f);
        Marks(frame).Should().BeEmpty("a hint calls nothing wrong, and the gutter is where the eye looks for what is");
    }

    [Fact]
    public void TheGutterMarksEachLine_WithItsWorstProblem()
    {
        var editor = Editor(Lines());
        editor.Diagnostics =
        [
            Problem(1, 0, 3, "An error first."),
            Problem(1, 4, 9, "A warning after it.", CodeDiagnosticSeverity.Warning),
            Problem(4, 4, 9, "Only a warning here.", CodeDiagnosticSeverity.Warning),
            Problem(6, 4, 9, "Information marks nothing.", CodeDiagnosticSeverity.Information),
        ];
        var host = Host(editor);

        var frame = Settle(host);

        var marks = Marks(frame);
        marks.Should().HaveCount(2, "the lines with an error or a warning are marked, once each");
        LineOf(frame, editor, marks.Single(mark => mark.Color == Destructive).Bounds).Should().Be(1,
            "the error outweighs the warning on its line");
        LineOf(frame, editor, marks.Single(mark => mark.Color == Warning).Bounds).Should().Be(4);
    }

    [Fact]
    public void TheAppsOwnMarker_KeepsItsLine()
    {
        var editor = Editor(Lines());
        editor.GutterMarkers = [new CodeGutterMarker(1, CodeGutterKind.Breakpoint)];
        editor.Diagnostics = [Problem(1, 4, 9, "An error on a breakpoint's line.", CodeDiagnosticSeverity.Warning)];
        var host = Host(editor);

        var frame = Settle(host);

        Marks(frame).Should().ContainSingle("the line holds one marker, the app's")
            .Which.Color.Should().Be(PhotonTheme.Instance.Colors(Variant.Destructive).Base,
                "a breakpoint's colour, and no warning's beside it");
    }

    /// <summary>The line whose row the gutter mark at <paramref name="mark"/> sits in.</summary>
    private static int LineOf(RealizeResult frame, CodeEditor editor, Rect mark)
    {
        var top = OnScreen(frame, editor, new CodePosition(0, 0)).Y;
        return (int)((mark.Y + mark.Height / 2 - top) / editor.Editor.Grid.Cell.Height);
    }

    [Fact]
    public void AnEmptyRange_IsDrawnUnderTheCharacterThere()
    {
        var editor = Editor(Lines());
        editor.Diagnostics = [new CodeDiagnostic(new CodeRange(new CodePosition(2, 4)), "Expected ';'.")];
        var host = Host(editor);

        var frame = Settle(host);

        var (bounds, _) = Drawings(frame).Should().ContainSingle().Subject;
        bounds.X.Should().BeApproximately(OnScreen(frame, editor, new CodePosition(2, 4)).X, 0.5f);
        bounds.Width.Should().BeApproximately(editor.Editor.Grid.Cell.Width, 0.5f,
            "a mark of no width says nothing, so it is one character wide");
    }

    [Fact]
    public void AnEmptyRangeOnAnEmptyLine_IsDrawnOneCellWide()
    {
        var editor = Editor("var a = 1;\n\nvar b = 2;");
        editor.Diagnostics = [new CodeDiagnostic(new CodeRange(new CodePosition(1, 0)), "Expected a statement.")];
        var host = Host(editor);

        var frame = Settle(host);

        var (bounds, _) = Drawings(frame).Should().ContainSingle("a problem of no width is still drawn").Subject;
        bounds.X.Should().BeApproximately(OnScreen(frame, editor, new CodePosition(1, 0)).X, 0.5f);
        bounds.Width.Should().BeApproximately(editor.Editor.Grid.Cell.Width, 0.5f);
    }

    /// <summary>Providers taken away take what they found with them, at once (found reviewing #299).</summary>
    [Fact]
    public void ProvidersTakenAway_TakeTheirProblems()
    {
        var checker = new Checker(Problem(5, 4, 9, "Found by the checker."));
        var first = Editor(Lines());
        Controller(first);
        first.DiagnosticProviders = [checker];
        var parent = new Parent(first);
        var host = Host(parent);
        Drawings(Settle(host)).Should().ContainSingle();

        parent.Hand(Editor(Lines()));
        var frame = Settle(host);

        first.Editor.Diagnostics.Items.Should().BeEmpty();
        Drawings(frame).Should().BeEmpty();
    }

    [Fact]
    public void TypingBeforeAProblem_CarriesItsWaveWithTheText()
    {
        var editor = Editor(Lines());
        editor.Diagnostics = [Problem(0, 4, 9, "Unused.")];
        var host = Host(editor);
        Settle(host);
        ClickInto(host, editor);

        host.TextInput("ab");
        var frame = Settle(host);

        editor.Editor.Diagnostics.Items.Single().Range.Start.Column.Should().Be(6, "the problem moved with its text");
        Drawings(frame).Single().Bounds.X.Should().BeApproximately(OnScreen(frame, editor, new CodePosition(0, 6)).X, 0.5f,
            "and its wave with it");
    }

    /// <summary>
    /// A parent that builds again with other problems (a build that finished, an analyzer that ran)
    /// hands them to the editor its position keeps, which reports them in place of the last ones.
    /// </summary>
    [Fact]
    public void OtherProblemsHandedByTheParent_ReplaceTheLastOnes()
    {
        var first = Editor(Lines());
        first.Diagnostics = [Problem(1, 4, 9, "First build.")];
        var parent = new Parent(first);
        var host = Host(parent);
        Settle(host);

        var next = Editor(Lines());
        next.Diagnostics = [Problem(6, 4, 9, "Second build.")];
        parent.Hand(next);
        var frame = Settle(host);

        first.Editor.Diagnostics.Items.Should().ContainSingle().Which.Message.Should().Be("Second build.");
        Drawings(frame).Should().ContainSingle()
            .Which.Bounds.Y.Should().BeGreaterThan(OnScreen(frame, first, new CodePosition(6, 0)).Y,
                "the wave is under line 6 now");
    }

    /// <summary>
    /// A parent that rebuilds with the list of problems it keeps hands the editor the same problems
    /// again, which change nothing: reported again, the problem an edit carried went back to where it
    /// stood before the edit.
    /// </summary>
    [Fact]
    public void TheSameProblemsHandedAgain_LeaveTheCarriedOnesWhereTheyAre()
    {
        IReadOnlyList<CodeDiagnostic> kept = [Problem(0, 4, 9, "Unused.")];
        var first = Editor(Lines());
        first.Diagnostics = kept;
        var parent = new Parent(first);
        var host = Host(parent);
        Settle(host);
        ClickInto(host, first);
        host.TextInput("ab");
        Settle(host);

        var next = Editor(Lines());
        next.Diagnostics = [.. kept];
        parent.Hand(next);
        Settle(host);

        first.Editor.Diagnostics.Items.Single().Range.Start.Column.Should().Be(6);
    }

    [Fact]
    public void AProviderHandedToTheEditor_IsAsked_AndWhatItFoundIsDrawn()
    {
        var checker = new Checker(Problem(5, 4, 9, "Found by the checker."));
        var editor = Editor(Lines());
        Controller(editor);
        editor.DiagnosticProviders = [checker];
        var host = Host(editor);

        var frame = Settle(host);

        checker.Asked.Should().BeGreaterThan(0, "a provider is asked when it is handed");
        Drawings(frame).Should().ContainSingle()
            .Which.Bounds.Y.Should().BeGreaterThan(OnScreen(frame, editor, new CodePosition(5, 0)).Y);
    }

    /// <summary>A status bar's parent: it counts the problems, building again whenever they change, and
    /// makes its provider anew in every build, as a parent makes a list anew.</summary>
    private sealed class Counting(CodeDiagnostic found) : StatefulComponent
    {
        private CodeEditor? _shown;

        public int Built { get; private set; }

        public override VisualNode Build(ComponentContext context)
        {
            Built++;
            var editor = new CodeEditor(Lines(), "csharp") { DiagnosticProviders = [new Checker(found)] };
            if (_shown is null)
            {
                // The instance the position keeps: later ones only hand it their configuration.
                _shown = editor;
                editor.Editor.Diagnostics.Changed += () => SetState(() => { });
            }
            return editor;
        }
    }

    /// <summary>
    /// A provider made in every build is a new one every build, and asked at once, as a controller made
    /// in Build is a new one in Flutter. Its answer finds what the last one did and raises nothing, so
    /// the builds stop: a parent that builds again on the problems used to be asked for a build by the
    /// answer of the provider its own build handed, without end.
    /// </summary>
    [Fact]
    public void AProviderMadeInEveryBuild_Settles()
    {
        var parent = new Counting(Problem(5, 4, 9, "Found anew."));
        var host = Host(parent);

        for (var frame = 0; frame < 6; frame++) Frame(host);

        host.NeedsRender.Should().BeFalse("the answer that found the same problems asked for no frame");
        parent.Built.Should().BeLessThan(4, "the parent built for the first answer, and not for every one after it");
    }

    // ---- the card -------------------------------------------------------------------------------

    [Fact]
    public void APointerAtRestOnAWord_ShowsTheCardAboveItsLine()
    {
        var editor = Editor(Lines());
        Controller(editor);
        var host = Host(editor);
        Settle(host);

        var frame = RestOn(host, editor, 10, 6);

        var note = frame.CodeRegions.Single().Notes.Should().ContainSingle("the card is the surface's note").Subject;
        var word = OnScreen(frame, editor, new CodePosition(10, 4));
        note.Bottom.Should().BeApproximately(word.Y, 0.5f, "the card ends at the top of the line it explains");
        note.X.Should().BeApproximately(word.X, 0.5f, "and starts where the word does");
        Texts(frame).Should().Contain(["int name10", "About name10."], "it shows the signature and the text");
        host.Semantics().Should().NotContain(node => node.Live != null,
            "a card the pointer asked for is no live region: a pointer resting asks nobody to speak");
    }

    /// <summary>
    /// An answer long enough to wrap is measured as it wraps, between words: counted as its width over
    /// the card's, the lines a wrap leaves half empty were counted full, and its last line was cut
    /// (found reviewing #1005).
    /// </summary>
    [Fact]
    public void ALongAnswer_WrapsBetweenWords_AndTheCardStillEndsAtItsLine()
    {
        var editor = Editor(Lines(), explains: false);
        editor.HoverProviders = [new Wordy()];
        Controller(editor);
        var host = Host(editor);
        Settle(host);

        var frame = RestOn(host, editor, 12, 6);

        var answer = Descendants(frame.Root).Select(node => node.Source).OfType<Text>()
            .Single(text => text.Content == Wordy.Text);
        answer.MaxLines.Should().Be(3, "one long word a line: none of the text is cut");
        var note = frame.CodeRegions.Single().Notes!.Single();
        note.Bottom.Should().BeApproximately(OnScreen(frame, editor, new CodePosition(12, 4)).Y, 0.5f,
            "and the card is as tall as its wrapped text");
    }

    /// <summary>Explains every word with three long words, each wider than half the card: one a line,
    /// where their width over the card's counts two.</summary>
    private sealed class Wordy : ICodeHoverProvider
    {
        public const string Text = "LedgerAccountingInvoicePaymentRefundTransfer "
            + "DepositWithdrawalReferenceIssuedAtTotalAmounts ToStringReferenceTotalAmountsLedgerAccounts";

        public Task<CodeHover?> HoverAsync(CodeDocument document, CodePosition position, CancellationToken cancellation) =>
            Task.FromResult<CodeHover?>(new CodeHover(document.WordAt(position), Text));
    }

    [Fact]
    public void OnTheFirstLine_TheCardStandsBelowIt()
    {
        var editor = Editor(Lines());
        Controller(editor);
        var host = Host(editor);
        Settle(host);

        var frame = RestOn(host, editor, 0, 6);

        var note = frame.CodeRegions.Single().Notes!.Single();
        var word = OnScreen(frame, editor, new CodePosition(0, 4));
        note.Y.Should().BeApproximately(word.Y + word.Height, 0.5f, "with no room above, the card stands below the line");
    }

    [Fact]
    public void ACardOnAProblem_SaysTheProblemFirst()
    {
        var editor = Editor(Lines());
        Controller(editor);
        editor.Diagnostics = [Problem(10, 4, 10, "The name 'name10' is unused.")];
        var host = Host(editor);
        Settle(host);

        var frame = RestOn(host, editor, 10, 6);

        var texts = Texts(frame);
        var problem = texts.IndexOf("The name 'name10' is unused.  csharp CS0103");
        problem.Should().BeGreaterThanOrEqualTo(0, "the problem's message, with its source and its rule");
        texts.IndexOf("About name10.").Should().BeGreaterThan(problem, "and what the provider says after it");
    }

    [Fact]
    public void ThePointerLeavingTheEditor_ClosesTheCard()
    {
        var editor = Editor(Lines());
        Controller(editor);
        var host = Host(editor);
        Settle(host);
        RestOn(host, editor, 10, 6).CodeRegions.Single().Notes.Should().NotBeNull();

        host.PointerLeave();
        var frame = Settle(host);

        editor.Editor.Hover.IsOpen.Should().BeFalse();
        frame.CodeRegions.Single().Notes.Should().BeNull("the card is gone with the pointer");
    }

    [Fact]
    public void ThePointerOnTheCard_KeepsItOpen_AndAPressThereMovesNoCaret()
    {
        var editor = Editor(Lines());
        Controller(editor);
        var host = Host(editor);
        Settle(host);
        ClickInto(host, editor);
        var note = RestOn(host, editor, 10, 6).CodeRegions.Single().Notes!.Single();

        // Up onto the card, over the line it hides.
        var x = note.X + note.Width / 2;
        var y = note.Y + note.Height / 2;
        host.PointerMove(x, y);
        Settle(host);
        editor.Editor.Hover.IsOpen.Should().BeTrue("the pointer on the card is the card's, not the code's under it");

        host.PressDown(x, y);
        host.PressUp(x, y);
        Settle(host);

        editor.Editor.Selection.Should().Be(new CodeRange(new CodePosition(0, 0)), "a press on the card moves no caret");
        host.CodeTarget.Should().NotBeNull("and the code keeps the keyboard");
    }

    /// <summary>
    /// A pointer moving with a press held is dragging, wherever the press began: over the code it rests
    /// on nothing, as the web twin reads from a move's buttons. Once it lifts, resting on a word brings
    /// up the card.
    /// </summary>
    [Fact]
    public void APointerDraggedOverTheCode_ShowsNoCardUntilItLifts()
    {
        var editor = new CodeEditor(Lines(), "csharp") { Height = 300, HoverProviders = [new Explainer()] };
        Controller(editor);
        var column = new Column { Width = SizeValue.Fill };
        column.Add(new Button("Run", onPressed: () => { }));
        column.Add(editor);
        var host = Host(column);
        var frame = Settle(host);
        var run = frame.HitRegions.Single(region => region.Node.Role == PressableRole.Button).Bounds;
        var word = OnScreen(frame, editor, new CodePosition(10, 6));

        host.PressDown(run.X + 2, run.Y + 2);
        host.PointerMove(word.X + 3, word.Y + word.Height / 2);
        Settle(host);
        editor.Editor.Hover.IsOpen.Should().BeFalse("a pointer that drags rests on nothing");

        host.PressUp(word.X + 3, word.Y + word.Height / 2);
        host.PointerMove(word.X + 4, word.Y + word.Height / 2);
        Settle(host);
        editor.Editor.Hover.IsOpen.Should().BeTrue("lifted, it rests, and the card shows");
    }

    /// <summary>A press the platform cancelled, with no release after it, is over too: the pointer moving
    /// next rests on the code (found by Copilot reviewing #1008).</summary>
    [Fact]
    public void APressCancelled_LeavesThePointerToRestOnTheCode()
    {
        var editor = new CodeEditor(Lines(), "csharp") { Height = 300, HoverProviders = [new Explainer()] };
        Controller(editor);
        var column = new Column { Width = SizeValue.Fill };
        column.Add(new Button("Run", onPressed: () => { }));
        column.Add(editor);
        var host = Host(column);
        var frame = Settle(host);
        var run = frame.HitRegions.Single(region => region.Node.Role == PressableRole.Button).Bounds;
        var word = OnScreen(frame, editor, new CodePosition(10, 6));

        host.PressDown(run.X + 2, run.Y + 2);
        host.PointerCancel();
        host.PointerMove(word.X + 3, word.Y + word.Height / 2);
        Settle(host);

        editor.Editor.Hover.IsOpen.Should().BeTrue("the press was taken away, and the pointer rests on the code");
    }

    [Fact]
    public void F8_GoesToTheNextProblem_AndShowsItInALiveRegion()
    {
        var editor = Editor(Lines());
        Controller(editor);
        editor.Diagnostics = [Problem(8, 4, 9, "The name 'name8' is unused.")];
        var host = Host(editor);
        Settle(host);
        ClickInto(host, editor);

        var frame = Press(host, "F8");

        editor.Editor.Selection.Should().Be(new CodeRange(new CodePosition(8, 4)), "F8 puts the caret on the problem");
        frame.CodeRegions.Single().Notes.Should().ContainSingle("and shows its card");
        Texts(frame).Should().Contain("The name 'name8' is unused.  csharp CS0103");
        host.Semantics().Should().Contain(node => node.Live == LiveRegionUrgency.Polite,
            "a card a key brought up is read out where the person is");
    }

    /// <summary>
    /// F8 to a problem past the view's right edge slides the code to it, and the card goes with the
    /// view. It was placed by how far the code had slid before the key, which only the completion
    /// list asked to be told of, and stood where the view no longer was (found in the browser, on the
    /// dashboard's code screen).
    /// </summary>
    [Fact]
    public void F8ToAProblemPastTheRightEdge_KeepsTheCardInsideTheView()
    {
        var editor = new CodeEditor("var line = \"" + new string('x', 300) + "\";\n" + Lines(), "csharp")
        {
            Height = SizeValue.Fill,
        };
        Controller(editor);
        editor.Diagnostics = [Problem(0, 250, 260, "Far to the right.")];
        var host = Host(editor);
        Settle(host);
        ClickInto(host, editor);

        Press(host, "F8");
        var frame = Settle(host);

        editor.Editor.Caret.Should().Be(new CodePosition(0, 250));
        var region = frame.CodeRegions.Single();
        var visible = region.Visible ?? region.Bounds;
        OnScreen(frame, editor, editor.Editor.Caret).X.Should().BeLessThan(visible.Right, "the code slid to the caret");
        var note = region.Notes!.Single();
        note.Right.Should().BeLessThanOrEqualTo(visible.Right + 0.5f, "the card's right edge stays in the view");
        note.Left.Should().BeGreaterThanOrEqualTo(visible.Left - 0.5f, "and so does its left");
    }

    /// <summary>
    /// …and it does on the web too, where the code slides outside any build: the browser's own scroll
    /// (a caret revealed, a wheel) reports how far it slid, and while a card shows, that has to ask for
    /// the build that places the card again. It asked only while the completion list showed, and the
    /// card F8 brought up stood where the view had been. Photon builds the editor every frame, so the
    /// ask is what this reads.
    /// </summary>
    [Fact]
    public void ACardShowing_AsksForABuildWhenTheCodeSlides()
    {
        var editor = Editor(Lines());
        var controller = Controller(editor);
        var host = Host(editor);
        Settle(host);
        controller.Hover.ShowAt(new CodePosition(10, 4)).Should().BeTrue();
        var frame = Settle(host);
        host.NeedsRender.Should().BeFalse("the frames the card asked for were drawn");
        var slide = Descendants(frame.Root).Select(node => node.Source).OfType<ScrollView>()
            .Single(view => view.Axis == ScrollAxis.Horizontal);

        slide.OnScrolled!(120);

        host.NeedsRender.Should().BeTrue("the card is placed by how far the code slid");
    }

    [Fact]
    public void Escape_ClosesTheCard_AndTheCodeKeepsTheKeyboard()
    {
        var editor = Editor(Lines());
        Controller(editor);
        editor.Diagnostics = [Problem(8, 4, 9, "Unused.")];
        var host = Host(editor);
        Settle(host);
        ClickInto(host, editor);
        Press(host, "F8").CodeRegions.Single().Notes.Should().NotBeNull();

        var frame = Press(host, "Escape");

        frame.CodeRegions.Single().Notes.Should().BeNull("Escape closes the card");
        host.CodeTarget.Should().NotBeNull();
    }

    [Fact]
    public void TheListOpening_TakesTheCardsPlace()
    {
        var editor = Editor(Lines());
        var controller = Controller(editor);
        var host = Host(editor);
        Settle(host);
        ClickInto(host, editor);
        controller.Hover.ShowAt(new CodePosition(0, 4)).Should().BeTrue();
        Settle(host).CodeRegions.Single().Notes.Should().NotBeNull("the card shows");

        var frame = Press(host, " ", KeyModifiers.Control);

        controller.Completion.IsOpen.Should().BeTrue("⌃Space opens the list");
        frame.CodeRegions.Single().Offered.Should().NotBeNull("which is drawn");
        frame.CodeRegions.Single().Notes.Should().BeNull("and the card is not: one thing at a time over the caret");
    }

    // ---- a turned surface -----------------------------------------------------------------------

    /// <summary>The editor in a box of 300 by 200 at 200, 200, turned <paramref name="degrees"/> about
    /// its centre, (350, 300), showing the card for line 3's name.</summary>
    private static (PhotonHost Host, CodeEditor Editor) TurnedCard(float degrees)
    {
        var editor = new CodeEditor(Lines(8), "csharp") { ShowLineNumbers = false, HoverProviders = [new Explainer()] };
        var turned = new Box(new BoxStyle { Width = 300, Height = 200, Transform = Transform2D.Rotate(degrees) }, editor);
        var host = Host(new Box(new BoxStyle { Padding = EdgeInsets.All(200) }, turned), 800, 700);
        Settle(host);
        editor.Editor.Hover.ShowAt(new CodePosition(3, 5)).Should().BeTrue();
        Settle(host);
        return (host, editor);
    }

    /// <summary>Where <paramref name="point"/>, laid out in the box of <see cref="TurnedCard"/>, is drawn
    /// with the box turned <paramref name="degrees"/> about its centre.</summary>
    private static Point Turned(Point point, float degrees)
    {
        var drawn = Matrix2D.Rotation(degrees * MathF.PI / 180).Transform(new Point(point.X - 350, point.Y - 300));
        return new Point(350 + drawn.X, 300 + drawn.Y);
    }

    /// <summary>
    /// A card on a turned surface takes the pointer in its own shape, as the list does: the box around
    /// it on screen holds corners no part of it is drawn in, and the code drawn there keeps its beam
    /// and its presses.
    /// </summary>
    [Fact]
    public void ACardOnATurnedSurface_TakesThePointerInItsOwnShape()
    {
        // Laid out unturned first, to read where the code and the card stand in the space they keep.
        var (plain, _) = TurnedCard(0);
        var code = plain.LastFrame!.CodeRegions.Single().Bounds;
        var card = plain.LastFrame!.CodeRegions.Single().Notes!.Single();

        const float degrees = 30;
        var (host, editor) = TurnedCard(degrees);
        var box = host.LastFrame!.CodeRegions.Single().Notes!.Single();

        // The corner of the box around the turned card farthest from the card, once taken back into the
        // surface's space, and inside the code.
        float Outside(Point local) =>
            MathF.Max(MathF.Max(card.Left - local.X, local.X - card.Right), MathF.Max(card.Top - local.Y, local.Y - card.Bottom));
        var corner = new Point[]
            {
                new(box.Left + 2, box.Top + 2), new(box.Right - 2, box.Top + 2),
                new(box.Left + 2, box.Bottom - 2), new(box.Right - 2, box.Bottom - 2),
            }
            .Where(point => code.Contains(Turned(point, -degrees)))
            .MaxBy(point => Outside(Turned(point, -degrees)));
        Outside(Turned(corner, -degrees)).Should().BeGreaterThan(4, "the corner is outside the card, in its own space");
        var middle = Turned(new Point(card.X + card.Width / 2, card.Y + card.Height / 2), degrees);

        host.CursorAt(middle.X, middle.Y).Should().Be(CursorShape.Default, "over the card, the pointer is the card's");
        host.CursorAt(corner.X, corner.Y).Should().Be(CursorShape.Text, "beside it, the code is drawn, and no part of the card");
        var caret = editor.Editor.Caret;
        host.PressDown(corner.X, corner.Y);
        host.PressUp(corner.X, corner.Y);
        Settle(host);
        editor.Editor.Caret.Should().NotBe(caret, "a press there lands on the code and moves its caret");
    }

    // ---- pixels ---------------------------------------------------------------------------------

    /// <summary>
    /// The problems and the card, light and dark, on the reference backend: an error's wave and a
    /// warning's in their colours, a hint's dots, the gutter's marks, and the card F8 brought up over
    /// the error, with its mark, its message, its source and its rule. The state the picture is of is
    /// asserted beside it, so a golden cannot bless a card that shows the wrong thing.
    /// </summary>
    [Theory]
    [InlineData(ThemeMode.Light, "code-problems-light")]
    [InlineData(ThemeMode.Dark, "code-problems-dark")]
    public void TheProblemsAndTheCard_RenderThePhotonPixels(ThemeMode mode, string golden)
    {
        var editor = new CodeEditor(Lines(8), "csharp") { Height = SizeValue.Fill };
        Controller(editor);
        editor.Diagnostics =
        [
            Problem(4, 4, 9, "The name 'name4' does not exist."),
            Problem(6, 4, 9, "Prefer a constant.", CodeDiagnosticSeverity.Warning),
            Problem(7, 4, 9, "Could be inlined.", CodeDiagnosticSeverity.Hint),
        ];
        using var backend = new ReferenceBackend();
        using var surface = backend.CreateSurface(360, 240);
        var host = new PhotonHost(editor, PhotonTheme.Instance, mode, 360, 240) { Density = Density.Compact };
        Settle(host);
        ClickInto(host, editor);
        Press(host, "F8");
        host.RenderFrame(new DisplayListBuilder(), timeMs: 500);
        var builder = new DisplayListBuilder();
        host.RenderFrame(builder, timeMs: 1000);
        backend.Render(builder.Build(), surface);

        editor.Editor.Caret.Should().Be(new CodePosition(4, 4), "F8 reached the error");
        editor.Editor.Hover.Problems.Should().ContainSingle().Which.Severity.Should().Be(CodeDiagnosticSeverity.Error);
        GoldenImage.Match(surface, golden);
    }
}
