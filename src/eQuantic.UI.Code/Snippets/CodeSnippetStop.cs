namespace eQuantic.UI.Code;

/// <summary>
/// One stop of an inserted snippet (#1013): its number, every place it is written in every copy, the place
/// the primary caret takes first, the stop whose placeholder it is nested in, its options when it is a
/// choice, and whether it is the final stop, or one dropped with a placeholder typed over.
/// </summary>
internal sealed class CodeSnippetStop
{
    public CodeSnippetStop(int index, int parent, bool final)
    {
        Index = index;
        Parent = parent;
        Final = final;
    }

    public int Index { get; }

    /// <summary>The stop whose placeholder holds this one's first place, or -1.</summary>
    public int Parent { get; }

    /// <summary>The final stop: <c>$0</c>, or the end of every copy of a snippet with none.</summary>
    public bool Final { get; }

    /// <summary>A choice's options, from the place that wrote them; null for any other stop.</summary>
    public IReadOnlyList<string>? Choices { get; set; }

    /// <summary>Every place the stop is written, the primary caret's first and the rest in document order.</summary>
    public List<CodeRange> Ranges { get; } = [];

    /// <summary>Whether the primary caret's place is first among <see cref="Ranges"/> yet.</summary>
    public bool HasPrimary { get; set; }

    /// <summary>Whether the stop was dropped, nested in a placeholder that was typed over.</summary>
    public bool Dropped { get; set; }
}
