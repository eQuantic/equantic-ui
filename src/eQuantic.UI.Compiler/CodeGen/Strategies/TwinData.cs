using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A value the browser holds as DATA (<c>[TwinIsData]</c>, <c>Color</c>), written out: a plain
/// object of the members it stores, each under its twin's name, in their order. A construction and
/// a default both build one here, so the two cannot disagree about the shape.
/// </summary>
internal static class TwinData
{
    /// <param name="data">A type <see cref="TypeSymbolExtensions.TwinIsData"/> answers yes for.</param>
    /// <param name="valueOf">A member's value as converted JavaScript, or null where nothing sets it.</param>
    /// <param name="zeroOf">The JavaScript default of a member's type, for a member nothing sets.</param>
    public static string Literal(INamedTypeSymbol data, Func<ISymbol, string?> valueOf, Func<ITypeSymbol, string> zeroOf) =>
        "{ " + string.Join(", ", data.DataMembers().Select(member =>
            $"{TwinName.Of(member.Name)}: {valueOf(member) ?? zeroOf(TypeOf(member))}")) + " }";

    private static ITypeSymbol TypeOf(ISymbol member) =>
        member is IFieldSymbol field ? field.Type : ((IPropertySymbol)member).Type;
}
