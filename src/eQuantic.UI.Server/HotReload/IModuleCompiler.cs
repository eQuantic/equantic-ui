namespace eQuantic.UI.Server.HotReload;

/// <summary>
/// What compiles the app's modules again for a save: the eqc the server keeps running
/// (<see cref="RunningCompiler"/>), or a test's stand-in.
/// </summary>
internal interface IModuleCompiler : IDisposable
{
    /// <summary>Gets ready for the first save without compiling, while the app starts.</summary>
    void Prepare();

    /// <summary>Compiles the app's modules, and gives up after <paramref name="limit"/>.</summary>
    CompileOutcome Compile(TimeSpan limit);
}
