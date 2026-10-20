namespace eQuantic.UI.Code;

/// <summary>
/// A request made of ONE provider for an open list, and what cancels it. A list holds its providers'
/// newest requests, answered or not: asking a provider again cancels its own request and no other, so the
/// answer a slow provider still owes lands beside the one asked again (#620), and closing the list cancels
/// them all, an answered one included, as a provider that registered on its token expects.
/// </summary>
internal sealed class CodeCompletionRequest
{
    public CodeCompletionRequest(ICodeCompletionProvider provider, string word)
    {
        Provider = provider;
        Word = word;
    }

    public ICodeCompletionProvider Provider { get; }

    /// <summary>The word the provider is asked for.</summary>
    public string Word { get; }

    public CancellationTokenSource Cancellation { get; } = new();

    /// <summary>Whether its answer has been taken.</summary>
    public bool Answered { get; set; }
}
