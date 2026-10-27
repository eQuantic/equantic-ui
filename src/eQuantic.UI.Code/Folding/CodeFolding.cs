namespace eQuantic.UI.Code;

/// <summary>
/// The folding of ONE editor (#1119): the regions its document folds into, which of them are folded, and the
/// commands that fold and unfold them, as VS Code's folding controller has them. It is a session of the plan's
/// fifth section, driven by keys and methods in a test with no screen; the editor draws its chevrons and hides
/// what it folds.
/// <para>
/// The regions come from <see cref="Providers"/> (LSP's <c>foldingRange</c>), asked once the text has rested for
/// <see cref="Delay"/>, or from the indentation's provider when there is none, as VS Code's "auto" strategy has it.
/// Their answers are joined as VS Code's <c>sanitizeAndMerge</c> joins them: ordered by their header line, one region
/// for each header (the one reaching furthest), a region that crosses another dropped, and one with no line under
/// its header dropped.
/// </para>
/// <para>
/// A fold never hides what is worked on. Between answers the regions are carried through every edit, a folded one
/// staying folded; an edit that changes a hidden line unfolds the regions hiding it, and a selection moved onto a
/// hidden line (a jump, find, go to line) unfolds them too, as VS Code's hidden range model does. When the next
/// answer arrives, a region whose header line was folded is folded still.
/// </para>
/// </summary>
public sealed class CodeFolding
{
    /// <summary>How far past the end of a line a region's last line reaches, for an edit to carry it by its line.</summary>
    private const int LineEnd = 1 << 20;

    private readonly CodeEditorController _editor;
    // The regions, ordered by their header line, and whether each is folded, in the same order.
    private List<CodeFold> _regions = [];
    private List<bool> _folded = [];
    // The rows a grid draws with what is folded hidden, or null when nothing is.
    private CodeRows? _rows;
    private List<CodeCollapse> _collapses = [];
    private int _generation;
    private CancellationTokenSource? _request;

    internal CodeFolding(CodeEditorController editor)
    {
        _editor = editor;
        editor.Changed += Edited;
        editor.SelectionChanged += Selected;
    }

    /// <summary>
    /// The language services this editor asks for its regions. None to begin with, and with none the indentation's
    /// provider answers, so every language folds.
    /// </summary>
    public IList<ICodeFoldProvider> Providers { get; } = [];

