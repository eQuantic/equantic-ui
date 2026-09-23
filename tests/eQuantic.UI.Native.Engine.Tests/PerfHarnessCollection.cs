using Xunit;

namespace eQuantic.UI.Native.Engine.Tests;

/// <summary>
/// The perf harness runs ALONE. xunit runs test classes side by side, and a ruler of bytes per
/// frame measured next to fourteen hundred other tests measures some of them too: the per-layer
/// ruler read 580 bytes in two full-suite runs and 556 in every other run, alone or not, on the
/// same binaries (#290). The time alarm reads the same shared machine.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerfHarnessCollection
{
    public const string Name = "Perf harness";
}
