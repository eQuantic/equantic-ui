using eQuantic.UI.Code;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;
using FluentAssertions;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Where a name is defined and used (#1084), driven as a person drives it, with no screen: F12, ⌘F12 and ⇧F12 as
/// VS Code's <c>SymbolNavigationAction</c> answers them (one place gone to, several listed, none said), the list
/// of places in the go-to field, F4 and ⇧F4 through it shown and closed, a place in another document handed to
/// the app, and the link ⌘ shows over a name, as VS Code's <c>ClickLinkGesture</c> shows it.
/// </summary>
[Collection(ProcessDispatcherCollection.Name)]
public class CodeDefinitionsTests : IDisposable
{
    private readonly IUiDispatcher? _outer = UiDispatcher.Current;

    public CodeDefinitionsTests() => UiDispatcher.Current = null;

    public void Dispose() => UiDispatcher.Current = _outer;

    private const KeyModifiers Command = KeyModifiers.Command;
    private const KeyModifiers Shift = KeyModifiers.Shift;

    /// <summary>
    /// Three hundred lines: <c>total</c> declared on line 9 (counted from 0) and used on lines 19, 29 and 39, two of
    /// them <c>total += amount;</c>; <c>Print</c> called on line 59 and defined in another document; <c>Run</c>
    /// declared on line 69 and implemented on lines 89 and 99; <c>far</c> used on line 49 and declared on line 199.
    /// A view 400 wide and 800 high over lines 20 high: forty lines.
    /// </summary>
    private static CodeEditorController Editor(CodePosition caret)
    {
        var lines = Enumerable.Range(0, 300).Select(i => $"    // line {i}").ToArray();
        lines[0] = "class Ledger";
        lines[9] = "    int total;";
        lines[19] = "        total += amount;";
        lines[29] = "        total += amount;";
        lines[39] = "        return total;";
        lines[49] = "        far = 2;";
        lines[59] = "        Print();";
        lines[69] = "    void Run();";
        lines[89] = "    void Run() { }";
        lines[99] = "    void Run() { }";
        lines[199] = "    int far;";
        var editor = new CodeEditorController(string.Join("\n", lines), CodeLanguages.CSharp)
        {
            Grid = new CodeGrid(Point.Zero, new Size(8, 20)),
        };
        editor.Selection = new CodeRange(caret);
        editor.View = new Rect(0, 0, 400, 800);
        return editor;
    }

    private static CodeEditorController WithProviders(CodePosition caret, out Defining definitions, out Referring references)
    {
        var editor = Editor(caret);
        definitions = new Defining();
        references = new Referring();
        editor.Definitions.Providers.Add(definitions);
        editor.References.Providers.Add(references);
        return editor;
    }

    private static CodePosition At(int line, int column) => new(line, column);

    private static CodeLocation Place(int line, int from, int to) => new(new CodeRange(At(line, from), At(line, to)));

    private static bool Press(CodeEditorController editor, string key, KeyModifiers modifiers = KeyModifiers.None,
        KeyboardConvention convention = KeyboardConvention.Apple) =>
        editor.HandleKey(key, modifiers, convention, null);

    private static string WordAt(CodeDocument document, CodePosition at)
    {
        var line = document.Line(at.Line);
        var column = at.Column < line.Length && CodeDocument.IsWordChar(line[at.Column]) ? at.Column
            : at.Column > 0 && CodeDocument.IsWordChar(line[at.Column - 1]) ? at.Column - 1 : -1;
        return column < 0 ? "" : document.TextIn(document.WordAt(At(at.Line, column)));
    }

    /// <summary>A language service of the document above: what it declares, where, and of what type.</summary>
    private sealed class Defining : ICodeDefinitionProvider
    {
        public int Asked;

        public Task<IReadOnlyList<CodeLocation>> DefinitionsAsync(CodeDocument document, CodePosition position,
            CodeDefinitionKind kind, CancellationToken cancellation)
        {
            Asked++;
            IReadOnlyList<CodeLocation> answer = (WordAt(document, position), kind) switch
            {
                ("total", CodeDefinitionKind.Definition or CodeDefinitionKind.Declaration) => [Place(9, 8, 13)],
                ("total", CodeDefinitionKind.TypeDefinition) => [new CodeLocation(new CodeRange(At(0, 0))) { Document = "System/Int32.cs" }],
                ("far", CodeDefinitionKind.Definition) => [Place(199, 8, 11)],
                ("Print", CodeDefinitionKind.Definition) => [new CodeLocation(new CodeRange(At(7, 4), At(7, 9))) { Document = "src/Console.cs" }],
                ("Run", CodeDefinitionKind.Definition) => [Place(69, 9, 12)],
                ("Run", CodeDefinitionKind.Implementation) => [Place(99, 9, 12), Place(89, 9, 12)],
                _ => [],
            };
            return Task.FromResult(answer);
        }
    }

    /// <summary>Every whole word of the name at the position, as a text search answers.</summary>
    private sealed class Referring : ICodeReferenceProvider
    {
        public Task<IReadOnlyList<CodeLocation>> ReferencesAsync(CodeDocument document, CodePosition position,
            CancellationToken cancellation)
        {
            var name = WordAt(document, position);
            var found = new List<CodeLocation>();
            if (name.Length == 0) return Task.FromResult<IReadOnlyList<CodeLocation>>(found);
            for (var line = 0; line < document.LineCount; line++)
            {
                var text = document.Line(line);
                for (var at = text.IndexOf(name, StringComparison.Ordinal); at >= 0; at = text.IndexOf(name, at + 1, StringComparison.Ordinal))
                {
                    var whole = (at == 0 || !CodeDocument.IsWordChar(text[at - 1]))
                        && (at + name.Length == text.Length || !CodeDocument.IsWordChar(text[at + name.Length]));
                    if (whole) found.Add(Place(line, at, at + name.Length));
                }
            }
            return Task.FromResult<IReadOnlyList<CodeLocation>>(found);
        }
    }

    private sealed class Fixed(IReadOnlyList<CodeLocation> places) : ICodeDefinitionProvider
    {
        public Task<IReadOnlyList<CodeLocation>> DefinitionsAsync(CodeDocument document, CodePosition position,
            CodeDefinitionKind kind, CancellationToken cancellation) => Task.FromResult(places);
    }

    private sealed class Waiting : ICodeDefinitionProvider
    {
        public readonly TaskCompletionSource<IReadOnlyList<CodeLocation>> Answer = new();

        public Task<IReadOnlyList<CodeLocation>> DefinitionsAsync(CodeDocument document, CodePosition position,
            CodeDefinitionKind kind, CancellationToken cancellation) => Answer.Task;
    }

    private sealed class Throwing : ICodeDefinitionProvider
    {
        public Task<IReadOnlyList<CodeLocation>> DefinitionsAsync(CodeDocument document, CodePosition position,
            CodeDefinitionKind kind, CancellationToken cancellation) => throw new InvalidOperationException("down");
    }

    // ---- asking ---------------------------------------------------------------------------------------

    [Fact]
    public async Task TwoProvidersAnswers_AreJoinedInTheDocumentsOrder_EachPlaceOnce()
    {
        var editor = Editor(At(19, 9));
        var other = new CodeLocation(new CodeRange(At(1, 0))) { Document = "Other.cs" };
        editor.Definitions.Providers.Add(new Fixed([Place(29, 0, 3), Place(3, 0, 3)]));
        editor.Definitions.Providers.Add(new Fixed([Place(3, 0, 3), other]));

        var places = await editor.Definitions.RequestAsync(At(19, 9), CodeDefinitionKind.Definition, CancellationToken.None);

        places.Should().Equal(Place(3, 0, 3), Place(29, 0, 3), other);
    }

    [Fact]
    public async Task AProviderThatThrows_CountsAsNothing_AndIsReported()
    {
        var editor = Editor(At(19, 9));
        var failures = new List<Exception>();
        editor.Definitions.Failed += failures.Add;
        editor.Definitions.Providers.Add(new Throwing());
        editor.Definitions.Providers.Add(new Fixed([Place(9, 8, 13)]));

        var places = await editor.Definitions.RequestAsync(At(19, 9), CodeDefinitionKind.Definition, CancellationToken.None);

        places.Should().Equal(Place(9, 8, 13));
        failures.Should().ContainSingle().Which.Message.Should().Be("down");
    }

    [Fact]
    public void AnAnswerAfterTheCaretMoved_IsDropped()
    {
        var editor = Editor(At(19, 9));
        var waiting = new Waiting();
        editor.Definitions.Providers.Add(waiting);

        Press(editor, "F12").Should().BeTrue();
        editor.Selection = new CodeRange(At(5, 0));
        waiting.Answer.SetResult([Place(9, 8, 13)]);

        editor.Selection.Should().Be(new CodeRange(At(5, 0)));
        editor.GoTo.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void AnAnswerAfterAnEdit_IsDropped()
    {
        var editor = Editor(At(19, 9));
        var waiting = new Waiting();
        editor.Definitions.Providers.Add(waiting);

        Press(editor, "F12");
        editor.Apply(new CodeRange(At(250, 0)), "x");
        var after = editor.Selection;
        waiting.Answer.SetResult([Place(9, 8, 13)]);

        editor.Selection.Should().Be(after, "the answer was for the text as it stood");
        editor.GoTo.IsOpen.Should().BeFalse();
    }

    // ---- going to a definition ------------------------------------------------------------------------

    [Fact]
    public void OneDefinition_IsGoneTo_AndRecordedAsAJump()
    {
        var editor = WithProviders(At(19, 9), out _, out _);

        Press(editor, "F12").Should().BeTrue();

        editor.Selection.Should().Be(new CodeRange(At(9, 8)), "the caret stands where the declaration starts");
        editor.GoTo.IsOpen.Should().BeFalse();
        Press(editor, "-", KeyModifiers.Control).Should().BeTrue();
        editor.Selection.Should().Be(new CodeRange(At(19, 9)), "⌃- walks back to where F12 was pressed");
    }

    [Fact]
    public void ADefinitionOutOfView_IsBroughtNearTheTop()
    {
        // VS Code's NearTopIfOutsideViewport: a fifth of a forty-line view is eight lines, more than five.
        var editor = WithProviders(At(49, 9), out _, out _);

        Press(editor, "F12");

        editor.Selection.Should().Be(new CodeRange(At(199, 8)));
        editor.VerticalScroll!.Offset.Should().Be(191 * 20f, "line 200 stands eight lines below the top");
    }

    [Fact]
    public void ADefinitionInView_MovesNoView()
    {
        var editor = WithProviders(At(19, 9), out _, out _);

        Press(editor, "F12");

        editor.VerticalScroll.Should().BeNull("line 10 is in the view already");
    }

    [Fact]
    public void RevealingNearTheTop_KeepsALineAtTheEndInTheView()
    {
        // A view of five lines: the gap is five lines at the least, which would push the line out; the line stays
        // at the view's bottom instead.
        var editor = Editor(At(0, 0));
        editor.View = new Rect(0, 0, 400, 100);

        editor.RevealNearTopIfOutsideView(At(199, 0));

        editor.VerticalScroll!.Offset.Should().Be(200 * 20f - 100);
    }

    [Fact]
    public void NoDefinition_IsSaidAtTheCaret_UntilTheCaretMoves()
    {
        var editor = WithProviders(At(19, 20), out _, out _);

        Press(editor, "F12").Should().BeTrue();

        editor.GoTo.NotFound.Should().Be(new CodeGoToNotFound(CodeGoToMode.Definitions, CodeDefinitionKind.Definition,
            At(19, 20), "amount"));
        editor.Selection = new CodeRange(At(20, 0));
        editor.GoTo.NotFound.Should().BeNull();
    }

    [Fact]
    public void WhatWasSaid_ClosesOnEscape_WhichItClaims()
    {
        var editor = WithProviders(At(19, 20), out _, out _);
        Press(editor, "F12");

        Press(editor, "Escape").Should().BeTrue();

        editor.GoTo.NotFound.Should().BeNull();
        Press(editor, "Escape").Should().BeFalse("with nothing said, Escape is the host's again");
    }

    [Fact]
    public void OnTheDefinitionItself_F12FindsTheReferences()
    {
        var editor = WithProviders(At(9, 10), out _, out _);

        Press(editor, "F12");

        editor.GoTo.IsOpen.Should().BeTrue();
        editor.GoTo.Mode.Should().Be(CodeGoToMode.References);
        editor.GoTo.Entries.Select(e => e.Target.Line).Should().Equal(9, 19, 29, 39);
    }

    [Fact]
    public void SeveralImplementations_AreListed_InTheDocumentsOrder()
    {
        var editor = WithProviders(At(69, 10), out _, out _);

        Press(editor, "F12", Command).Should().BeTrue();

        editor.GoTo.Mode.Should().Be(CodeGoToMode.Definitions);
        editor.GoTo.DefinitionKind.Should().Be(CodeDefinitionKind.Implementation);
        editor.GoTo.Entries.Select(e => e.Target).Should().Equal(At(89, 9), At(99, 9));
        editor.GoTo.Entries[0].Name.Should().Be("void Run() { }", "a row is its line, its leading white space left out");
        editor.GoTo.Entries[0].Matches.Should().Equal(5, 6, 7);
        editor.GoTo.Entries[0].Container.Should().Be("90:10");
        editor.GoTo.Entries[0].Kind.Should().BeNull();
    }

    [Fact]
    public void ImplementationsOnTheOnlyOne_HaveNoAlternative()
    {
        var editor = Editor(At(89, 10));
        editor.Definitions.Providers.Add(new Fixed([Place(89, 9, 12)]));
        editor.References.Providers.Add(new Referring());

        editor.Execute(CodeCommand.GoToImplementation).Should().BeTrue();

        editor.GoTo.IsOpen.Should().BeFalse("VS Code's alternativeImplementationCommand is empty");
        editor.Selection.Should().Be(new CodeRange(At(89, 9)));
    }

    [Fact]
    public void ATypeDefinitionInAnotherDocument_IsHandedToTheApp()
    {
        var editor = WithProviders(At(19, 9), out _, out _);
        var opened = new List<CodeLocation>();
        editor.OpenRequested += opened.Add;
        var places = editor.JumpList.Places.Count;

        editor.Execute(CodeCommand.GoToTypeDefinition).Should().BeTrue();

        opened.Should().ContainSingle().Which.Document.Should().Be("System/Int32.cs");
        editor.Selection.Should().Be(new CodeRange(At(19, 9)), "the caret stays: the other document is the app's");
        editor.JumpList.Places.Should().HaveCount(places, "a place in another document is no jump of this editor");
    }

    [Fact]
    public void ADefinitionInAnotherFile_RaisesOpenRequestedOnce()
    {
        var editor = WithProviders(At(59, 9), out _, out _);
        var opened = new List<CodeLocation>();
        editor.OpenRequested += opened.Add;

        Press(editor, "F12");

        opened.Should().Equal(new CodeLocation(new CodeRange(At(7, 4), At(7, 9))) { Document = "src/Console.cs" });
        editor.Selection.Should().Be(new CodeRange(At(59, 9)));
    }

    [Fact]
    public void WithNoProvider_F12ClaimsNothing()
    {
        var editor = Editor(At(19, 9));

        Press(editor, "F12").Should().BeFalse();
        Press(editor, "F12", Shift).Should().BeFalse();
        editor.CanExecute(CodeCommand.GoToDefinition).Should().BeFalse();
    }

    // ---- references and the list ------------------------------------------------------------------------

    [Fact]
    public void SeveralReferences_AreListed_TheNearestSelectedAndPreviewed()
    {
        var editor = WithProviders(At(29, 9), out _, out _);

        Press(editor, "F12", Shift).Should().BeTrue();

        editor.GoTo.IsOpen.Should().BeTrue();
        editor.GoTo.Entries.Select(e => e.Target.Line).Should().Equal(9, 19, 29, 39);
        editor.GoTo.Selected.Should().Be(2, "the third use is the one the caret stands on");
        editor.GoTo.Preview.Should().Be(new CodeRange(At(29, 8), At(29, 13)));
        editor.Selection.Should().Be(new CodeRange(At(29, 9)), "the caret has not moved");
        editor.GoTo.Places.Should().HaveCount(4);
    }

    [Fact]
    public void TheTextFiltersTheRows_KeepingTheirOrder()
    {
        var editor = WithProviders(At(29, 9), out _, out _);
        Press(editor, "F12", Shift);

        editor.GoTo.Query = "+=";

        editor.GoTo.Entries.Select(e => e.Target.Line).Should().Equal(19, 29);
        editor.GoTo.Selected.Should().Be(0);
        editor.GoTo.Mode.Should().Be(CodeGoToMode.References, "the text only filters a list of places");
    }

    [Fact]
    public void ARowGoneTo_IsAJump_FromWhereTheCaretWas()
    {
        var editor = WithProviders(At(29, 9), out _, out _);
        Press(editor, "F12", Shift);

        Press(editor, "ArrowDown");
        Press(editor, "Enter");

        editor.GoTo.IsOpen.Should().BeFalse();
        editor.Selection.Should().Be(new CodeRange(At(39, 15)));
        Press(editor, "-", KeyModifiers.Control);
        editor.Selection.Should().Be(new CodeRange(At(29, 9)));
    }

    [Fact]
    public void EscapeOnTheList_GivesTheViewBack()
    {
        var editor = WithProviders(At(29, 9), out _, out _);
        Press(editor, "F12", Shift);
        Press(editor, "ArrowDown");

        Press(editor, "Escape");

        editor.GoTo.IsOpen.Should().BeFalse();
        editor.Selection.Should().Be(new CodeRange(At(29, 9)));
        editor.VerticalScroll!.Offset.Should().Be(0, "the view as it was when the list opened");
    }

    [Fact]
    public void OneReference_IsGoneTo()
    {
        var editor = Editor(At(0, 2));
        editor.References.Providers.Add(new Referring());

        Press(editor, "F12", Shift);

        editor.GoTo.IsOpen.Should().BeFalse();
        editor.Selection.Should().Be(new CodeRange(At(0, 0)), "class is written once: the place is gone to");
    }

    [Fact]
    public void AnEditWhileTheListShows_CarriesTheRows_AndKeepsTheOneSelected()
    {
        var editor = WithProviders(At(29, 9), out _, out _);
        Press(editor, "F12", Shift);
        Press(editor, "ArrowDown");

        editor.Apply(new CodeRange(At(0, 0)), "// inserted\n");

        editor.GoTo.IsOpen.Should().BeTrue();
        editor.GoTo.Selected.Should().Be(3, "the row walked to stays selected");
        editor.GoTo.Entries.Select(e => e.Target.Line).Should().Equal(10, 20, 30, 40);
        editor.GoTo.Preview.Should().Be(new CodeRange(At(40, 15), At(40, 20)));
    }

    // ---- the walk -----------------------------------------------------------------------------------------

    [Fact]
    public void F4WhileTheListShows_SelectsTheNextRow_Wrapping()
    {
        var editor = WithProviders(At(29, 9), out _, out _);
        Press(editor, "F12", Shift);

        Press(editor, "F4").Should().BeTrue();
        editor.GoTo.Selected.Should().Be(3);
        Press(editor, "F4");
        editor.GoTo.Selected.Should().Be(0);
        Press(editor, "F4", Shift);
        editor.GoTo.Selected.Should().Be(3);
        editor.Selection.Should().Be(new CodeRange(At(29, 9)), "selecting a row moves no caret");
    }

    [Fact]
    public void F4AfterTheListClosed_WalksThePlaces_CarriedThroughAnEdit()
    {
        var editor = WithProviders(At(29, 9), out _, out _);
        Press(editor, "F12", Shift);
        editor.GoTo.Select(1);
        Press(editor, "Enter");
        editor.Selection.Should().Be(new CodeRange(At(19, 8)));

        editor.Apply(new CodeRange(At(0, 0)), "// inserted\n");
        Press(editor, "F4").Should().BeTrue();
        Press(editor, "F4").Should().BeTrue();

        editor.Selection.Should().Be(new CodeRange(At(40, 15)), "the use that was on line 39 is on line 40 now");
        Press(editor, "F4");
        editor.Selection.Should().Be(new CodeRange(At(10, 8)), "the walk wraps to the declaration");
        Press(editor, "F4", Shift);
        editor.Selection.Should().Be(new CodeRange(At(40, 15)));
    }

    [Fact]
    public void F4WithNothingHeld_IsNotClaimed()
    {
        var editor = WithProviders(At(29, 9), out _, out _);

        Press(editor, "F4").Should().BeFalse();
        Press(editor, "F4", Shift).Should().BeFalse();
    }

    [Fact]
    public void ANewDocument_DropsThePlaces()
    {
        var editor = WithProviders(At(29, 9), out _, out _);
        Press(editor, "F12", Shift);
        Press(editor, "Escape");

        editor.Document = CodeDocument.FromText("other");

        editor.GoTo.Places.Should().BeEmpty();
        Press(editor, "F4").Should().BeFalse();
    }

    // ---- the link -----------------------------------------------------------------------------------------

    /// <summary>A point on the left of the character at <paramref name="at"/>, on a grid of 8 by 20: the character
    /// the pointer is on, and the caret a press there places before it.</summary>
    private static Point On(CodePosition at) => new(at.Column * 8 + 2, at.Line * 20 + 10);

    [Fact]
    public void CommandOverAName_ShowsTheLink_AndTheHand()
    {
        var editor = WithProviders(At(0, 0), out _, out _);

        editor.HandlePointer(PointerPhase.Hover, On(At(19, 10)), Command, 0);

        editor.Definitions.Link.Should().Be(new CodeRange(At(19, 8), At(19, 13)));
        editor.CursorAt(On(At(19, 12))).Should().Be(PointerCursor.Pointer);
        editor.CursorAt(On(At(19, 14))).Should().Be(PointerCursor.Text, "past the word the pointer is the beam");
    }

    [Fact]
    public void TheLink_IsAskedOncePerWord()
    {
        var editor = WithProviders(At(0, 0), out var definitions, out _);

        editor.HandlePointer(PointerPhase.Hover, On(At(19, 8)), Command, 0);
        editor.HandlePointer(PointerPhase.Hover, On(At(19, 11)), Command, 0);
        editor.HandlePointer(PointerPhase.Hover, On(At(19, 12)), Command, 0);

        definitions.Asked.Should().Be(1);
    }

    [Fact]
    public void ANameWithNoDefinition_ShowsNoLink()
    {
        var editor = WithProviders(At(0, 0), out _, out _);

        editor.HandlePointer(PointerPhase.Hover, On(At(19, 18)), Command, 0);

        editor.Definitions.Link.Should().BeNull();
        editor.CursorAt(On(At(19, 18))).Should().Be(PointerCursor.Text);
    }

    [Theory]
    [InlineData("released")]
    [InlineData("key")]
    [InlineData("left")]
    [InlineData("edit")]
    [InlineData("scroll")]
    [InlineData("shift")]
    public void TheLinkGoes(string how)
    {
        var editor = WithProviders(At(0, 0), out _, out _);
        editor.HandlePointer(PointerPhase.Hover, On(At(19, 10)), Command, 0);
        editor.Definitions.Link.Should().NotBeNull();

        switch (how)
        {
            case "released": editor.HandlePointer(PointerPhase.Hover, On(At(19, 10)), KeyModifiers.None, 0); break;
            case "key": Press(editor, "a", Command); break;
            case "left": editor.HandlePointer(PointerPhase.Exit, Point.Zero, Command, 0); break;
            case "edit": editor.Apply(new CodeRange(At(250, 0)), "x"); break;
            case "scroll": editor.View = new Rect(0, 40, 400, 800); break;
            case "shift": editor.HandlePointer(PointerPhase.Hover, On(At(19, 10)), Command | Shift, 0); break;
        }

        editor.Definitions.Link.Should().BeNull();
    }

    [Fact]
    public void AModifierKeyAlone_LeavesTheLink()
    {
        var editor = WithProviders(At(0, 0), out _, out _);
        editor.HandlePointer(PointerPhase.Hover, On(At(19, 10)), Command, 0);

        Press(editor, "Meta", Command).Should().BeFalse();

        editor.Definitions.Link.Should().NotBeNull();
    }

    [Fact]
    public void ACommandPressAndItsRelease_GoToTheDefinition()
    {
        var editor = WithProviders(At(0, 0), out _, out _);
        editor.HandlePointer(PointerPhase.Hover, On(At(19, 10)), Command, 0);

        editor.HandlePointer(PointerPhase.Down, On(At(19, 10)), Command, 1);
        editor.Selection.Should().Be(new CodeRange(At(19, 10)), "the press places the caret as any does");
        editor.HandlePointer(PointerPhase.Up, On(At(19, 10)), Command, 1);

        editor.Selection.Should().Be(new CodeRange(At(9, 8)));
        Press(editor, "-", KeyModifiers.Control);
        editor.Selection.Should().Be(new CodeRange(At(19, 10)), "the click is a jump from where it was pressed");
    }

    [Fact]
    public void ACommandClickWithNoDefinition_SaysNothing()
    {
        var editor = WithProviders(At(0, 0), out _, out _);

        editor.HandlePointer(PointerPhase.Down, On(At(19, 18)), Command, 1);
        editor.HandlePointer(PointerPhase.Up, On(At(19, 18)), Command, 1);

        editor.GoTo.NotFound.Should().BeNull("VS Code mutes its message for the click");
        editor.Selection.Should().Be(new CodeRange(At(19, 18)));
    }

    [Fact]
    public void ADragWithCommand_SelectsAndGoesNowhere()
    {
        var editor = WithProviders(At(0, 0), out _, out _);

        editor.HandlePointer(PointerPhase.Down, On(At(19, 9)), Command, 1);
        editor.HandlePointer(PointerPhase.Move, On(At(21, 4)), Command, 1);
        editor.HandlePointer(PointerPhase.Up, On(At(21, 4)), Command, 1);

        editor.Selection.Should().Be(new CodeRange(At(19, 9), At(21, 4)));
    }

    [Fact]
    public void AReleaseOnAnotherLine_GoesNowhere()
    {
        var editor = WithProviders(At(0, 0), out _, out _);

        editor.HandlePointer(PointerPhase.Down, On(At(19, 9)), Command, 1);
        editor.HandlePointer(PointerPhase.Up, On(At(20, 9)), Command, 1);

        editor.Selection.Should().Be(new CodeRange(At(19, 9)));
    }

    [Fact]
    public void APlainPress_IsNoLink()
    {
        var editor = WithProviders(At(0, 0), out _, out _);

        editor.HandlePointer(PointerPhase.Down, On(At(19, 9)), KeyModifiers.None, 1);
        editor.HandlePointer(PointerPhase.Up, On(At(19, 9)), KeyModifiers.None, 1);

        editor.Selection.Should().Be(new CodeRange(At(19, 9)));
    }

    // ---- the keys -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(KeyboardConvention.Apple)]
    [InlineData(KeyboardConvention.Standard)]
    public void TheKeys_AreVSCodes_InBothConventions(KeyboardConvention convention)
    {
        // Ctrl is Command on Windows and Linux, so ⌘F12 is Ctrl+F12 there.
        var editor = WithProviders(At(69, 10), out _, out _);
        Press(editor, "F12", Command, convention).Should().BeTrue();
        editor.GoTo.DefinitionKind.Should().Be(CodeDefinitionKind.Implementation);
        Press(editor, "Escape", KeyModifiers.None, convention);

        editor.Selection = new CodeRange(At(19, 9));
        Press(editor, "F12", KeyModifiers.None, convention).Should().BeTrue();
        editor.Selection.Should().Be(new CodeRange(At(9, 8)));

        editor.Selection = new CodeRange(At(29, 9));
        Press(editor, "F12", Shift, convention).Should().BeTrue();
        editor.GoTo.Mode.Should().Be(CodeGoToMode.References);
    }
}
