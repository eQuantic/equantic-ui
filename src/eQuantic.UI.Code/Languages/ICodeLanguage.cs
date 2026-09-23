namespace eQuantic.UI.Code;

/// <summary>
/// A LANGUAGE, as far as colouring is concerned: it reads one line at a time and hands back the
/// spans, carrying an opaque state across the line break.
/// <para>
/// Line-at-a-time with carry-over state is the shape every real editor uses, and the reason is
/// incremental re-colouring: when line 40 changes, only line 40 re-tokenizes — and its successors
/// only if the carried state came out different. A block comment opened on line 40 is exactly that
/// case, and it is why the state exists at all.
/// </para>
/// </summary>
public interface ICodeLanguage
{
    /// <summary>What this language is called, for a picker or a caption.</summary>
    string Name { get; }

    /// <summary>
    /// Appends the tokens of <paramref name="line"/> to <paramref name="into"/> and returns the
    /// state the NEXT line starts in. <paramref name="state"/> is 0 at the top of a document and
    /// otherwise whatever this method last returned — its meaning belongs to the language.
    /// </summary>
    int Tokenize(string line, int state, List<CodeToken> into);

    /// <summary>
    /// How this language COMMENTS, INDENTS and PAIRS — everything the editor's behaviours are
    /// built from. Defaulted, so a language that only wants colours writes nothing.
    /// </summary>
    CodeLanguageRules Rules => CodeLanguageRules.Default;
}
