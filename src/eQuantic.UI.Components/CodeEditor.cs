using eQuantic.UI.Code;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// CODE, written. The same surface <see cref="CodeBlock"/> draws, plus a caret, a selection and a
/// keyboard — which is the whole difference between reading code and editing it.
/// <para>
/// The controller is STATE: it holds the document, the selection and the undo history, and it has
/// to outlive the rebuild that every keystroke causes. That is why this is a stateful component and
/// why the surface node carries the controller itself rather than a copy of what it currently says.
/// </para>
/// <para>
/// An app that wants to own the text passes <c>OnChanged</c> and gets the document back after every
/// edit; an app that just wants an editor can ignore it and read <see cref="Editor"/> when it needs
/// to. Both are ordinary — an editor is not a form field, and forcing every keystroke through the
/// app would make undo the app's problem too.
/// </para>
/// </summary>
public sealed class CodeEditor : StatefulComponent
{
    private CodeEditorController? _editor;
    private bool _findOpen;
    private string _findText = "";
    private float _offset;
    private float _viewport;
    private float _viewportWidth;
    // How far the code has scrolled sideways: where the completion list's and the card's right edges
    // have to stop.
    private float _scrollX;
    // The completion list's first row in view, the most columns it has needed since it opened, and
    // the list of entries those columns were last measured over.
    private int _listTop;
    private int _listColumns;
    private IReadOnlyList<CodeCompletionMatch>? _measured;
    // The go-to field's first row in view (#1083).
    private int _goToTop;

    public CodeEditor(string code = "", string? language = null)
    {
        InitialCode = code;
        LanguageName = language;
    }

    /// <summary>The text the editor OPENS with. Later changes to it are ignored — the document is
    /// the editor's own from the first keystroke, and reopening it is a new editor.</summary>
    public string InitialCode { get; init; }

    /// <summary>Which language colours it — a name or an extension ("csharp", ".ts", "python").</summary>
    public string? LanguageName { get; set; }

    /// <summary>Raised after every edit, with the whole document. Null = the app is not tracking it.</summary>
    public Action<string>? OnChanged { get; set; }

    /// <summary>Raised when the caret or the selection moves — a status bar's cue.</summary>
    public Action<CodeRange>? OnSelectionChanged { get; set; }

    public bool ShowLineNumbers { get; set; } = true;
    public int FirstLineNumber { get; set; } = 1;
    /// <summary>
    /// How tall the editor is. Hug, the default, is as tall as the code, up to
    /// <see cref="MaxHeight"/> when one is set. Fill takes the height its place gives it, which is
    /// how an IDE's pane uses it, and a fixed height is that many dp. Anything but an uncapped Hug
    /// scrolls the code inside it and builds only the lines in view.
    /// </summary>
    public SizeValue Height { get; set; } = SizeValue.Hug;

    /// <summary>The most a Hug editor grows to before it scrolls, in dp. 0 = no cap.</summary>
    public float MaxHeight { get; set; }
    public SizeVariant Size { get; set; } = SizeVariant.Small;
    public bool Inverse { get; set; }
    public bool ReadOnly { get; set; }
    public bool Autofocus { get; set; }
    public string? Caption { get; set; }

    /// <summary>Lines to point at — an error's line, a breakpoint, a diff hunk.</summary>
    public IReadOnlyList<CodeGutterMarker> GutterMarkers { get; set; } = [];

    /// <summary>Ranges to mark — search matches, a squiggle. The editor ADDS to these: the bracket
    /// under the caret, and the matches of whatever is being searched for.</summary>
    public IReadOnlyList<CodeDecoration> Decorations { get; set; } = [];

    /// <summary>Outlines the bracket the caret is against and the one it pairs with. On by default:
    /// it is how you find the end of a block without counting.</summary>
    public bool MatchBrackets { get; set; } = true;

    /// <summary>What the APP searches for: an IDE with a find UI of its own sets it. Every match is
    /// washed and the one the caret is on outlined, while the editor's own find bar is closed or its
    /// field empty; null or empty marks nothing.</summary>
    public string? Search { get; set; }

    public bool SearchMatchCase { get; set; }

    /// <summary>A press on a gutter row — where an IDE toggles a breakpoint.</summary>
    public Action<int>? OnGutterPressed { get; set; }

    /// <summary>
    /// What the editor completes from as a word is typed: the providers its completion asks, in this
    /// order. Null, the default, is the language's own words and the document's
    /// (<see cref="CodeKeywordCompletionProvider"/>, <see cref="CodeWordCompletionProvider"/>). An
    /// empty list completes nothing, and an IDE passes its language service, with the built-ins
    /// beside it or without them. A read-only editor completes nothing, whatever it is given.
    /// <para>
    /// The editor puts these in its completion when it opens, and again only when the list holds
    /// other providers, so a parent rebuilding with the same ones changes nothing; a list already
    /// showing goes on with the providers it was asked of. It takes out only what it put in: a
    /// provider an app added to <c>Editor.Completion.Providers</c> itself stays beside them.
    /// </para>
    /// </summary>
    public IReadOnlyList<ICodeCompletionProvider>? Completions { get; set; }

    /// <summary>
    /// The problems the APP knows about: a build's output, an analyzer's run. The editor draws each under
    /// its range in its severity's colour, marks its line in the gutter, carries it through every edit
    /// until the app reports again, and explains it in the card the pointer or F8 brings up. A list of
    /// other problems than the last one replaces it; the same problems again change nothing, so a parent
    /// rebuilding with the list it keeps does not put a problem back where it was before an edit.
    /// </summary>
    public IReadOnlyList<CodeDiagnostic> Diagnostics { get; set; } = [];

    /// <summary>
    /// What the editor asks for problems as its text changes (<see cref="ICodeDiagnosticProvider"/>): a
    /// compiler, an analyzer, a server action that builds the buffer. They are asked once when they are
    /// handed and then once typing pauses, one request out at a time. The editor puts these in its
    /// diagnostics and takes out only what it put in, as it does with <see cref="Completions"/>.
    /// <para>
    /// A provider is known by reference, so keep each in a field, as a parent keeps a controller in
    /// Flutter: one made in a build is a new provider every build, and is asked again at once, the
    /// pause typing earns skipped.
    /// </para>
    /// </summary>
    public IReadOnlyList<ICodeDiagnosticProvider> DiagnosticProviders { get; set; } = [];

    /// <summary>
    /// What explains the code under the pointer or the caret (<see cref="ICodeHoverProvider"/>), in the
    /// card beside the problems reported there. Put in and taken out as <see cref="DiagnosticProviders"/>
    /// are.
    /// </summary>
    public IReadOnlyList<ICodeHoverProvider> HoverProviders { get; set; } = [];

    /// <summary>
    /// What explains the call being written (<see cref="ICodeSignatureProvider"/>): its signatures and the
    /// argument the caret is on, in the panel over the caret's line. Put in and taken out as
    /// <see cref="DiagnosticProviders"/> are, and known by reference as they are.
    /// </summary>
    public IReadOnlyList<ICodeSignatureProvider> SignatureProviders { get; set; } = [];

