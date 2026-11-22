using System.Collections.Concurrent;
using System.Collections.Immutable;
using eQuantic.UI.Conformance.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Xunit;

namespace eQuantic.UI.Conformance.Tests;

/// <summary>
/// The .NET side of every case is a script, and a script's compilation resolves the framework
/// assemblies it finds missing. Scripting's own resolver read each one again for every script, into
/// native memory the GC never saw. A run of the suite reached a 22 GB working set over a 1.2 GB managed
/// heap and killed the 16 GB CI runner mid-run (#1816). Each reference is now resolved once per
/// process.
/// </summary>
public class ScriptReferencesTests
{
    /// <summary>Scripting's resolver, counting what it is asked to resolve, by assembly.</summary>
    private sealed class Counting(MetadataReferenceResolver inner) : MetadataReferenceResolver
    {
        public ConcurrentDictionary<string, int> Missing { get; } = new();

        public override bool ResolveMissingAssemblies => inner.ResolveMissingAssemblies;

        public override ImmutableArray<PortableExecutableReference> ResolveReference(string reference, string? baseFilePath,
            MetadataReferenceProperties properties) => inner.ResolveReference(reference, baseFilePath, properties);

        public override PortableExecutableReference? ResolveMissingAssembly(MetadataReference definition,
            AssemblyIdentity referenceIdentity)
        {
            Missing.AddOrUpdate(referenceIdentity.GetDisplayName(), 1, (_, count) => count + 1);
            return inner.ResolveMissingAssembly(definition, referenceIdentity);
        }

        public override bool Equals(object? other) => ReferenceEquals(this, other);

        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }

    [Fact]
    public void TheEvaluatorsScripts_ResolveThroughOneResolverForTheProcess()
    {
        DotNetEvaluator.Options.MetadataResolver.Should().BeOfType<ReferencesResolvedOnce>(
            "every script of the suite shares what one resolution read");
    }

    [Fact]
    public async Task ScriptsThatNeedTheSameAssemblies_ResolveEachOfThemOnce()
    {
        var counting = new Counting(ScriptOptions.Default.MetadataResolver);
        var options = DotNetEvaluator.Options.WithMetadataResolver(new ReferencesResolvedOnce(counting));

        (await CSharpScript.EvaluateAsync<int>("new[] { 1, 2 }.Sum()", options)).Should().Be(3);
        (await CSharpScript.EvaluateAsync<int>("new List<int> { 3, 4 }.Sum()", options)).Should().Be(7);
        (await CSharpScript.EvaluateAsync<string>("string.Join(\",\", new[] { \"a\", \"b\" })", options)).Should().Be("a,b");

        counting.Missing.Should().NotBeEmpty("a script's compilation resolves the framework's facades, or this proves nothing");
        counting.Missing.Where(entry => entry.Value > 1).Should().BeEmpty(
            "an assembly read once is handed to every later script, never read again");
    }
}
