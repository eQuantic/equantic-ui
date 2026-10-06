using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// Tests that read or bind the PROCESS's UI dispatcher (<c>UiDispatcher.Current</c>) run alone.
/// Hosting a Photon app arms it for the process and leaves it armed, bound to whichever thread drew
/// last, so a test running beside one would see an answer it meant to apply at once posted to a frame
/// that never comes. Each of these puts back what it found.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessDispatcherCollection
{
    public const string Name = "Process dispatcher";
}
