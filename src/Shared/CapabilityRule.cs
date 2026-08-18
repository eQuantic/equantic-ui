using Microsoft.CodeAnalysis;

namespace eQuantic.UI;

/// <summary>
/// What makes a constructor parameter a DEPENDENCY rather than a shape data arrives in.
///
/// <para>
/// Four places have to agree about this, and they live in two assemblies that cannot reference each
/// other: the parser (which takes the dependency OUT of the emitted constructor), the object-creation
/// strategy (which drops the argument standing in its place), the factory generator (which leaves it
/// out of the factory's signature and fills it from the scope), and the emitter (which decides
/// whether an absent capability is an error). They agreed by being written out four times, with a
/// comment in each saying they must — which is the shape of a rule that is about to drift.
/// </para>
/// <para>
/// A drift here does not fail: it MOVES ARGUMENTS. `new Quark(clock, mood, size)` becomes
/// `(mood = clock, size = 'happy')`, which is a type error in neither language and surfaces three
/// layers away as `dp.toFixed is not a function`. So the rule is one file, linked into both, and
/// answering it is not something any of them gets to do alone.
/// </para>
/// </summary>
internal static class CapabilityRule
{
    /// <summary>
    /// An interface from anywhere but System. The runtime's own interfaces are not dependencies:
    /// <c>IReadOnlyList&lt;AccordionItem&gt;</c> is how a component receives its items, and an
    /// Accordion resolving its rows from a container is nonsense — which is exactly what the first
    /// version of this rule did.
    /// </summary>
    public static bool IsDependency(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Interface
        && type.ContainingNamespace?.ToDisplayString() is { } space
        && !space.StartsWith("System", System.StringComparison.Ordinal);

    /// <summary>
    /// Whether the component says it cannot work without this one. `IClock clock` under an enabled
    /// nullable context is a promise the parameter is never null; `IClock? clock` is the author
    /// saying they handle the target that does not have it.
    /// <para>
    /// A file with nullable DISABLED gives no signal either way, and a missing capability there
    /// stays what it always was — null, handled by whoever wrote it. Reading silence as a demand
    /// would turn a working app into a throwing one on a compiler upgrade.
    /// </para>
    /// </summary>
    public static bool IsRequired(IParameterSymbol parameter) =>
        parameter.NullableAnnotation == NullableAnnotation.NotAnnotated;
}