    /// <summary>
    /// What declares the document's symbols (<see cref="ICodeSymbolProvider"/>), which ⇧⌘O lists and goes to
    /// (#1083). Put in and taken out as <see cref="DiagnosticProviders"/> are, and known by reference as they
    /// are.
    /// </summary>
    public IReadOnlyList<ICodeSymbolProvider> SymbolProviders { get; set; } = [];

    /// <summary>
    /// What says where a name is defined, declared, typed or implemented (<see cref="ICodeDefinitionProvider"/>),
    /// which F12 and ⌘F12 go to and ⌘ links over a name (#1084). Put in and taken out as
    /// <see cref="DiagnosticProviders"/> are, and known by reference as they are.
    /// </summary>
    public IReadOnlyList<ICodeDefinitionProvider> DefinitionProviders { get; set; } = [];

    /// <summary>What says where a name is used (<see cref="ICodeReferenceProvider"/>), which ⇧F12 lists and F4
    /// walks (#1084). Put in and taken out as <see cref="DiagnosticProviders"/> are.</summary>
    public IReadOnlyList<ICodeReferenceProvider> ReferenceProviders { get; set; } = [];

    /// <summary>
    /// What says where the code folds (<see cref="ICodeFoldProvider"/>), whose regions the gutter's chevrons fold
    /// (#1119). None, the default, folds by indentation. Put in and taken out as <see cref="DiagnosticProviders"/>
    /// are, and the regions asked again when they change.
    /// </summary>
    public IReadOnlyList<ICodeFoldProvider> FoldProviders { get; set; } = [];

    /// <summary>
    /// A place in another document the person went to (#1084): a definition or a reference a provider answered
    /// there. The editor holds one document, so the app opens that one and puts the caret at the location's start
    /// (<see cref="CodeEditorController.OpenRequested"/>).
    /// </summary>
    public Action<CodeLocation>? OnOpenRequested { get; set; }

    /// <summary>
    /// An app's own key bindings over the editor's defaults (<see cref="CodeKeymap.Defaults"/>): a chord
    /// bound here is the app's alone, and one bound to <see cref="CodeCommand.None"/> is given back to the
    /// host. Handed to <see cref="CodeEditorController.KeyBindings"/>, and taken out again when a later
    /// build hands others.
    /// </summary>
    public IReadOnlyList<CodeKeyBinding> KeyBindings { get; set; } = [];

    /// <summary>The live editor: the document, the selection, and every command. An IDE reaches for
    /// this to run its own — a formatter, a refactor, a language server's edit — and they undo like
    /// anything the person typed, because they go through the same primitive.</summary>
    public CodeEditorController Editor => _editor ??= Create();

    private CodeEditorController Create()
    {
        var editor = new CodeEditorController(InitialCode, CodeLanguages.For(LanguageName))
        {
            ReadOnly = ReadOnly,
        };
        _toldDocument = editor.Document;
        _toldSelection = editor.Selection;
        // The list changes outside any input the surface handles (an answer arriving, an entry
        // resolved), and has to be drawn again when it does.
        editor.Completion.Changed += () => SetState(() => { });
        // So do the problems (an answer, a report) and the card (the pointer resting, an answer).
        editor.Diagnostics.Changed += () => SetState(() => { });
        editor.Hover.Changed += () => SetState(() => { });
        editor.SignatureHelp.Changed += () => SetState(() => { });
        // And a snippet's session, whose end on Escape moves neither the text nor the carets (#1013).
        editor.Snippet.Changed += () => SetState(() => { });
        // And the go-to field, and a scroll the editor asks of its view, which an app may ask for with no key
        // (#1083).
        // It moves the caret when an answer arrives after the key that asked (a definition gone to, #1084), so the
        // app hears where the caret went as it hears a key's move.
        editor.GoTo.Changed += () => SetState(() => TellSelection(editor));
        editor.ScrollRequested += () => SetState(() => { });
        // And the link ⌘ shows over a name, which an answer brings and a key, the pointer or a scroll takes away,
        // and a place in another document, which is the app's to open (#1084).
        editor.Definitions.Changed += () => SetState(() => { });
        // And the regions, which arrive after the text rests and fold and unfold by key (#1119).
        editor.Folding.Changed += () => SetState(() => { });
        // A method, not a lambda: the twin's controller is untyped where an object initializer built it (#1106).
        editor.OpenRequested += Opened;
        return editor;
    }

    /// <summary>A place in another document the person went to, handed on to the app (#1084).</summary>
    private void Opened(CodeLocation place) => OnOpenRequested?.Invoke(place);

    /// <summary>The providers last handed to the completion (a copy, since a parent may change the list
    /// it keeps in place, and null for the built-ins), whether any have been, and the providers the
    /// editor put in for them: the only ones it ever takes out again.</summary>
    private IReadOnlyList<ICodeCompletionProvider>? _handed;
    private bool _handedAny;
    private List<ICodeCompletionProvider> _put = [];

    /// <summary>What was last reported, and the problem and hover providers the editor put in.</summary>
    private IReadOnlyList<CodeDiagnostic>? _reported;
    private List<ICodeDiagnosticProvider> _diagnosers = [];
    private List<ICodeHoverProvider> _explainers = [];
    private List<ICodeSignatureProvider> _signers = [];
    private List<ICodeSymbolProvider> _declarers = [];
    private List<ICodeDefinitionProvider> _definers = [];
    private List<ICodeReferenceProvider> _referrers = [];
    private List<ICodeFoldProvider> _folders = [];

