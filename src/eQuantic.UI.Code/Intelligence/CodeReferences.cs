namespace eQuantic.UI.Code;

/// <summary>
/// Where what stands at a position in ONE editor's document is used (#1084): the providers that answer it and
/// the asking. Finding the references asks them (<see cref="CodeGoTo.References"/>), which lists or goes to what
/// they answer.
/// </summary>
public sealed class CodeReferences
{
    private readonly CodeEditorController _editor;

    internal CodeReferences(CodeEditorController editor) => _editor = editor;

    /// <summary>The language services this editor asks. None to begin with, and with none nothing is
    /// used anywhere.</summary>
    public IList<ICodeReferenceProvider> Providers { get; } = [];

    /// <summary>Raised when a provider throws, with what it threw. The other providers' answers still count,
    /// so a failing provider is otherwise silent.</summary>
    public event Action<Exception>? Failed;

    /// <summary>
    /// What every provider answers for <paramref name="position"/> in the document as it is now, as one list
    /// (<see cref="CodeLocation"/>'s order: the editor's own document first, then by name, then by place, each
    /// place once). They are all asked at once, and the answer waits for the last. One that throws is reported
    /// through <see cref="Failed"/> and counts as answering nothing; a cancelled request throws, as a cancelled
    /// task does.
    /// </summary>
    public async Task<IReadOnlyList<CodeLocation>> RequestAsync(CodePosition position, CancellationToken cancellation)
    {
        var document = _editor.Document;
        var asked = new List<Task<IReadOnlyList<CodeLocation>>>();
        foreach (var provider in Providers) asked.Add(AskOne(provider, document, position, cancellation));
        var answers = new List<IReadOnlyList<CodeLocation>>();
        foreach (var one in asked) answers.Add(await one);
        cancellation.ThrowIfCancellationRequested();
        return CodeLocation.Joined(answers);
    }

    private async Task<IReadOnlyList<CodeLocation>> AskOne(ICodeReferenceProvider provider, CodeDocument document,
        CodePosition position, CancellationToken cancellation)
    {
        try
        {
            return await provider.ReferencesAsync(document, position, cancellation);
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
