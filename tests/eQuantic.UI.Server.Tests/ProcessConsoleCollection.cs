namespace eQuantic.UI.Server.Tests;

/// <summary>
/// Tests that redirect the PROCESS's console run alone: <c>Console.SetOut</c> is one writer for every
/// thread, so a test running beside one would write into its capture, or lose its own lines to it.
/// Each of these puts back the writer it found.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessConsoleCollection
{
    public const string Name = "Process console";
}
