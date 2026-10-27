namespace eQuantic.UI.Code;

/// <summary>
/// A place in a document (#1084), LSP's <c>Location</c>: a range, and the document it is in. A place in the
/// document the editor shows has no <see cref="Document"/>; one in another carries the name its language service
/// gives that document (a path, a URI), and going to it hands it to the app, which owns its documents
/// (<see cref="CodeEditorController.OpenRequested"/>).
/// </summary>
public sealed record CodeLocation(CodeRange Range)
{
    /// <summary>The document the place is in, as its language service names it, or null for the one the editor
    /// shows.</summary>
    public string? Document { get; init; }

    /// <summary>
    /// The answers of every provider as one list, as VS Code's <c>ReferencesModel</c> makes it: ordered by
    /// document, the editor's own first and the others by name, ordinally, then by where they start and where
    /// they end, a place equal to the one before it dropped.
    /// </summary>
    internal static IReadOnlyList<CodeLocation> Joined(IEnumerable<IReadOnlyList<CodeLocation>> answers)
    {
        var places = new List<CodeLocation>();
        foreach (var answer in answers) places.AddRange(answer);
        // Two places the comparison calls equal are the same place, and one of them is dropped, so the sort's
        // stability (JavaScript's is stable, .NET's is not) decides nothing.
        places.Sort((a, b) => Compare(a, b));
        var joined = new List<CodeLocation>();
        foreach (var place in places)
            if (joined.Count == 0 || Compare(joined[^1], place) != 0) joined.Add(place);
        return joined;
    }

    private static int Compare(CodeLocation a, CodeLocation b)
    {
        if (a.Document != b.Document)
        {
            if (a.Document is null) return -1;
            if (b.Document is null) return 1;
            var named = string.CompareOrdinal(a.Document, b.Document);
            if (named != 0) return named;
        }
        var start = a.Range.Start.CompareTo(b.Range.Start);
        return start != 0 ? start : a.Range.End.CompareTo(b.Range.End);
    }
}
