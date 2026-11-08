using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The name a C# type has in TypeScript, where it has one to say: a named vocabulary or user type.
/// Primitives, generics and arrays keep their C# spellings, which are not TypeScript's, and inference is
/// already right for them. The one rule a local's annotation, a local function's return and a temporary's
/// declaration read.
/// </summary>
internal static class TsNamedType
{
    /// <summary><paramref name="type"/>'s name, <c>any</c> for what crosses as nothing a module names, or
    /// null where it has none to say. An interface, an exception and a delegate are named by what they
    /// cross as (<see cref="TsStandIn"/>): <c>IThing thing = new Thing()</c> declared an <c>IThing</c> no
    /// module defines.</summary>
    internal static string? For(ITypeSymbol? type, ConversionContext context) =>
        type is INamedTypeSymbol { IsGenericType: false, SpecialType: SpecialType.None } named
            ? TsStandIn.For(named, context.UsedRuntimeTypes) ?? named.NestedTwinName() ?? named.Name
            : null;
}