    /// <summary>
    /// Reports <see cref="Diagnostics"/> when they are other problems than the last ones, and hands the
    /// providers when they are other providers, asking the problem providers at once and closing the card
    /// and the panel the providers before had filled.
    /// </summary>
    private void HandIntelligence(CodeEditorController editor)
    {
        if (_reported is null || !SameProblems(Diagnostics, _reported))
        {
            _reported = [.. Diagnostics];
            editor.Diagnostics.Report(_reported);
        }
        if (!SameProviders(DiagnosticProviders, _diagnosers))
        {
            foreach (var provider in _diagnosers)
            {
                var at = IndexOfInstance(editor.Diagnostics.Providers, provider);
                if (at >= 0) editor.Diagnostics.Providers.RemoveAt(at);
            }
            _diagnosers = [.. DiagnosticProviders];
            foreach (var provider in _diagnosers) editor.Diagnostics.Providers.Add(provider);
            // Asked at once, and what the providers taken out had found goes with them.
            editor.Diagnostics.Check();
        }
        if (!SameProviders(HoverProviders, _explainers))
        {
            foreach (var provider in _explainers)
            {
                var at = IndexOfInstance(editor.Hover.Providers, provider);
                if (at >= 0) editor.Hover.Providers.RemoveAt(at);
            }
            _explainers = [.. HoverProviders];
            foreach (var provider in _explainers) editor.Hover.Providers.Add(provider);
            // What the card shows, and what it waits for, came from the providers before.
            editor.Hover.Dismiss();
        }
        if (!SameProviders(SignatureProviders, _signers))
        {
            foreach (var provider in _signers)
            {
                var at = IndexOfInstance(editor.SignatureHelp.Providers, provider);
                if (at >= 0) editor.SignatureHelp.Providers.RemoveAt(at);
            }
            _signers = [.. SignatureProviders];
            foreach (var provider in _signers) editor.SignatureHelp.Providers.Add(provider);
            // What the panel shows, and the requests still out for it, came from the providers before.
            editor.SignatureHelp.Dismiss();
        }
        if (!SameProviders(SymbolProviders, _declarers))
        {
            foreach (var provider in _declarers)
            {
                var at = IndexOfInstance(editor.Symbols.Providers, provider);
                if (at >= 0) editor.Symbols.Providers.RemoveAt(at);
            }
            _declarers = [.. SymbolProviders];
            foreach (var provider in _declarers) editor.Symbols.Providers.Add(provider);
        }
        if (!SameProviders(DefinitionProviders, _definers))
        {
            foreach (var provider in _definers)
            {
                var at = IndexOfInstance(editor.Definitions.Providers, provider);
                if (at >= 0) editor.Definitions.Providers.RemoveAt(at);
            }
            _definers = [.. DefinitionProviders];
            foreach (var provider in _definers) editor.Definitions.Providers.Add(provider);
            // The link stood on what the providers before answered.
            editor.Definitions.ClearLink();
        }
        if (!SameProviders(ReferenceProviders, _referrers))
        {
            foreach (var provider in _referrers)
            {
                var at = IndexOfInstance(editor.References.Providers, provider);
                if (at >= 0) editor.References.Providers.RemoveAt(at);
            }
            _referrers = [.. ReferenceProviders];
            foreach (var provider in _referrers) editor.References.Providers.Add(provider);
        }
        if (!SameProviders(FoldProviders, _folders))
        {
            foreach (var provider in _folders)
            {
                var at = IndexOfInstance(editor.Folding.Providers, provider);
                if (at >= 0) editor.Folding.Providers.RemoveAt(at);
            }
            _folders = [.. FoldProviders];
            foreach (var provider in _folders) editor.Folding.Providers.Add(provider);
            // The regions came from the providers before.
            editor.Folding.Check();
        }
    }

    private List<CodeKeyBinding> _bindings = [];

    /// <summary>
    /// Hands <see cref="KeyBindings"/> to the controller when they are other bindings than the last ones:
    /// the block handed before comes out and the new one goes in at the end, and what the app put on the
    /// controller itself stays, in its order. The block comes out whole, where it stands last, since it went
    /// in at the end and whatever the app puts before it leaves it there; never by value, binding by binding,
    /// where a binding of the controller's equal to one of the component's went in its place, and the
    /// controller's own order, and with it what a key ran, changed (Copilot's second round on #1027).
    /// </summary>
    private void HandKeyBindings(CodeEditorController editor)
    {
        if (SameBindings(KeyBindings, _bindings)) return;
        var list = editor.KeyBindings;
        var at = LastBlockAt(list, _bindings);
        if (at >= 0)
        {
            for (var i = 0; i < _bindings.Count; i++) list.RemoveAt(at);
        }
        else
        {
            // The app split the block up: each binding comes out where it stands last, nearest the end the
            // block was put at.
            for (var i = _bindings.Count - 1; i >= 0; i--)
            {
                var last = LastIndexOf(list, _bindings[i]);
                if (last >= 0) list.RemoveAt(last);
            }
        }
        _bindings = [.. KeyBindings];
        foreach (var binding in _bindings) list.Add(binding);
    }

    /// <summary>The last place <paramref name="block"/> stands whole and in order in <paramref name="list"/>,
    /// or -1.</summary>
    private static int LastBlockAt(IList<CodeKeyBinding> list, List<CodeKeyBinding> block)
    {
        if (block.Count == 0) return -1;
        for (var at = list.Count - block.Count; at >= 0; at--)
        {
            if (Holds(list, block, at)) return at;
        }
        return -1;
    }

    private static bool Holds(IList<CodeKeyBinding> list, List<CodeKeyBinding> block, int at)
    {
        if (at < 0 || at + block.Count > list.Count) return false;
        for (var i = 0; i < block.Count; i++)
        {
            if (!list[at + i].Equals(block[i])) return false;
        }
        return true;
    }

