namespace eQuantic.UI.Testing;

/// <summary>
/// What a served page did once its scripts ran against the server's markup (<see cref="HydrationAssert"/>):
/// the page the server drew and the one the browser's router chose, whether the markup was adopted, what
/// the browser drew differently, and what the page said in its console.
/// </summary>
public sealed record HydrationReport
{
    /// <summary>How many differences <see cref="Problems"/> names before it counts the rest.</summary>
    private const int DifferencesNamed = 10;

    /// <summary>The page's address.</summary>
    public required string Address { get; init; }

    /// <summary>Whether the page's boot got as far as writing what hydration did.</summary>
    public required bool Booted { get; init; }

    /// <summary>The page the server drew, by its module's name; null when the page never booted.</summary>
    public string? Page { get; init; }

    /// <summary>The page the browser's router chose for the address; null when it chose none.</summary>
    public string? RoutedPage { get; init; }

    /// <summary>Whether the browser adopted the server's markup rather than drawing over it.</summary>
    public bool Hydrated { get; init; }

    /// <summary>Why the browser drew over the server's markup, when it did.</summary>
    public IReadOnlyList<string> DrewOver { get; init; } = [];

    /// <summary>What hydration said about the markup it could not adopt.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>What the browser's tree said other than the server's markup.</summary>
    public IReadOnlyList<HydrationDifference> Differences { get; init; } = [];

    /// <summary>Everything the page wrote to its console as an error, an uncaught throw included.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>A string the page read that its catalogs do not hold, as the runtime said it.</summary>
    public IReadOnlyList<string> MissingStrings { get; init; } = [];

    /// <summary>Each thing that went wrong, as a sentence, the most telling first; empty when the page hydrated
    /// as the server drew it.</summary>
    public IReadOnlyList<string> Problems
    {
        get
        {
            var problems = new List<string>();
            if (!Booted)
                problems.Add("the page never got as far as hydrating: its scripts did not load or its boot did not finish");
            problems.AddRange(Errors.Select(error => $"the page reported an error: {error}"));
            if (Page is not null && RoutedPage is not null && Page != RoutedPage)
                problems.Add($"the server drew {Page}, the browser's router chose {RoutedPage} for {new Uri(Address).AbsolutePath}: "
                    + $"a navigation there shows {RoutedPage} with what the server loads for {Page}");
            if (Booted && !Hydrated)
                problems.Add($"the browser drew over the server's markup ({string.Join("; ", DrewOver.Concat(Warnings))})");
            problems.AddRange(MissingStrings.Select(missing => $"a string is missing: {missing}"));
            problems.AddRange(Differences.Take(DifferencesNamed).Select(difference => difference.ToString()));
            if (Differences.Count > DifferencesNamed)
                problems.Add($"and {Differences.Count - DifferencesNamed} more differences");
            return problems;
        }
    }

    /// <summary>Whether the page hydrated as the server drew it.</summary>
    public bool Passed => Problems.Count == 0;

    /// <inheritdoc />
    public override string ToString() => Passed
        ? $"{Address} hydrated as the server drew it"
        : $"{Address}: {string.Join("; ", Problems)}";
}
