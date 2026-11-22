using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Conformance.Tests.Infrastructure;

/// <summary>
/// A metadata resolver that answers each reference once per process and hands every later script the
/// same <see cref="PortableExecutableReference"/> (#1816). Scripting's own resolver answered every
/// script's compilation anew: the framework assemblies a compilation finds missing were read again into
/// native buffers that only a finalizer gives back. The GC never saw them, so a run of the suite
/// reached a 22 GB working set over a 1.2 GB managed heap and killed a 16 GB CI runner mid-run.
/// </summary>
internal sealed class ReferencesResolvedOnce(MetadataReferenceResolver inner) : MetadataReferenceResolver
{
    private readonly ConcurrentDictionary<(string Reference, string? BaseFilePath, MetadataReferenceProperties Properties),
        ImmutableArray<PortableExecutableReference>> _references = new();

    private readonly ConcurrentDictionary<string, PortableExecutableReference?> _missing = new();

    public override bool ResolveMissingAssemblies => inner.ResolveMissingAssemblies;

    public override ImmutableArray<PortableExecutableReference> ResolveReference(string reference, string? baseFilePath,
        MetadataReferenceProperties properties) =>
        _references.GetOrAdd((reference, baseFilePath, properties),
            key => inner.ResolveReference(key.Reference, key.BaseFilePath, key.Properties));

    public override PortableExecutableReference? ResolveMissingAssembly(MetadataReference definition,
        AssemblyIdentity referenceIdentity) =>
        _missing.GetOrAdd(referenceIdentity.GetDisplayName(), _ => inner.ResolveMissingAssembly(definition, referenceIdentity));

    public override bool Equals(object? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
}
