global using eQuantic.UI.Tests;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Tests;

/// <summary>
/// The metadata references every test compilation takes: ONE per assembly for the whole test process.
/// <c>MetadataReference.CreateFromFile</c> copies the assembly into native memory, which the GC does not
/// count and frees only when the reference is finalized, so suites that made new ones for every
/// compilation held tens of gigabytes per test host: 29 GB for the compiler suite and 32.6 GB for the
/// conformance suite, measured on a 48 GB machine (#481). A <c>PortableExecutableReference</c> is
/// immutable and meant to be shared, and a shared one also spares Roslyn building the assembly's
/// symbols again for every compilation.
/// <para>
/// Linked into each test project that compiles C#. <c>TestReferencesGuardTests</c> fails on a
/// <c>CreateFromFile</c> anywhere else in the tests, naming this owner, so the copy does not come back
/// one test at a time.
/// </para>
/// </summary>
internal static class TestReferences
{
    /// <summary>One reference per path. A <see cref="Lazy{T}"/> per entry, because two tests asking for
    /// the same assembly at once may both run <c>GetOrAdd</c>'s factory: the loser's wrapper is
    /// dropped, and only the winner's <c>Value</c> ever reads the file.</summary>
    private static readonly ConcurrentDictionary<string, Lazy<MetadataReference>> Cache = new(StringComparer.Ordinal);

    /// <summary>The assembly at <paramref name="path"/>, read once.</summary>
    public static MetadataReference Of(string path) =>
        Cache.GetOrAdd(path, static file => new Lazy<MetadataReference>(() => MetadataReference.CreateFromFile(file))).Value;

    /// <summary>The assembly that declares <paramref name="type"/>.</summary>
    public static MetadataReference Of(Type type) => Of(type.Assembly.Location);

    /// <summary>The assembly itself.</summary>
    public static MetadataReference Of(Assembly assembly) => Of(assembly.Location);
}
