using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// An event's <c>add</c> and <c>remove</c> accessors on its twin (#746). C# compiles <c>e.Changed += h</c>
/// into a call of the event's add accessor: a field-like event's is written by the compiler and composes the
/// event's invocation list, and an event declared with accessors brings its own. The twin of a field-like
/// event keeps that list under the event's name, as it always did, and an event declared with accessors is
/// written as two methods, under <c>$add$</c> and <c>$remove$</c> and the event's twin name, which no C#
/// member can take. A subscription calls what the event it binds has (<see cref="Of"/>).
/// </summary>
internal static class EventAccessors
{
    /// <summary>How a subscription reaches an event's accessors.</summary>
    internal enum Kind
    {
        /// <summary>A field-like event: its invocation list, held under its name, is composed in place.</summary>
        FieldLike,

        /// <summary>An event the source declares with accessors and nothing overrides: its methods are called.</summary>
        Declared,

        /// <summary>An interface's, an abstract or a virtual event, whose implementation may be either: the
        /// runtime calls the accessors the twin wrote, where it wrote them, and composes the list otherwise.</summary>
        EitherAtRunTime,
    }

    /// <summary>The name the twin writes the <paramref name="verb"/> accessor (<c>add</c> or <c>remove</c>)
    /// of the event it calls <paramref name="twinName"/> under.</summary>
    internal static string Name(string verb, string twinName) => "$" + verb + "$" + twinName;

    /// <summary>How a subscription to <paramref name="symbol"/> reaches its accessors.</summary>
    internal static Kind Of(IEventSymbol symbol)
    {
        if (symbol.ContainingType?.TypeKind == TypeKind.Interface || symbol.IsAbstract || symbol.IsVirtual || symbol.IsOverride)
            return Kind.EitherAtRunTime;
        return symbol.OriginalDefinition.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is EventDeclarationSyntax)
            ? Kind.Declared
            : Kind.FieldLike;
    }
}
