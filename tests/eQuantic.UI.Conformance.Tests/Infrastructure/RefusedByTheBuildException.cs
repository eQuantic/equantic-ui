namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// A case whose C# the build refuses, failed before anything runs. What the converter writes where it
/// refuses is a fallback (the source of a query whose clause it cannot lower, a member call left as
/// written), the build stops there, and no browser ever meets it: run as if it had compiled, it
/// answered something else and read as a translation bug, or matched .NET and read as a pass (#907).
/// </summary>
public sealed class RefusedByTheBuildException(IReadOnlyList<Refusal> refusals) : Exception(Describe(refusals))
{
    /// <summary>Every reason the build gave, in the order it gave them.</summary>
    public IReadOnlyList<Refusal> Refusals { get; } = refusals;

    private static string Describe(IReadOnlyList<Refusal> refusals) =>
        "The build refuses this case's C#, so nothing runs:\n"
        + string.Concat(refusals.Select(refusal => $"  {refusal}\n"))
        + "A case written for a fence says so, with the id it pins: "
        + "ConformanceRunner.AssertStatementsRefusedByTheBuild or AssertRefusedByTheBuild.";
}
