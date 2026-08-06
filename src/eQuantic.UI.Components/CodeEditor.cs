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

    public CodeEditor(string code = "", string? language = null)
    {
        InitialCode = code;
        LanguageName = language;
    }

    /// <summary>The text the editor OPENS with. Later changes to it are ignored — the document is
    /// the editor's own from the first keystroke, and reopening it is a new editor.</summary>
    public string InitialCode { get; init; }

    /// <summary>Which language colours it — a name or an extension ("csharp", ".ts", "python").</summary>
    public string? LanguageName { get; init; }

    /// <summary>Raised after every edit, with the whole document. Null = the app is not tracking it.</summary>
    public Action<string>? OnChanged { get; init; }

    /// <summary>Raised when the caret or the selection moves — a status bar's cue.</summary>
    public Action<CodeRange>? OnSelectionChanged { get; init; }

    public bool ShowLineNumbers { get; init; } = true;
    public int FirstLineNumber { get; init; } = 1;
    public float MaxHeight { get; init; }
    public SizeVariant Size { get; init; } = SizeVariant.Small;
    public bool Inverse { get; init; }
    public bool ReadOnly { get; init; }
    public bool Autofocus { get; init; }
    public string? Caption { get; init; }

    /// <summary>Lines to point at — an error's line, a breakpoint, a diff hunk.</summary>
    public IReadOnlyList<CodeGutterMarker> GutterMarkers { get; init; } = [];

    /// <summary>Ranges to mark — search matches, a squiggle.</summary>
    public IReadOnlyList<CodeDecoration> Decorations { get; init; } = [];

    /// <summary>A press on a gutter row — where an IDE toggles a breakpoint.</summary>
    public Action<int>? OnGutterPressed { get; init; }

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
        return editor;
    }

    public override VisualNode Build(ComponentContext context)
    {
        var editor = Editor;
        editor.ReadOnly = ReadOnly;
        // The controller's OWN highlighter, kept across frames: a keystroke re-colours the line it
        // touched and stops, where a fresh one per frame would re-tokenize the file per character.
        var highlighter = editor.Highlighter;

        var metrics = CodeBlock.MetricsFor(context, Size, ShowLineNumbers,
            FirstLineNumber + editor.Document.LineCount - 1);

        var block = new CodeBlock(editor.Document, highlighter.Language)
        {
            ShowLineNumbers = ShowLineNumbers,
            FirstLineNumber = FirstLineNumber,
            MaxHeight = MaxHeight,
            Size = Size,
            Inverse = Inverse,
            Caption = Caption,
            GutterMarkers = GutterMarkers,
            Decorations = Decorations,
            OnGutterPressed = OnGutterPressed,
            Highlighter = highlighter,
            // The caret's line is washed while the editor holds it — the one piece of state the
            // read-only block cannot know about.
            ActiveLine = editor.Caret.Line,
        };

        return new CodeSurface(block, editor)
        {
            ContentTop = metrics.ContentTop,
            LineHeight = metrics.LineHeight,
            ContentLeft = metrics.ContentLeft,
            ColumnWidth = metrics.ColumnWidth,
            Autofocus = Autofocus,
            Label = Caption ?? "Code editor",
            // The controller mutates outside the tree, so the rebuild has to be asked for. This is
            // the seam: everything the surface does ends here, and here is where the app hears it.
            OnChanged = () => SetState(() =>
            {
                OnChanged?.Invoke(editor.Document.Text);
                OnSelectionChanged?.Invoke(editor.Selection);
            }),
        };
    }
}
