namespace eQuantic.UI.Testing;

/// <summary>
/// A page that does not hydrate as the server drew it (<see cref="HydrationAssert"/>): its message says
/// each thing that went wrong, the first one first, and <see cref="Report"/> holds all of it.
/// </summary>
public sealed class HydrationException : Exception
{
    /// <summary>The failure, described by its report.</summary>
    /// <param name="report">What the page did.</param>
    public HydrationException(HydrationReport report)
        : base($"{report.Address} does not hydrate as the server drew it:{Environment.NewLine}"
               + string.Join(Environment.NewLine, report.Problems.Select(problem => $"- {problem}")))
    {
        Report = report;
    }

    /// <summary>What the page did.</summary>
    public HydrationReport Report { get; }
}
