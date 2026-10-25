namespace eQuantic.UI.Code;

/// <summary>
/// A snippet: text with tab stops, written in the Language Server Protocol's snippet syntax, which VS Code's
/// snippets and every language server's completion items are written in (#1013).
/// <para>
/// <c>$1</c> and <c>${1}</c> are a tab stop, <c>${1:name}</c> a placeholder, whose text may hold further stops,
/// <c>${1|one,two|}</c> a choice, and <c>$0</c> the final stop; a stop written twice is a mirror, typed with it.
/// <c>$TM_SELECTED_TEXT</c>, <c>$TM_CURRENT_LINE</c>, <c>$TM_CURRENT_WORD</c>, <c>$TM_LINE_INDEX</c> and
/// <c>$TM_LINE_NUMBER</c> are variables, <c>${NAME:default}</c> one with a default, and an unknown one is a
/// placeholder holding its name. A backslash escapes <c>$</c>, <c>}</c> and itself, and in a choice's options
/// <c>,</c> and <c>|</c> too. Whatever the syntax does not make is text, so parsing never fails.
/// </para>
/// <para>
/// A completion entry inserts one through <see cref="CodeCompletionItem.Snippet"/>, and an app through
/// <see cref="CodeEditorController.InsertSnippet"/>; the session that walks its stops is
/// <see cref="CodeEditorController.Snippet"/>.
/// </para>
/// </summary>
public sealed class CodeSnippet
{
    private CodeSnippet(string body, IReadOnlyList<CodeSnippetPart> parts)
    {
        Body = body;
        Parts = parts;
    }

    /// <summary>The snippet as it was written.</summary>
    public string Body { get; }

    /// <summary>What the body reads as.</summary>
    internal IReadOnlyList<CodeSnippetPart> Parts { get; }

    /// <summary>Reads <paramref name="body"/>, which never fails: what is not syntax is text.</summary>
    public static CodeSnippet Parse(string body) => new(body, new CodeSnippetParser(body).Parse());

    /// <summary>The body, as written.</summary>
    public override string ToString() => Body;
}
