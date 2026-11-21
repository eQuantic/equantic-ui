using Xunit;

namespace eQuantic.UI.Testing.Tests;

/// <summary>The notices are a process-wide channel, so the tests that read it run alone.</summary>
[CollectionDefinition(nameof(DevelopmentNoticesCollection), DisableParallelization = true)]
public sealed class DevelopmentNoticesCollection;
