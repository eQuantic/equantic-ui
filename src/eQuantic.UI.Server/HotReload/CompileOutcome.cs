namespace eQuantic.UI.Server.HotReload;

/// <summary>
/// How a compile of the app's modules ended: whether it wrote them, and the lines that say why not,
/// in MSBuild's form (<c>file(line,col): error CODE: message</c>) where the compiler wrote them so.
/// </summary>
internal sealed record CompileOutcome(bool Compiled, IReadOnlyList<string> Errors)
{
    /// <summary>A compile that wrote the modules.</summary>
    public static CompileOutcome Done { get; } = new(true, []);

    /// <summary>A compile that did not run to its end, for the reason given.</summary>
    public static CompileOutcome NotRun(string why) => new(false, [why]);

    /// <summary>No compile can run for this host: its project is not built with eQuantic.UI.Sdk, so there is no
    /// eqc to start, which the server has said once already (#1757). A save has nothing to tell the page.</summary>
    public static CompileOutcome Off { get; } = new(false, []) { Unavailable = true };

    /// <summary>Whether no compile can run for this host at all, rather than this one having failed.</summary>
    public bool Unavailable { get; init; }
}
