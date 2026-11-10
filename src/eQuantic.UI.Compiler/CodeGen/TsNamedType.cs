using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The name a C# type has in TypeScript, where it has one to say: a named vocabulary or user type.
/// Primitives, generics and arrays keep their C# spellings, which are not TypeScript's, and inference is
/// already right for them. The one rule a local's annotation, a local function's return, a temporary's
/// declaration and a list's item read.
/// </summary>
internal static class TsNamedType
{
    /// <summary><paramref name="type"/>'s name, <c>any</c> for what crosses as nothing a module names, or
    /// null where it has none to say. An interface, an exception and a delegate are named by what they
    /// cross as (<see cref="TsStandIn"/>): <c>IThing thing = new Thing()</c> declared an <c>IThing</c> no
    /// module defines.</summary>
    internal static string? For(ITypeSymbol? type, ConversionContext context) =>
        type is INamedTypeSymbol { IsGenericType: false, SpecialType: SpecialType.None } named
            ? TsStandIn.For(named, context.UsedRuntimeTypes) ?? Named(named, context)
            : null;

    /// <summary>
    /// The TS spelling of a named type that is neither a primitive nor generic. A type with a twin, the
    /// app's own or one the runtime provides, is named by its twin and imported as any reference to it is:
    /// an app class only a tuple's slot named was named and never imported. A task held as a value is a
    /// promise, where the mapper's <c>void</c> is what a method returning one is awaited to. Any other type
    /// the app does not declare is named as the mapper spells it where a module names that with no module
    /// of the app's behind it (a Guid is a string, a TimeSpan the runtime's), and null where nothing can
    /// verify the name, which leaves it to inference: a local function returning a Guid was written
    /// <c>(): Guid</c>, a name no module has (Copilot's reviews of #1068 found the first ones, the review
    /// of #793 the return).
    /// </summary>
    internal static string? Named(INamedTypeSymbol named, ConversionContext context)
    {
        if (named.Locations.Any(location => location.IsInSource) || named.IsRuntimeProvided())
            return named.IntroduceTwin(context);
        if (named is { Name: "Task", Arity: 0 } && named.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks")
            return "Promise<void>";
        var ts = TypeScriptEmitter.CSharpTypeToTypeScript(named.Name);
        return TypeScriptEmitter.NamesWithoutAModule(ts) ? ts : null;
    }
}