    /// <summary>
    /// Whether this editor folds: true to begin with. An editor that draws every line whatever is folded (a cell of
    /// a spreadsheet, whose entry draws its own lines) turns it off, and then it has no region, asks nothing, and its
    /// fold commands claim no key, as VS Code's <c>folding</c> option turns its folding off.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (value)
            {
                Check();
                return;
            }
            _generation++;
            ProviderRequest.Cancel(_request, Failed);
            _request = null;
            _regions = [];
            _folded = [];
            Apply();
        }
    }

    private bool _enabled = true;

    /// <summary>How long typing has to pause before the regions are asked again. VS Code's own pause.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>The regions, ordered by their header line, nested or apart, never crossing.</summary>
    public IReadOnlyList<CodeFold> Regions => _regions;

    /// <summary>What the folded regions hide, for a grid to draw no row of: the lines under each folded header that no
    /// other folded region already hides.</summary>
    public IReadOnlyList<CodeCollapse> Collapses => _collapses;

    /// <summary>The rows of the document with what is folded hidden, or null when nothing is.</summary>
    internal CodeRows? Rows => _rows;

    /// <summary>Raised when the regions or what is folded change.</summary>
    public event Action? Changed;

    /// <summary>Raised when a provider throws, with what it threw. That answer changes nothing: the regions and what
    /// is folded stay as they were until the next one.</summary>
    public event Action<Exception>? Failed;

    /// <summary>Whether the region whose header is <paramref name="line"/> is folded.</summary>
    public bool IsFolded(int line)
    {
        var at = IndexOf(line);
        return at >= 0 && _folded[at];
    }

    /// <summary>Whether <paramref name="line"/> is under a folded header, and drawn on no row.</summary>
    public bool IsHidden(int line) => HeaderHiding(line) >= 0;

    /// <summary>The innermost region holding <paramref name="line"/>, its header or a line under it, or null.</summary>
    public CodeFold? RegionAt(int line)
    {
        var at = Innermost(line, null);
        return at >= 0 ? _regions[at] : null;
    }

    /// <summary>Asks the providers now, without waiting for the text to rest: when a document opens, or the
    /// providers change.</summary>
    public void Check() => _ = Ask(wait: false);

    // ---- the commands -----------------------------------------------------------------------------------

    /// <summary>Folds the innermost open region holding <paramref name="line"/> (⌥⌘[). Answers whether there was one.</summary>
    public bool Fold(int line) => SetInnermost(line, folded: true);

    /// <summary>Unfolds the innermost folded region holding <paramref name="line"/> (⌥⌘]). Answers whether there was one.</summary>
    public bool Unfold(int line) => SetInnermost(line, folded: false);

    /// <summary>Folds the innermost region holding <paramref name="line"/> and every region inside it (⌘K ⌘[).</summary>
    public bool FoldRecursively(int line) => SetWithin(line, folded: true);

    /// <summary>Unfolds the innermost region holding <paramref name="line"/> and every region inside it (⌘K ⌘]).</summary>
    public bool UnfoldRecursively(int line) => SetWithin(line, folded: false);

    /// <summary>Folds the innermost region holding <paramref name="line"/> when it is open and unfolds it when it is
    /// folded (⌘K ⌘L, and a press on its chevron). Answers whether there was one.</summary>
    public bool Toggle(int line)
    {
        var at = Innermost(line, null);
        if (at < 0) return false;
        _folded[at] = !_folded[at];
        Folded();
        return true;
    }

    /// <summary>Folds every region (⌘K ⌘0). Answers whether one was open.</summary>
    public bool FoldAll() => SetAll(folded: true);

    /// <summary>Unfolds every region (⌘K ⌘J). Answers whether one was folded.</summary>
    public bool UnfoldAll() => SetAll(folded: false);

    /// <summary>Whether <see cref="Fold"/> would fold a region at <paramref name="line"/>.</summary>
    internal bool CanFold(int line) => Innermost(line, false) >= 0;

    /// <summary>Whether <see cref="Unfold"/> would unfold a region at <paramref name="line"/>.</summary>
    internal bool CanUnfold(int line) => Innermost(line, true) >= 0;

    /// <summary>Whether a region holds <paramref name="line"/>.</summary>
    internal bool CanToggle(int line) => Innermost(line, null) >= 0;


    /// <summary>Whether any region is folded, or open, as <paramref name="folded"/> asks.</summary>
    internal bool Any(bool folded)
    {
        foreach (var state in _folded)
            if (state == folded) return true;
        return false;
    }

    private bool SetInnermost(int line, bool folded)
    {
        var at = Innermost(line, !folded);
        if (at < 0) return false;
        _folded[at] = folded;
        Folded();
        return true;
    }

    private bool SetWithin(int line, bool folded)
    {
        var at = Innermost(line, null);
        if (at < 0) return false;
        var outer = _regions[at];
        var changed = false;
        for (var i = at; i < _regions.Count && _regions[i].StartLine <= outer.EndLine; i++)
        {
            if (_regions[i].EndLine > outer.EndLine || _folded[i] == folded) continue;
            _folded[i] = folded;
            changed = true;
        }
        if (changed) Folded();
        return changed;
    }

    private bool SetAll(bool folded)
    {
        var changed = false;
        for (var i = 0; i < _folded.Count; i++)
        {
            if (_folded[i] == folded) continue;
            _folded[i] = folded;
            changed = true;
        }
        if (changed) Folded();
        return changed;
    }

    /// <summary>The index of the innermost region holding <paramref name="line"/> whose folded state is
    /// <paramref name="folded"/>, any state when null, or -1. A region holds its header and every line under it.</summary>
    private int Innermost(int line, bool? folded)
    {
        // The regions are ordered by their header and never cross, so the last one holding the line is the innermost.
        var found = -1;
        for (var i = 0; i < _regions.Count && _regions[i].StartLine <= line; i++)
        {
            if (line > _regions[i].EndLine) continue;
            if (folded is { } state && _folded[i] != state) continue;
            found = i;
        }
        return found;
    }

    /// <summary>Whether <paramref name="region"/>, folded, would hide one of <paramref name="lines"/>.</summary>
    private static bool Hides(CodeFold region, List<int> lines)
    {
        foreach (var line in lines)
            if (region.StartLine < line && line <= region.EndLine) return true;
        return false;
    }

    /// <summary>The index of the region whose header is <paramref name="line"/>, or -1. The regions are ordered by
    /// their header, one for each, so it is searched for by halves: a view asks for every header it draws.</summary>
    private int IndexOf(int line)
    {
        var low = 0;
        var high = _regions.Count - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var header = _regions[middle].StartLine;
            if (header == line) return middle;
            if (header < line) low = middle + 1;
            else high = middle - 1;
        }
        return -1;
    }

    // ---- what is hidden ---------------------------------------------------------------------------------

    /// <summary>
    /// A command folded or unfolded: what is hidden is made again, and a caret a fold hid is put at the end of the
    /// outermost header it is hidden under, every caret of several, as VS Code moves its cursors out of what it folds.
    /// </summary>
    private void Folded()
    {
        Apply();
        var selections = new List<CodeRange>(_editor.Selections);
        var moved = false;
        for (var i = 0; i < selections.Count; i++)
        {
            var header = HeaderHiding(selections[i].Focus.Line);
            if (header < 0) continue;
            selections[i] = new CodeRange(new CodePosition(header, _editor.Document.Line(header).Length));
            moved = true;
        }
        if (moved) _editor.Selections = selections;
    }

    /// <summary>The header of the outermost folded region hiding <paramref name="line"/>, or -1 when it shows.</summary>
    private int HeaderHiding(int line)
    {
        // Ordered by their header, the first folded region holding the line is the outermost.
        for (var i = 0; i < _regions.Count; i++)
            if (_folded[i] && _regions[i].StartLine < line && line <= _regions[i].EndLine) return _regions[i].StartLine;
        return -1;
    }

    /// <summary>What is folded changed: the collapses and the rows are made again, and the editor hears it.</summary>
    private void Apply()
    {
        Rebuild();
        _editor.FoldsChanged();
        Changed?.Invoke();
    }

    /// <summary>The collapses of the folded regions no other folded region holds, and the rows they make.</summary>
    private void Rebuild()
    {
        var collapses = new List<CodeCollapse>();
        var hiddenTo = -1;
        for (var i = 0; i < _regions.Count; i++)
        {
            var region = _regions[i];
            if (!_folded[i] || region.StartLine < hiddenTo) continue;
            collapses.Add(new CodeCollapse(region.StartLine + 1, region.EndLine, Placeholder: false));
            hiddenTo = region.EndLine + 1;
        }
        _collapses = collapses;
        _rows = collapses.Count == 0 ? null : new CodeRows(_editor.Document.LineCount, [], collapses);
    }

    // ---- the text and the caret -------------------------------------------------------------------------

    /// <summary>
    /// The text changed. A new document drops every region and asks at once. An edit carries every region by its
    /// lines, unfolds a folded one whose hidden lines it reaches, drops one whose header line it removed or joined to
    /// the line above, and asks again once typing pauses.
    /// </summary>
    private void Edited(CodeChange change)
    {
        if (change.Replaced)
        {
            _regions = [];
            _folded = [];
            Apply();
            Check();
            return;
        }
        var regions = new List<CodeFold>();
        var folded = new List<bool>();
        for (var i = 0; i < _regions.Count; i++)
        {
            var region = _regions[i];
            var isFolded = _folded[i];
            var kept = true;
            foreach (var edit in change.Edits)
            {
                var from = edit.Range.Start.Line;
                var to = edit.Range.End.Line;
                // An edit that reaches a hidden line shows it.
                if (isFolded && from <= region.EndLine && to > region.StartLine) isFolded = false;
                var carried = edit.Carry(new CodeRange(new CodePosition(region.StartLine, 0),
                    new CodePosition(region.EndLine, LineEnd)));
                // An edit from above the header that takes text of the header line, or leaves the header after
                // text of the line above, removed the header line: the lines above it deleted leave it a line. So
                // does one from the header's first column past its line break, which takes the whole line.
                var header = new CodePosition(region.StartLine, 0);
                if ((from < region.StartLine && to >= region.StartLine
                        && (edit.Range.End > header || carried.Start.Column != 0))
                    || (edit.Range.Start == header && to > region.StartLine))
                {
                    kept = false;
                    break;
                }
                region = region with { StartLine = carried.Start.Line, EndLine = carried.End.Line };
            }
            if (!kept || region.EndLine <= region.StartLine) continue;
            regions.Add(region);
            folded.Add(isFolded);
        }
        _regions = regions;
        _folded = folded;
        Apply();
        // A grid with rows of its own (a diff's side, a formula bar) hides no fold, so nothing is asked for it.
        if (_editor.HidesFolds) _ = Ask(wait: true);
    }

    /// <summary>The selection moved: a line a selection ends on that is hidden is shown, every caret's of several,
    /// the regions hiding it unfolded.</summary>
    private void Selected(CodeRange _)
    {
        var changed = false;
        foreach (var selection in _editor.Selections)
        {
            if (Reveal(selection.Anchor.Line)) changed = true;
            if (Reveal(selection.Focus.Line)) changed = true;
        }
        if (changed) Apply();
    }

    /// <summary>Unfolds every folded region hiding <paramref name="line"/>. Answers whether one did.</summary>
    private bool Reveal(int line)
    {
        var changed = false;
        for (var i = 0; i < _regions.Count; i++)
        {
            if (!_folded[i] || line <= _regions[i].StartLine || line > _regions[i].EndLine) continue;
            _folded[i] = false;
            changed = true;
        }
        return changed;
    }

    // ---- asking -----------------------------------------------------------------------------------------

    /// <summary>
    /// Asks every provider, or the indentation's when there is none, after the pause when <paramref name="wait"/>
    /// says so. Whatever was waiting or out is cancelled first; an answer is applied only while its generation is
    /// the latest.
    /// </summary>
    private async Task Ask(bool wait)
    {
        var generation = ++_generation;
        ProviderRequest.Cancel(_request, Failed);
        _request = null;
        if (!_enabled) return;
        var request = new CancellationTokenSource();
        _request = request;
        if (wait && Delay > TimeSpan.Zero)
        {
            var waited = true;
            try
            {
                await Task.Delay(Delay, request.Token);
            }
            catch (OperationCanceledException)
            {
                waited = false;
            }
            // Back where the editor lives before anything of it is read: a timer ends the wait on a thread of its
            // own, and the document, the providers and the generation are the UI thread's (a Photon host has no
            // synchronization context to bring an await back to it), as the problems' session does.
            UiThread.Run(() =>
            {
                if (waited && generation == _generation) _ = AskEvery(generation, request.Token);
            });
            return;
        }
        await AskEvery(generation, request.Token);
    }

    /// <summary>Asks every provider about the document as it stands, on the UI thread, each before any is waited for,
    /// and applies their answers there when the last one comes.</summary>
    private async Task AskEvery(int generation, CancellationToken cancellation)
    {
        var document = _editor.Document;
        var providers = new List<ICodeFoldProvider>(Providers);
        if (providers.Count == 0) providers.Add(new IndentationFoldProvider());
        var asked = new List<Task<IReadOnlyList<CodeFold>?>>();
        foreach (var provider in providers) asked.Add(AskOne(provider, document, cancellation));
        var answers = new List<IReadOnlyList<CodeFold>>();
        foreach (var one in asked)
        {
            // A provider that failed changes nothing: the regions and what is folded stay until the next answer, as
            // a language server that timed out once must not open everything the person folded.
            if (await one is not { } answer) return;
            answers.Add(answer);
        }
        // Back where the editor lives: a provider and a timer end on a thread of their own, and the regions are the
        // UI thread's (a Photon host has no synchronization context to bring an await back to it).
        UiThread.Run(() =>
        {
            if (generation != _generation) return;
            Answered(answers, document);
        });
    }

    /// <summary>What <paramref name="provider"/> answers, or null when it threw or was cancelled.</summary>
    private async Task<IReadOnlyList<CodeFold>?> AskOne(ICodeFoldProvider provider, CodeDocument document,
        CancellationToken cancellation)
    {
        try
        {
            return await provider.FoldsAsync(document, cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception error)
        {
            UiThread.Run(() => Failed?.Invoke(error));
            return null;
        }
    }

    /// <summary>
    /// The regions answered, joined as VS Code joins them. A region whose header was folded stays folded, unless it
    /// would hide a line a selection ends on, as VS Code's folding model blocks the lines its selections stand on: an
    /// answer never hides the caret.
    /// </summary>
    private void Answered(List<IReadOnlyList<CodeFold>> answers, CodeDocument document)
    {
        var blocked = new List<int>();
        foreach (var selection in _editor.Selections)
        {
            blocked.Add(selection.Anchor.Line);
            blocked.Add(selection.Focus.Line);
        }
        var all = new List<CodeFold>();
        foreach (var answer in answers)
            foreach (var region in answer)
                if (region.StartLine >= 0 && region.EndLine < document.LineCount && region.EndLine > region.StartLine)
                    all.Add(region);
        // By header, and of two on one header the one reaching furthest first, which is the one kept.
        all.Sort((a, b) => a.StartLine != b.StartLine ? a.StartLine.CompareTo(b.StartLine) : b.EndLine.CompareTo(a.EndLine));
        var regions = new List<CodeFold>();
        var folded = new List<bool>();
        var open = new List<CodeFold>();
        foreach (var region in all)
        {
            while (open.Count > 0 && open[open.Count - 1].EndLine < region.StartLine) open.RemoveAt(open.Count - 1);
            if (open.Count > 0)
            {
                var holding = open[open.Count - 1];
                // One region for each header, and none crossing the one that holds it.
                if (region.StartLine == holding.StartLine || region.EndLine > holding.EndLine) continue;
            }
            open.Add(region);
            regions.Add(region);
            folded.Add(IsFolded(region.StartLine) && !Hides(region, blocked));
        }
        _regions = regions;
        _folded = folded;
        Apply();
    }
}
