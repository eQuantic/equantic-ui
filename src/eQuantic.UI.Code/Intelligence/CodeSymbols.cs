namespace eQuantic.UI.Code;

/// <summary>
/// The symbols of ONE editor's document: where they come from (<see cref="Providers"/>) and the asking.
/// Going to a symbol asks them (<see cref="CodeGoTo"/>, #1083), and what slice 6 draws from them, a sticky
/// scroll and a breadcrumb, asks the same providers here rather than each keeping its own.
/// </summary>
public sealed class CodeSymbols
{
    private readonly CodeEditorController _editor;

    internal CodeSymbols(CodeEditorController editor) => _editor = editor;

    /// <summary>
    /// The language services this editor asks, in the order their answers are joined. None to begin with,
    /// and with none a document declares nothing.
    /// </summary>
    public IList<ICodeSymbolProvider> Providers { get; } = [];

    /// <summary>Raised when a provider throws, with what it threw. The other providers' answers still
    /// count, so a failing provider is otherwise silent.</summary>
    public event Action<Exception>? Failed;

    /// <summary>
    /// What every provider answers about the document as it is now, each provider's symbols after the one
    /// before it in <see cref="Providers"/>. They are all asked at once, and the answer waits for the last:
    /// what VS Code's outline waits for before its list. One that throws is reported through
    /// <see cref="Failed"/> and counts as answering nothing; a cancelled request throws, as a cancelled
    /// task does.
    /// </summary>
    public async Task<IReadOnlyList<CodeSymbol>> RequestAsync(CancellationToken cancellation)
    {
        var document = _editor.Document;
        var asked = new List<Task<IReadOnlyList<CodeSymbol>>>();
        foreach (var provider in Providers) asked.Add(AskOne(provider, document, cancellation));
        var answer = new List<CodeSymbol>();
        foreach (var one in asked) answer.AddRange(await one);
        cancellation.ThrowIfCancellationRequested();
        return answer;
    }

    private async Task<IReadOnlyList<CodeSymbol>> AskOne(ICodeSymbolProvider provider, CodeDocument document,
        CancellationToken cancellation)
    {
        try
        {
            return await provider.SymbolsAsync(document, cancellation);
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
}
