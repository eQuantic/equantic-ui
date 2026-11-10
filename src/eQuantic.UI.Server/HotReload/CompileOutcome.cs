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
}
