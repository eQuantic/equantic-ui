using eQuantic.UI.Primitives;

namespace eQuantic.UI.Code;

/// <summary>
/// Where what stands at a position in ONE editor's document is defined (#1084): the providers that answer it,
/// the asking, and the LINK a pointer follows with ⌘ held. Going to a definition asks them
/// (<see cref="CodeGoTo.Definition"/>), which goes to or lists what they answer.
/// <para>
/// The link is VS Code's <c>ClickLinkGesture</c>. While the engine's Command modifier is held alone (⌘ on
/// Apple, Ctrl elsewhere, where ⌥ adds carets on both) and the pointer rests on a word, the definitions there
/// are asked at once, not after the hover's pause, and once for each word. An answer with a place shows the
/// link: the word is <see cref="Link"/>, which the editor underlines, and the pointer is a hand over it. The
/// link goes when the modifier is released or another key pressed, when the pointer leaves the word, when a
/// selection is made, when the text changes and when the view scrolls. A press with the modifier, and its
/// release on the same line, go to the definition at the press (<see cref="CodeEditorController.HandlePointer"/>).
/// </para>
/// </summary>
public sealed class CodeDefinitions
{
    private readonly CodeEditorController _editor;
    private int _generation;
    private CancellationTokenSource? _request;
    // The word asked for, or linked: the pointer moving along it asks nothing again.
    private CodeRange? _asked;
    private CodeRange? _link;

    internal CodeDefinitions(CodeEditorController editor)
    {
        _editor = editor;
        // The link stands on the code as it was: an edit takes it away, and so does a selection being made, as a
        // drag with ⌘ held makes one (VS Code stops its link the moment one starts).
        editor.Changed += _ => ClearLink();
        editor.SelectionChanged += Selected;
    }

    private void Selected(CodeRange selection)
    {
        if (!selection.IsEmpty) ClearLink();
    }

    /// <summary>
    /// The language services this editor asks, in the order their answers are joined. None to begin with, and
    /// with none nothing is defined anywhere: F12 claims nothing and ⌘ over a name shows no link.
    /// </summary>
    public IList<ICodeDefinitionProvider> Providers { get; } = [];

    /// <summary>Raised when a provider throws, with what it threw. The other providers' answers still count,
    /// so a failing provider is otherwise silent.</summary>
    public event Action<Exception>? Failed;

    /// <summary>The word the pointer follows with ⌘ held, which the editor underlines, or null with none.</summary>
    public CodeRange? Link => _link;

    /// <summary>Raised when the link shows or goes.</summary>
    public event Action? Changed;

    /// <summary>
    /// Where every provider says what stands at <paramref name="position"/> is defined, declared, typed or
    /// implemented, as <paramref name="kind"/> asks, in the document as it is now, as one list
    /// (<see cref="CodeLocation"/>'s order: the editor's own document first, then by name, then by place, each
    /// place once). They are all asked at once, and the answer waits for the last. One that throws is reported
    /// through <see cref="Failed"/> and counts as answering nothing; a cancelled request throws, as a cancelled
    /// task does.
    /// </summary>
    public async Task<IReadOnlyList<CodeLocation>> RequestAsync(CodePosition position, CodeDefinitionKind kind,
        CancellationToken cancellation)
    {
        var document = _editor.Document;
        var asked = new List<Task<IReadOnlyList<CodeLocation>>>();
        foreach (var provider in Providers) asked.Add(AskOne(provider, document, position, kind, cancellation));
        var answers = new List<IReadOnlyList<CodeLocation>>();
        foreach (var one in asked) answers.Add(await one);
        cancellation.ThrowIfCancellationRequested();
        return CodeLocation.Joined(answers);
    }

    private async Task<IReadOnlyList<CodeLocation>> AskOne(ICodeDefinitionProvider provider, CodeDocument document,
        CodePosition position, CodeDefinitionKind kind, CancellationToken cancellation)
    {
        try
        {
            return await provider.DefinitionsAsync(document, position, kind, cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return [];
        }
        catch (Exception error)
        {
            UiThread.Run(() => Failed?.Invoke(error));
            return [];
        }
    }

    // ---- the link ---------------------------------------------------------------------------------------

    /// <summary>Whether <paramref name="modifiers"/> are the link's: Command, and nothing else.</summary>
    internal static bool Follows(KeyModifiers modifiers) =>
        (modifiers & (KeyModifiers.Command | KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift)) == KeyModifiers.Command;

    /// <summary>
    /// The pointer rests over the code on the character at <paramref name="character"/>, or on none, with
    /// <paramref name="modifiers"/> held: a host says so as the pointer moves, and again when the modifiers
    /// change with the pointer at rest. With the link's modifier alone over a word, the word's definitions are
    /// asked unless they were for this word already; anything else takes the link away.
    /// </summary>
    internal void PointerAt(CodePosition? character, KeyModifiers modifiers)
    {
        if (!Follows(modifiers) || Providers.Count == 0 || character is not { } at || WordUnder(at) is not { } word)
        {
            ClearLink();
            return;
        }
        if (_asked is { } asked && asked == word) return;
        ClearLink();
        _asked = word;
        var generation = _generation;
        var request = new CancellationTokenSource();
        _request = request;
        _ = AskLink(word, at, generation, request.Token);
    }

    private async Task AskLink(CodeRange word, CodePosition at, int generation, CancellationToken cancellation)
    {
        IReadOnlyList<CodeLocation> places;
        try
        {
            places = await RequestAsync(at, CodeDefinitionKind.Definition, cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return;
        }
        UiThread.Run(() =>
        {
            if (generation != _generation || places.Count == 0) return;
            _link = word;
            Changed?.Invoke();
        });
    }

    /// <summary>Whether the character at <paramref name="character"/> is on the link.</summary>
    internal bool OnLink(CodePosition? character) =>
        _link is { } link && character is { } at && link.Start <= at && at < link.End;

    /// <summary>Takes the link away, and drops what was being asked for it: what the editor does when the modifier is
    /// released, a key pressed, the pointer gone, the text edited or the view scrolled, and an app does when the
    /// providers it handed are no longer the ones that answered.</summary>
    public void ClearLink()
    {
        _generation++;
        ProviderRequest.Cancel(_request, Failed);
        _request = null;
        _asked = null;
        if (_link is null) return;
        _link = null;
        Changed?.Invoke();
    }

    /// <summary>The word on the character at <paramref name="at"/>, or null when that character is none of a
    /// word's: VS Code's <c>getWordAtPosition</c>, which a link follows.</summary>
    internal CodeRange? WordUnder(CodePosition at)
    {
        var document = _editor.Document;
        if (at.Line < 0 || at.Line >= document.LineCount) return null;
        var line = document.Line(at.Line);
        if (at.Column < 0 || at.Column >= line.Length || !CodeDocument.IsWordChar(line[at.Column])) return null;
        return document.WordAt(at);
    }
}
