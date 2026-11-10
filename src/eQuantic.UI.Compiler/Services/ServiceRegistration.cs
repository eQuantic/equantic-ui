using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// One registration the app wrote in its container, as the browser can repeat it: the interface it
/// is keyed by, the class that implements it, and how long one instance lives.
/// </summary>
/// <param name="Service">The interface, whose simple name is the key both sides agree on.</param>
/// <param name="Implementation">The class the container builds, declared in source the build reads,
/// or null when the registration hands the container something the browser cannot build: a factory
/// or an instance, which are server code, or a class from an assembly the build has no source for.</param>
/// <param name="Lifetime">The registry's word for the lifetime: <c>singleton</c>, <c>scoped</c> or
/// <c>transient</c>, as the runtime's <c>ServiceLifetime</c> spells it.</param>
/// <param name="OnlyIfAbsent">Whether it is a <c>TryAdd…</c>, which the container ignores when the
/// service is already registered.</param>
/// <param name="Written">Where the registration is written, for a message that has to point at it.</param>
internal sealed record ServiceRegistration(
    INamedTypeSymbol Service,
    INamedTypeSymbol? Implementation,
    string Lifetime,
    bool OnlyIfAbsent,
    Location Written)
{
    /// <summary>Whether the browser can build what this registration hands out.</summary>
    public bool Crosses => Implementation is not null;

    /// <summary>The registration as a reader finds it: the call's own text and its place.</summary>
    public string Describe()
    {
        var line = Written.GetLineSpan();
        var file = Path.GetFileName(line.Path);
        return $"{Written.SourceTree?.GetRoot().FindNode(Written.SourceSpan)} ({file}:{line.StartLinePosition.Line + 1})";
    }
}
