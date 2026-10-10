using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The time zone the local-time cases run in, one at a time: the cases change it for the whole process,
/// so they must not overlap anything else. Collections that disable parallelization run after every
/// parallel one has finished.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalTimeZoneCollection
{
    public const string Name = "Local time zone";
}
