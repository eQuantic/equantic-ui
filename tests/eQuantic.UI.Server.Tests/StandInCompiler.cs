using eQuantic.UI.Server.HotReload;

namespace eQuantic.UI.Server.Tests;

/// <summary>A compiler of the app's modules that answers as the test says, and counts its disposals.</summary>
internal sealed class StandInCompiler(Func<TimeSpan, CompileOutcome> compile) : IModuleCompiler
{
    /// <summary>One that compiles at once.</summary>
    public static StandInCompiler Compiling() => new(_ => CompileOutcome.Done);

    public int Disposals { get; private set; }

    public void Prepare()
    {
    }

    public CompileOutcome Compile(TimeSpan limit) => compile(limit);

    public void Dispose() => Disposals++;
}
