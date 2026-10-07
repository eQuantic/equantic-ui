namespace eQuantic.UI.Code;

/// <summary>
/// Why a provider is asked: LSP's <c>CompletionTriggerKind</c>, with the case it folds into
/// <see cref="Invoked"/> told apart. A word STARTED by typing is where a provider such as Roslyn
/// answers differently from an explicit request, so an adapter that needs the difference gets it,
/// and an LSP client maps it back to Invoked.
/// </summary>
public enum CodeCompletionTrigger : byte
{
    /// <summary>Asked for: ⌃Space, or the app calling <see cref="CodeCompletion.Invoke"/>.</summary>
    Invoked = 0,

    /// <summary>A word started by typing while no list was open: the list a code editor opens as
    /// you type.</summary>
    Typing = 1,

    /// <summary>One of the provider's own trigger characters typed: the dot after a name.</summary>
    Character = 2,

    /// <summary>The word changed under an answer the provider said was incomplete.</summary>
    Incomplete = 3,
}