    private static int LastIndexOf(IList<CodeKeyBinding> list, CodeKeyBinding binding)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].Equals(binding)) return i;
        }
        return -1;
    }

    private static bool SameBindings(IReadOnlyList<CodeKeyBinding> one, IReadOnlyList<CodeKeyBinding> other)
    {
        if (one.Count != other.Count) return false;
        for (var i = 0; i < one.Count; i++)
            if (!one[i].Equals(other[i])) return false;
        return true;
    }

    /// <summary>Whether two lists hold the same problems in the same order.</summary>
    private static bool SameProblems(IReadOnlyList<CodeDiagnostic> one, IReadOnlyList<CodeDiagnostic> other)
    {
        if (one.Count != other.Count) return false;
        for (var i = 0; i < one.Count; i++)
            if (!one[i].Equals(other[i])) return false;
        return true;
    }

    /// <summary>Hands <see cref="Completions"/> to the completion, the built-ins for null, unless
    /// what it holds already came from a list of the same providers. A provider the app added to the
    /// completion itself stays: the controller's own documentation tells an IDE to add its language
    /// service there, and clearing the list wiped it on the first build.</summary>
    private void HandProviders(CodeCompletion completion)
    {
        if (_handedAny && SameProviders(Completions, _handed)) return;
        _handedAny = true;
        var providers = completion.Providers;
        foreach (var provider in _put)
        {
            var at = IndexOfInstance(providers, provider);
            if (at >= 0) providers.RemoveAt(at);
        }
        _put = Completions is null
            ? [new CodeKeywordCompletionProvider(), new CodeWordCompletionProvider()]
            : [.. Completions];
        // The copy, never the list itself: compared with itself, a list the parent changed in place
        // would always hold the same providers, and the ones it gained would never be handed.
        _handed = Completions is null ? null : _put;
        foreach (var provider in _put) providers.Add(provider);
    }

    /// <summary>Where <paramref name="provider"/> itself stands in <paramref name="providers"/>, or -1:
    /// found by reference, since <c>Remove</c> takes the first one that EQUALS it, which for a provider
    /// that is a record can be one the app added.</summary>
    private static int IndexOfInstance(IEnumerable<object> providers, object provider)
    {
        var at = 0;
        foreach (var held in providers)
        {
            if (ReferenceEquals(held, provider)) return at;
            at++;
        }
        return -1;
    }

    /// <summary>Whether two lists hold the same providers in the same order: a parent's build makes
    /// a new list every time, around the providers it keeps.</summary>
    private static bool SameProviders(IReadOnlyList<object>? one, IReadOnlyList<object>? other)
    {
        if (one is null || other is null) return one is null && other is null;
        if (one.Count != other.Count) return false;
        for (var i = 0; i < one.Count; i++)
        {
            if (!ReferenceEquals(one[i], other[i])) return false;
        }
        return true;
    }

    /// <summary>A press on a row of the list: selects that entry and accepts it, and the app hears
    /// of the edit as it does of a key's.</summary>
    private void Pick(CodeEditorController editor, int index) => SetState(() =>
    {
        if (editor.Completion.Select(index)) editor.Completion.Accept();
        Notify(editor);
    });

    /// <summary>The document and the selection the app was last told about (see <see cref="Notify"/>).</summary>
    private CodeDocument? _toldDocument;
    private CodeRange _toldSelection;

    /// <summary>
    /// Tells the app what CHANGED since it was last told: the document after an edit, the selection
    /// after a move. Both were raised together for anything the surface did, so an arrow reached the
    /// app as an edit (an app re-reading the document on every change did it for every arrow), and
    /// the find bar's steps moved the selection without a word.
    /// </summary>
    private void Notify(CodeEditorController editor)
    {
        if (editor.Document != _toldDocument)
        {
            _toldDocument = editor.Document;
            OnChanged?.Invoke(editor.Document.Text);
        }
        TellSelection(editor);
    }

    /// <summary>The header lines of the folded regions among <paramref name="folds"/>.</summary>
    private static List<int> FoldedLines(CodeEditorController editor, IReadOnlyList<CodeFold> folds)
    {
        var folded = new List<int>();
        foreach (var fold in folds)
            if (editor.Folding.IsFolded(fold.StartLine)) folded.Add(fold.StartLine);
        return folded;
    }

    /// <summary>Tells the app where the caret is when it is not where it was last told.</summary>
    private void TellSelection(CodeEditorController editor)
    {
        if (editor.Selection == _toldSelection) return;
        _toldSelection = editor.Selection;
        OnSelectionChanged?.Invoke(editor.Selection);
    }

    /// <summary>
    /// The gutter's markers: the app's own, and on every other line in view that holds a problem, an
    /// error's or a warning's marker, the worst problem of the line deciding. An information or a hint
    /// marks no line: the gutter is where the eye looks for what is wrong.
    /// </summary>
    private IReadOnlyList<CodeGutterMarker> Gutter(CodeEditorController editor, int first, int last)
    {
        var problems = editor.Diagnostics.Items;
        if (problems.Count == 0) return GutterMarkers;
        // The worst severity of each line, as the number it is: a severity is a word in the browser,
        // where comparing two would compare their letters.
        var worst = new Dictionary<int, int>();
        foreach (var problem in problems)
        {
            var severity = (int)problem.Severity;
            if (severity < (int)CodeDiagnosticSeverity.Warning) continue;
            var line = problem.Range.Start.Line;
            if (line < first || line > last) continue;
            if (!worst.TryGetValue(line, out var known) || severity > known) worst[line] = severity;
        }
        if (worst.Count == 0) return GutterMarkers;
        var markers = new List<CodeGutterMarker>(GutterMarkers);
        foreach (var entry in worst)
        {
            if (markers.Any(marker => marker.Line == entry.Key)) continue;
            markers.Add(new CodeGutterMarker(entry.Key,
                entry.Value == (int)CodeDiagnosticSeverity.Error ? CodeGutterKind.Error : CodeGutterKind.Warning));
        }
        return markers;
    }

    /// <summary>
    /// Everything the block should mark: what the app handed in, the search matches on the lines from
    /// <paramref name="first"/> to <paramref name="last"/>, the bracket pair under the caret, the problems
    /// on those lines, and a snippet's placeholders, in the colours of <paramref name="theme"/>. Computed per frame from the
    /// model rather than stored, because the first three are functions of where the caret is — and a
    /// stored copy would be one keystroke behind.
    /// <para>
    /// Only the matches and the problems in the window become marks. A search over a long file finds
    /// tens of thousands, and every one of them was a decoration the block walked twice to throw away,
    /// on every build.
    /// </para>
    /// </summary>
    private IReadOnlyList<CodeDecoration> Marks(CodeEditorController editor, IReadOnlyList<CodeRange> matches,
        int first, int last, IAppTheme theme)
    {
        var problems = editor.Diagnostics.Items;
        if (matches.Count == 0 && !MatchBrackets && editor.Composition is null && problems.Count == 0
            && !editor.Snippet.IsActive && editor.GoTo.Preview is null && editor.Definitions.Link is null) return Decorations;

        var marks = new List<CodeDecoration>(Decorations);
        // What the go-to field reads, its lines washed whole under every other mark, as VS Code washes the
        // range it previews (#1083).
        if (editor.GoTo.Preview is { } previewed && previewed.End.Line >= first && previewed.Start.Line <= last)
            marks.Add(new CodeDecoration(previewed, CodeDecorationKind.Line) { Color = theme.Colors(Variant.Info).Subtle });
        // The word ⌘ links to its definition, underlined in the accent colour, as VS Code draws its
        // goto-definition-link (#1084).
        if (editor.Definitions.Link is { } link && link.End.Line >= first && link.Start.Line <= last)
            marks.Add(new CodeDecoration(link, CodeDecorationKind.Underline) { Color = theme.Colors(Variant.Primary).Base });
        // A snippet's placeholders in view are washed while it is being filled in, the one the carets stand at
        // too, as VS Code washes every tab stop (#1013).
        foreach (var placeholder in editor.Snippet.PlaceholdersIn(first, last))
            marks.Add(new CodeDecoration(placeholder, CodeDecorationKind.Highlight) { Color = theme.Colors(Variant.Info).Subtle });
        // Each problem in view under its range, as a wave in its severity's colour, or three dots for a
        // hint; the block draws one of no width under the cell where it stands.
        foreach (var problem in problems)
        {
            if (problem.Range.End.Line < first || problem.Range.Start.Line > last) continue;
            marks.Add(new CodeDecoration(problem.Range,
                problem.Severity == CodeDiagnosticSeverity.Hint ? CodeDecorationKind.Dots : CodeDecorationKind.Squiggle)
            {
                Color = CodeHoverCard.SeverityColor(theme, problem.Severity),
            });
        }
        // The text an input method is still composing is IN the document, and says so: underlined,
        // in the code's own ink, until it is committed or cancelled.
        if (editor.Composition is { } composition)
            marks.Add(new CodeDecoration(composition, CodeDecorationKind.Underline));
        if (matches.Count > 0)
        {
            var current = editor.Selection;
            for (var i = FirstEndingOnOrAfter(matches, first); i < matches.Count && matches[i].Start.Line <= last; i++)
            {
                var match = matches[i];
                // The one the caret is ON wears the outline; the rest are washed. Without that,
                // "next match" moves something nobody can see.
                marks.Add(new CodeDecoration(match,
                    match.Start.Equals(current.Start) && match.End.Equals(current.End)
                        ? CodeDecorationKind.Outline
                        : CodeDecorationKind.Highlight));
            }
        }
        if (MatchBrackets && editor.BracketAtCaret() is { } pair)
        {
            marks.Add(new CodeDecoration(
                new CodeRange(pair.Here, pair.Here with { Column = pair.Here.Column + 1 }),
                CodeDecorationKind.Outline));
            marks.Add(new CodeDecoration(
                new CodeRange(pair.There, pair.There with { Column = pair.There.Column + 1 }),
                CodeDecorationKind.Outline));
        }
        return marks;
    }

    /// <summary>
    /// Adopt the fresh configuration when the reconciler retains this instance across a parent
    /// rebuild — the hook every stateful component needs and this one did not have.
    /// <para>
    /// The base implementation does nothing, which is right for a component whose props never
    /// change, and silently wrong for one whose props do: a page that recomputed
    /// <see cref="Decorations"/> (diagnostics as you type), <see cref="Search"/> or
    /// <see cref="GutterMarkers"/> handed them to a NEW node the reconciler then threw away in
    /// favour of the retained one. The parent rebuilt, its own text updated, and the editor kept
    /// drawing the first frame's configuration forever — with nothing anywhere to say so.
    /// </para>
    /// <para>
    /// <see cref="InitialCode"/> is deliberately NOT adopted: the editor takes its text when it
    /// opens and the document is its own from then on, which is the whole reason it is called
    /// <em>initial</em>. Adopting it would overwrite what someone is typing on every parent build.
    /// </para>
    /// </summary>
    public override void AdoptConfig(UiComponent next)
    {
        if (next is not CodeEditor fresh) return;
        LanguageName = fresh.LanguageName;
        OnChanged = fresh.OnChanged;
        OnSelectionChanged = fresh.OnSelectionChanged;
        ShowLineNumbers = fresh.ShowLineNumbers;
        FirstLineNumber = fresh.FirstLineNumber;
        Height = fresh.Height;
        MaxHeight = fresh.MaxHeight;
        Size = fresh.Size;
        Inverse = fresh.Inverse;
        ReadOnly = fresh.ReadOnly;
        Autofocus = fresh.Autofocus;
        Caption = fresh.Caption;
        GutterMarkers = fresh.GutterMarkers;
        Decorations = fresh.Decorations;
        MatchBrackets = fresh.MatchBrackets;
        Search = fresh.Search;
        SearchMatchCase = fresh.SearchMatchCase;
        OnGutterPressed = fresh.OnGutterPressed;
        Completions = fresh.Completions;
        Diagnostics = fresh.Diagnostics;
        DiagnosticProviders = fresh.DiagnosticProviders;
        HoverProviders = fresh.HoverProviders;
        SignatureProviders = fresh.SignatureProviders;
        SymbolProviders = fresh.SymbolProviders;
        DefinitionProviders = fresh.DefinitionProviders;
        ReferenceProviders = fresh.ReferenceProviders;
        FoldProviders = fresh.FoldProviders;
        OnOpenRequested = fresh.OnOpenRequested;
        KeyBindings = fresh.KeyBindings;
    }

    /// <summary>What find is looking for: the bar's text while the bar is open, else what the app
    /// asked for through <see cref="Search"/>.</summary>
    private string? Needle => _findOpen && _findText.Length > 0 ? _findText : Search;

    /// <summary>The matches of the last search, and what they were found for: the document, the
    /// needle and the case rule. A document is immutable, so its reference says whether the text
    /// changed.</summary>
    private IReadOnlyList<CodeRange> _matches = [];
    private CodeDocument? _matchedIn;
    private string? _matchedFor;
    private bool _matchedCase;

    /// <summary>
    /// Every match of <paramref name="needle"/>, found again only when the document, the needle or
    /// the case rule changed. Every build scanned the whole file, and a build is a keystroke, a
    /// caret blink's worth of state and every step of a scroll: over 50,000 lines, 4 ms a step before
    /// anything was drawn.
    /// </summary>
    private IReadOnlyList<CodeRange> MatchesOf(CodeEditorController editor, string? needle)
    {
        if (needle is not { Length: > 0 }) return [];
        if (editor.Document != _matchedIn || needle != _matchedFor || SearchMatchCase != _matchedCase)
        {
            _matches = editor.FindAll(needle, SearchMatchCase);
            _matchedIn = editor.Document;
            _matchedFor = needle;
            _matchedCase = SearchMatchCase;
        }
        return _matches;
    }

    /// <summary>The index of the first match that ends on <paramref name="line"/> or after it. The
    /// matches are in document order and never overlap, so their ends are in order too.</summary>
    private static int FirstEndingOnOrAfter(IReadOnlyList<CodeRange> matches, int line)
    {
        var low = 0;
        var high = matches.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (matches[middle].End.Line < line) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var editor = Editor;
        editor.ReadOnly = ReadOnly;
        HandProviders(editor.Completion);
        HandIntelligence(editor);
        HandKeyBindings(editor);
        // The controller's OWN highlighter, kept across frames: a keystroke re-colours the line it
        // touched and stops, where a fresh one per frame would re-tokenize the file per character.
        var highlighter = editor.Highlighter;

        var metrics = CodeBlock.MetricsFor(context, Size, ShowLineNumbers,
            FirstLineNumber + editor.Document.LineCount - 1);
        // The gutter takes the chevrons right of its numbers, as VS Code's folding column stands (#1119): always,
        // regions or none, or the code would move sideways each time the first one came or the last one went.
        // With no numbers there is no gutter, and a folded header still shows its marker.
        var folds = editor.Folding.Regions;
        if (ShowLineNumbers) metrics = metrics with { GutterWidth = metrics.GutterWidth + CodeBlock.FoldSlot };

        // THE grid, handed to the engine — the only thing that turns a position into a point. The
        // block draws the lines on these same numbers, so the caret and the glyphs cannot disagree.
        // BEFORE the block reads the selection's bands: read first, they were drawn on the grid of
        // the build before, which on the first frame is the default one.
        editor.Grid = new CodeGrid(new Point(metrics.ContentLeft, metrics.ContentTop),
            new Size(metrics.ColumnWidth, metrics.LineHeight));

        // A BOUNDED editor scrolls the code inside it and builds only the lines in view: one that
        // fills its place (an IDE's pane), one of a fixed height, and one capped by MaxHeight. Only
        // an uncapped Hug grows with the code, and builds all of it, because all of it is on screen.
        var bounded = Height.Kind != SizeKind.Hug;
        var windowed = bounded || MaxHeight > 0;
        if (!windowed)
        {
            // An editor with no viewport shows every line. The window it had while it had one went
            // on limiting the build: a Fill editor switched to Hug went on building lines 1168 to
            // 1220 of 2000, and every other line was blank. And a viewport mounted again later
            // starts at the top, where this offset has to start too.
            _offset = 0;
            _viewport = 0;
        }
        // The lines this build draws, and the only ones anything is measured on: the selection's
        // bands and the matches are asked for between these two, not over the whole file.
        // Counted in ROWS: a folded region's lines have none (#1119), as a diff's hidden runs have one.
        var shown = editor.Grid.Rows;
        var rowCount = shown?.RowCount ?? editor.Document.LineCount;
        var (firstRow, lastRow) = CodeBlock.WindowOf(rowCount, metrics.LineHeight, _offset, _viewport);
        var (first, last) = shown?.LinesIn(firstRow, lastRow) ?? (firstRow, lastRow);

        // Every match, found once per search: the marks and the bar's count both read it.
        var matches = MatchesOf(editor, Needle);

        // The empty-string constructor + inits, NOT the (document, language) pair as arguments:
        // the transpiled twin has one constructor whose body is the string shape, and the property
        // assignment lands after it on both sides. CodeBlock.Of is this same move, packaged.
        var block = new CodeBlock("")
        {
            Document = editor.Document,
            Language = highlighter.Language,
            Decorations = Marks(editor, matches, first, last, context.Theme),
            ShowLineNumbers = ShowLineNumbers,
            FirstLineNumber = FirstLineNumber,
            // Bare content — see CodeBlock.Standalone. The slab and both scroll views are built
            // below, OUTSIDE the surface, so the marks travel with the code instead of being
            // painted against a box the code slid out from under.
            Standalone = false,
            Size = Size,
            Inverse = Inverse,
            GutterMarkers = Gutter(editor, first, last),
            OnGutterPressed = OnGutterPressed,
            // The rows a folded region hides, and the regions the gutter's chevrons fold (#1119).
            Rows = shown,
            Folds = folds,
            FoldedLines = FoldedLines(editor, folds),
            // Folding.Changed asks for the build.
            OnFoldPressed = line => editor.Folding.Toggle(line),
            Highlighter = highlighter,
            // THE grid — the same numbers the surface places the caret and the band on.
            Metrics = metrics,
            // The window the block builds. Both numbers come back from layout, so the first frame
            // builds everything and every frame after builds what you can see.
            ViewportOffset = _offset,
            ViewportHeight = _viewport,
            ViewportWidth = _viewportWidth,
            // A bounded editor's code fills its viewport however short the file: a press under the
            // last line is in the code, and the completion list has the viewport's room under it. A
            // hugging editor's viewport is its code, and taking its height as a floor would keep it
            // from ever shrinking.
            MinHeight = bounded ? _viewport : 0,
            // Every caret's line is washed, and the selections are drawn under the text — the pieces of
            // state the read-only block cannot know about. All are the ENGINE's measurements, on the grid
            // handed to it above, and only for the lines the window builds.
            ActiveLines = editor.CaretLinesIn(first, last),
            SelectionBands = editor.SelectionBandsIn(first, last),
            Selections = editor.SelectionsIn(first, last),
            WidestLine = editor.WidestLine,
        };

        // The completion list, while one shows: drawn by the surface at the word it completes, in the
        // code's own coordinates, so it moves with the code (docs/CODE-EDITOR-PLAN.md, §7).
        var completion = editor.Completion;
        VisualNode? offered = null;
        var offeredAt = Point.Zero;
        var highlighted = -1;
        // Which side of its line the list stands on, while one shows: the panel of a call takes the other.
        bool? listAbove = null;
        // The room over the code, in the surface's coordinates: the viewport, inside the surface, since the
        // scrollers around the surface clip whatever leaves it.
        var codeHeight = 2 * metrics.ContentTop + rowCount * metrics.LineHeight;
        var surfaceHeight = bounded ? MathF.Max(codeHeight, _viewport) : codeHeight;
        var viewTop = windowed ? _offset : 0;
        var viewBottom = windowed && _viewport > 0 ? MathF.Min(_offset + _viewport, surfaceHeight) : surfaceHeight;
        ReportView(editor, windowed);
        if (completion.IsOpen && completion.Selected >= 0)
        {
            var items = completion.Items;
            // The list only widens while it shows, so it never narrows under the pointer as the
            // word filters it. Measured once for each list the completion holds (a keystroke's filter
            // makes a new one), and for the selected entry on every build, the one a resolve fills
            // in where it stands: a language service's thousands of entries were walked on every
            // arrow key (found by Copilot reviewing #653).
            if (!ReferenceEquals(items, _measured))
            {
                _measured = items;
                _listColumns = Math.Max(_listColumns, CodeCompletionView.ColumnsOf(items));
            }
            _listColumns = Math.Max(_listColumns, CodeCompletionView.EntryColumns(items[completion.Selected].Item));
            var width = CodeCompletionView.WidthOf(metrics, _listColumns);
            if (_viewportWidth > 0) width = MathF.Min(width, _viewportWidth);
            var documentation = items[completion.Selected].Item.Documentation;
            var documentationLines = documentation is { Length: > 0 }
                ? CodeCompletionView.DocumentationLinesOf(context, metrics, documentation, width)
                : 0;

            var (x, y, rows, above, documentationShown) = CodeCompletionView.Place(metrics,
                editor.CaretRect(completion.Start), viewTop, viewBottom, _scrollX, _viewportWidth,
                Math.Min(CodeCompletionView.PageRows, items.Count), width, documentationLines,
                CodeCompletionView.DocumentationLineOf(context, metrics));

            // The page follows the selection past either end, and PageUp and PageDown step by it.
            var selected = completion.Selected;
            if (selected < _listTop) _listTop = selected;
            if (selected >= _listTop + rows) _listTop = selected - rows + 1;
            _listTop = Math.Max(0, Math.Min(_listTop, items.Count - rows));
            completion.PageSize = rows;

            offered = CodeCompletionView.Build(context, completion, metrics, _listTop, rows, width, above,
                documentation, documentationShown, index => Pick(editor, index));
            offeredAt = new Point(x, y);
            highlighted = selected - _listTop;
            listAbove = above;
        }
        else
        {
            _listTop = 0;
            _listColumns = 0;
            _measured = null;
        }

        // The card, while one shows and no list does: drawn by the surface over what it explains, in the
        // code's own coordinates, so it moves with the code (docs/CODE-EDITOR-PLAN.md, §7), above the
        // line it explains and below it when the view has no room above.
        var notes = new List<Positioned>();
        var hover = editor.Hover;
        if (hover.IsOpen && offered is null)
        {
            var (card, cardWidth, cardHeight) = CodeHoverCard.Build(context, hover, metrics, _viewportWidth);
            if (CodeNoteBuilder.Place(editor.CaretRect(hover.Range.Start), viewTop, viewBottom, _scrollX, _viewportWidth,
                    cardWidth, cardHeight, aboveTaken: null) is { } at)
                notes.Add(new Positioned(card, top: at.Y, start: at.X));
        }

        // What was said about nothing found where the person asked, at the caret where it was asked, while no card
        // shows, as VS Code's message stands there (#1084).
        if (editor.GoTo.NotFound is { } nothing && !hover.IsOpen && offered is null)
        {
            var (said, saidWidth, saidHeight) = CodeHoverCard.Message(context, metrics, CodeGoToView.NotFoundText(nothing),
                _viewportWidth);
            if (CodeNoteBuilder.Place(editor.CaretRect(nothing.At), viewTop, viewBottom, _scrollX, _viewportWidth,
                    saidWidth, saidHeight, aboveTaken: null) is { } at)
                notes.Add(new Positioned(said, top: at.Y, start: at.X));
        }

        // The panel of the call being written, while one shows and no card does (the card is what the person
        // asked for last): over the caret's line, under it when the view has no room above, and on the other
        // side of a list showing, or nowhere when that side has no room.
        var signatures = editor.SignatureHelp;
        if (signatures.IsOpen && signatures.Signature is not null && !hover.IsOpen)
        {
            var (panel, panelWidth, panelHeight) = CodeSignatureCard.Build(context, signatures, metrics, _viewportWidth);
            if (CodeNoteBuilder.Place(editor.CaretRect(editor.Caret), viewTop, viewBottom, _scrollX, _viewportWidth,
                    panelWidth, panelHeight, aboveTaken: listAbove) is { } at)
                notes.Add(new Positioned(panel, top: at.Y, start: at.X));
        }

        VisualNode surface = new CodeSurface(block, editor)
        {
            Autofocus = Autofocus,
            Label = Caption ?? SdkStrings.CodeEditor,
            // The caret writes with the BLOCK's ink, not the page's — see CodeBlock.InkFor.
            CaretColor = CodeBlock.InkFor(Inverse, context.Theme),
            // The controller mutates outside the tree, so the rebuild has to be asked for. This is
            // the seam: everything the surface does ends here, and here is where the app hears it.
            OnChanged = () => SetState(() => Notify(editor)),
            Options = offered,
            OptionsOrigin = offeredAt,
            HighlightedOption = highlighted,
            Notes = notes,
        };

        // The viewport lives OUT HERE, around the surface, rather than inside the block. One
        // coordinate space is the whole point: the surface travels with the code, so the caret and
        // the selection — which are drawn against it — travel with it too, and a pointer lands on
        // the column it is pointing at however far the file has been scrolled.
        //
        // Note the order: the SCROLLERS fill, the SURFACE hugs the code. That is what keeps a long
        // line from widening the pane it sits in — the viewport takes the width it is given and the
        // line overflows INSIDE it, which is the whole reason a scroll view is here at all.
        VisualNode viewport = new ScrollView(surface, ScrollAxis.Horizontal)
        {
            Width = SizeValue.Fill,
            // How wide it turned out to be, handed back to the block so the code is never narrower
            // than the space you can click in.
            OnViewportChanged = width =>
            {
                if (MathF.Abs(width - _viewportWidth) < 1) return;
                SetState(() => _viewportWidth = width);
            },
            // How far it slid, which only the completion list and the card read: a rebuild while either
            // shows, and otherwise a number kept for when one opens.
            OnScrolled = offset =>
            {
                if (MathF.Abs(offset - _scrollX) < 1) return;
                if (editor.Completion.IsOpen || editor.Hover.IsOpen || editor.SignatureHelp.IsOpen)
                    SetState(() => _scrollX = offset);
                else _scrollX = offset;
                ReportView(editor, windowed);
            },
            // Where the editor asks the code to slide to: a position brought into view across (#1083).
            Request = editor.HorizontalScroll,
        };

        // THE GUTTER, beside the sideways scroll and inside the vertical one below: the numbers
        // travel down the file with the code and stay put as it slides across. The block's own
        // instance builds them, over the same window, so there is one set of numbers built from one
        // measurement — and ContentLeft counts from where the CODE begins, not from where the gutter
        // does, which is what makes this arrangement possible at all.
        if (ShowLineNumbers)
        {
            var withGutter = new Row(gap: 0) { Width = SizeValue.Fill, Cross = CrossAlign.Start };
            withGutter.Add(block.Gutter(context));
            withGutter.Add(new Flexible(viewport));
            viewport = withGutter;
        }

        if (windowed)
        {
            viewport = new Box(new BoxStyle
            {
                Width = SizeValue.Fill,
                Height = bounded ? SizeValue.Fill : SizeValue.Hug,
                // A Hug editor's cap is the viewport's; a bounded one's caps the whole editor below.
                MaxHeight = !bounded && MaxHeight > 0 ? SizeValue.Fixed(MaxHeight) : SizeValue.Hug,
            }, new ScrollView(viewport)
            {
                Width = SizeValue.Fill,
                Height = bounded ? SizeValue.Fill : SizeValue.Hug,
                // The window the block builds, reported from the viewport that actually moves.
                OnScrolled = offset =>
                {
                    if (MathF.Abs(offset - _offset) < 1) return;
                    SetState(() => _offset = offset);
                    ReportView(editor, windowed);
                },
                // Where the editor asks the code to scroll to: a line centred, the view given back (#1083).
                Request = editor.VerticalScroll,
                OnViewportChanged = height =>
                {
                    if (MathF.Abs(height - _viewport) < 1) return;
                    SetState(() => _viewport = height);
                },
            });
        }

        // The slab the block used to paint for itself. It is out here now because it has to be the
        // VIEWPORT's frame, not the content's: a slab as wide as the file would round its corners
        // somewhere off screen.
        surface = new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = bounded ? SizeValue.Fill : SizeValue.Hug,
            Background = CodeBlock.SurfaceFor(Inverse, context.Theme),
            CornerRadius = new CornerRadii(context.Theme.Shape(ShapeScale.Medium)),
            Clip = true,
        }, viewport);

        // THE LAYERS, always: the code first, then its caption in the corner, then the find bar over
        // both. The code is the first layer whether or not anything is over it, so it keeps its place
        // in the tree. Opening find used to wrap it in a Stack it did not have before, which made it
        // a new surface to every host: the scroll went back to the top, "next" revealed nothing
        // (a surface seen for the first time is where it opened), and on Photon the keyboard pointed
        // at a path nothing had any more.
        // A bounded editor with a cap takes the height its place gives it up to the cap, slab and
        // all. The cap was the inner viewport's alone, and a Fill editor in a pane taller than it
        // drew an empty slab below the code. The box that caps it is always there, capping or not,
        // so a cap set or taken away leaves the code where it was in the tree.
        var capped = bounded && MaxHeight > 0;
        var layers = new Stack { Width = SizeValue.Fill, Height = SizeValue.Fill };
        layers.Add(surface);
        if (CodeBlock.Corner(Caption, null, Inverse, context.Theme) is { } corner) layers.Add(corner);
        if (_findOpen)
        {
            // The bar counts what ITS field looks for: with the field empty, the matches of the app's own
            // Search still mark the code, and are no count of anything typed.
            IReadOnlyList<CodeRange> found = _findText.Length > 0 ? matches : [];
            layers.Add(new Positioned(FindBar(context, editor, found), top: Space.S2, end: Space.S2));
        }
        // The go-to field over the top of the code, centred, as VS Code's quick input stands over its editor
        // (#1083), over the find bar when both show.
        var goTo = editor.GoTo;
        if (goTo.IsOpen)
        {
            var width = _viewportWidth > 0
                ? MathF.Min(CodeGoToView.MaxWidth, MathF.Max(Space.S4 * 12, _viewportWidth - 2 * Space.S4))
                : CodeGoToView.MaxWidth;
            // The page follows the selection past either end, and PageUp and PageDown step by it.
            if (goTo.Selected >= 0)
            {
                if (goTo.Selected < _goToTop) _goToTop = goTo.Selected;
                if (goTo.Selected >= _goToTop + CodeGoToView.PageRows) _goToTop = goTo.Selected - CodeGoToView.PageRows + 1;
            }
            _goToTop = Math.Max(0, Math.Min(_goToTop, goTo.Entries.Count - CodeGoToView.PageRows));
            goTo.PageSize = CodeGoToView.PageRows;
            var centred = new Row(main: MainAlign.Center) { Width = SizeValue.Fill };
            centred.Add(CodeGoToView.Build(context, editor, metrics, width, _goToTop,
                typed => SetState(() => goTo.Query = typed),
                () => SetState(() =>
                {
                    if (goTo.Accept()) Notify(editor);
                }),
                () => SetState(() => goTo.Dismiss()),
                index => SetState(() =>
                {
                    goTo.Select(index);
                    if (goTo.Accept()) Notify(editor);
                })));
            layers.Add(new Positioned(centred, top: Space.S2, start: 0, end: 0));
        }
        else _goToTop = 0;
        // The field's keys, wherever the keyboard is in this editor and only while it shows: the arrows and the
        // page keys walk its list and Escape closes it, giving the view back; the list hangs with no scrim, so
        // no panel's Escape stands in for it. Enter is the field's own. Chords
        // of the editor's own, as the find bar's Escape is, and inside it, so Escape closes the field first;
        // and mounted whether it shows or not, so nothing under them moves in the tree.
        VisualNode keyed = layers;
        keyed = GoToKey(keyed, "ArrowDown", goTo.IsOpen, () => goTo.SelectNext());
        keyed = GoToKey(keyed, "ArrowUp", goTo.IsOpen, () => goTo.SelectPrevious());
        keyed = GoToKey(keyed, "PageDown", goTo.IsOpen, () => goTo.SelectNextPage());
        keyed = GoToKey(keyed, "PageUp", goTo.IsOpen, () => goTo.SelectPreviousPage());
        keyed = GoToKey(keyed, "Escape", goTo.IsOpen, () => goTo.Close());
        // F4 and ⇧F4 walk a list of places while it shows, as they walk its places once it is closed (#1084).
        var placesShown = goTo.IsOpen && CodeGoToView.Lists(goTo.Mode);
        keyed = GoToKey(keyed, "F4", placesShown, () => goTo.NextPlace());
        keyed = new Shortcut(keyed, new KeyChord("F4", KeyModifiers.Shift), () => SetState(() => goTo.PreviousPlace()))
        {
            FocusScoped = true,
            Enabled = placesShown,
        };
        // Escape closes the bar wherever the keyboard is in this editor, the bar or the code, and only
        // while the bar is open: a chord of the editor's own, so that of two editors on one page the
        // one in use closes its bar, and one that binds nothing while the bar is closed, so a dialog
        // around the editor keeps its Escape (#457). Page-wide, the last bar mounted closed wherever
        // the keyboard was.
        var closable = new Shortcut(keyed, KeyChord.Escape, () => CloseFind(editor))
        {
            FocusScoped = true,
            Enabled = _findOpen,
        };
        // ⌘F is UI, not a model command, so it is not in the keymap: a chord of this editor's own,
        // answered while the keyboard is in it (the code, the caption or the bar), so that of two
        // editors on one page the one in use opens its find. Page-wide, the last editor mounted took
        // it wherever the keyboard was. Both chords wrap the layers whether the bar is open or not,
        // so nothing under them moves in the tree.
        var findable = new Shortcut(closable, new KeyChord("f", KeyModifiers.Command),
            () => SetState(() => _findOpen = true)) { FocusScoped = true };
        return new Box(new BoxStyle
        {
            Width = SizeValue.Fill,
            Height = Height,
            MaxHeight = capped ? SizeValue.Fixed(MaxHeight) : SizeValue.Hug,
        }, findable);
    }

    /// <summary>One key of the go-to field, answered while the keyboard is in this editor and only while the
    /// field shows.</summary>
    private Shortcut GoToKey(VisualNode child, string key, bool open, Func<bool> act) =>
        new(child, new KeyChord(key, KeyModifiers.None), () => SetState(() => act())) { FocusScoped = true, Enabled = open };

    /// <summary>Tells the editor the part of its code on screen, in the surface's coordinates (#1083): the two
    /// scroll views' offsets and sizes. Only a windowed editor scrolls its code; an uncapped Hug one is all on
    /// screen, its page scrolling instead, and has no height to centre in.</summary>
    private void ReportView(CodeEditorController editor, bool windowed) =>
        editor.View = new Rect(_scrollX, windowed ? _offset : 0, _viewportWidth, windowed ? _viewport : 0);

    /// <summary>Closes the bar and gives the code the keyboard back, which is where it came from
    /// or where it is anyway.</summary>
    private void CloseFind(CodeEditorController editor) => SetState(() =>
    {
        _findOpen = false;
        _findText = "";
        editor.RequestFocus();
    });

    /// <summary>
    /// The find bar: what to look for, how many there are, and the two ways through them. It floats
    /// over the top-right corner rather than pushing the code down — code that jumps when you open
    /// find has lost the line you were looking at.
    /// </summary>
    private VisualNode FindBar(ComponentContext context, CodeEditorController editor,
        IReadOnlyList<CodeRange> matches)
    {
        var theme = context.Theme;
        // Which match the caret is on, by halving: the matches are in document order, and a
        // search over a long file has tens of thousands of them.
        var index = 0;
        var current = editor.Selection.Start;
        var low = 0;
        var high = matches.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (matches[middle].Start.CompareTo(current) < 0) low = middle + 1;
            else high = middle;
        }
        if (low < matches.Count && matches[low].Start.Equals(current)) index = low + 1;

        // A STEP selects the match, which reveals it (the selection's own rule), and the app hears
        // that the selection moved, as it does when a key moves it.
        void Step(bool forward)
        {
            if (_findText.Length == 0) return;
            // Through the matches of what the field holds NOW, which the cache answers without a
            // search when the build saw the same: a key typed and Enter pressed before the next build
            // would have stepped through the matches of the text before it.
            if (editor.NextOf(MatchesOf(editor, _findText), backward: !forward) is not { } found) return;
            SetState(() =>
            {
                editor.Selection = found;
                Notify(editor);
            });
        }

        var row = new Row(gap: Space.S2) { Cross = CrossAlign.Center };
        row.Add(new Box(new BoxStyle { Width = 168 }, new TextEntry(_findText,
            value => SetState(() => _findText = value))
        {
            Placeholder = SdkStrings.Find,
            Label = SdkStrings.Find,
            Autofocus = true,
            OnSubmit = () => Step(true),
        }));
        row.Add(new Text(matches.Count == 0 ? (_findText.Length == 0 ? "" : "0") : $"{index}/{matches.Count}",
            TypeRole.LabelSmall, theme.TextMuted, maxLines: 1)
        {
            Tabular = true,
        });
        row.Add(new IconButton(new Icon(Icons.ChevronUp), SdkStrings.PreviousMatch)
        {
            Size = SizeVariant.Small,
            OnPressed = () => Step(false),
        });
        row.Add(new IconButton(new Icon(Icons.ChevronDown), SdkStrings.NextMatch)
        {
            Size = SizeVariant.Small,
            OnPressed = () => Step(true),
        });
        row.Add(new IconButton(new Icon(Icons.Close), SdkStrings.CloseFind)
        {
            Size = SizeVariant.Small,
            OnPressed = () => CloseFind(editor),
        });

        return new Box(new BoxStyle
        {
            Background = theme.Surface,
            BorderWidth = 1,
            BorderColor = theme.Border,
            CornerRadius = new CornerRadii(theme.Shape(ShapeScale.Medium)),
            Padding = EdgeInsets.Symmetric(Space.S2, Space.S1),
            Shadow = theme.Elevation(2),
        }, row);
    }
}
