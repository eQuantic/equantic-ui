namespace eQuantic.UI.Code;

/// <summary>
/// One stop of an inserted snippet (#1013): its number, every place it is written in every copy with the
/// placeholders that place is nested in, the place the primary caret takes first, its options when it is a
/// choice, and whether it is the final stop, or one dropped with the placeholders it was written in.
/// </summary>
internal sealed class CodeSnippetStop
{
    public CodeSnippetStop(int index, bool final)
    {
        Index = index;
        Final = final;
    }

    public int Index { get; }

    /// <summary>The final stop: <c>$0</c>, or the end of every copy of a snippet with none.</summary>
    public bool Final { get; }

    /// <summary>A choice's options, from the place that wrote them; null for any other stop.</summary>
    public IReadOnlyList<string>? Choices { get; set; }

    /// <summary>Every place the stop is written, the primary caret's first and the rest in document order.</summary>
    public List<CodeRange> Ranges { get; } = [];

    /// <summary>The placeholders each of <see cref="Ranges"/> is nested in, place for place: a stop written
    /// both alone and in another's placeholder is nested in it at one place only (Copilot on #1079).</summary>
    public List<IReadOnlyList<int>> Enclosing { get; } = [];

    /// <summary>Whether the primary caret's place is first among <see cref="Ranges"/> yet.</summary>
    public bool HasPrimary { get; set; }

    /// <summary>Whether the stop was dropped, every place of it nested in placeholders that were typed over.</summary>
    public bool Dropped { get; set; }

    /// <summary>Adds a place, first when it is the primary caret's.</summary>
    public void Add(CodeRange range, IReadOnlyList<int> enclosing, bool first)
    {
        if (first)
        {
            Ranges.Insert(0, range);
            Enclosing.Insert(0, enclosing);
            HasPrimary = true;
            return;
        }
        Ranges.Add(range);
        Enclosing.Add(enclosing);
    }

    /// <summary>Takes away the place at <paramref name="index"/>.</summary>
    public void RemoveAt(int index)
    {
        Ranges.RemoveAt(index);
        Enclosing.RemoveAt(index);
    }

    /// <summary>Whether a place of the stop is nested in stop <paramref name="index"/>'s placeholder.</summary>
    public bool NestedIn(int index)
    {
        foreach (var enclosing in Enclosing)
            if (enclosing.Contains(index)) return true;
        return false;
    }
}
