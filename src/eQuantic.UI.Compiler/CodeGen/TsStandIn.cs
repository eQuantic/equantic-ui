using Microsoft.CodeAnalysis;
using eQuantic.UI.Compiler.CodeGen.Strategies;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// What a TypeScript annotation writes for a type whose C# name names nothing there, asked of the
/// type's SYMBOL: an enum crosses as its members (the vocabulary's named union, a number when it is
/// [Flags], a string otherwise), an interface as <c>any</c> (no module is emitted for one), an
/// exception of .NET's as the <c>Error</c> it is in JavaScript (<c>utils/exceptions.ts</c>), and a
/// delegate as the function it is. An exception class of the app's is a class, whose twin its own name
/// is (#611).
/// <para>
/// ONE rule for every annotation the emitters write: a class's members and parameters, a record's, a
/// local's, a local function's parameters. Each path kept its own subset of it, so a record declared
/// <c>error: Exception</c>, a local <c>thing: IThing</c> and an empty list <c>Action[]</c>, names no
/// module defines, which the runtime's own type check refuses the day a twin writes one; the code
/// engine's completion was the first to write the exception and the interface (#296).
/// </para>
/// </summary>
internal static class TsStandIn
{
    /// <summary>
    /// The stand-in for <paramref name="type"/>, or null when its own name is its twin's: a class, a
    /// record, a struct. A nullable value type is not unwrapped here, since each caller writes its
    /// own null. A vocabulary union it answers is added to <paramref name="imports"/>, the runtime
    /// types the module imports, when there is such a set to add it to.
    /// </summary>
    public static string? For(ITypeSymbol type, ISet<string>? imports = null)
    {
        switch (type.TypeKind)
        {
            case TypeKind.Enum:
                // [Flags] members COMBINE, so they cross as the number the bitwise operators need.
                if (type.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == "FlagsAttribute"))
                    return "number";
                if (TypeScriptEmitter.VocabularyUnionFor(type) is not { } union) return "string";
                imports?.Add(union);
                return union;
            case TypeKind.Interface:
                return "any";
            case TypeKind.Delegate when type is INamedTypeSymbol { DelegateInvokeMethod: { } invoke }:
                return Function(invoke, imports);
            case TypeKind.Class when ExceptionTypes.Is(type) && !ExceptionTypes.HasTwin(type):
                return "Error";
            default:
                return null;
        }
    }

    /// <summary>The stand-in where it sits INSIDE another type, an array's element or a type
    /// argument: a function is parenthesised there, or <c>() =&gt; void[]</c> is a function that
    /// returns an array.</summary>
    public static string? Inside(ITypeSymbol type, ISet<string>? imports = null) =>
        For(type, imports) is { } standIn ? Parenthesised(standIn) : null;

    /// <summary><paramref name="type"/>, parenthesised when it is a function.</summary>
    public static string Parenthesised(string type) => type.Contains("=>") ? $"({type})" : type;

    /// <summary>A delegate's signature: each of its types named where this can name it, and
    /// <c>any</c> where it cannot, since a name guessed is a type nothing declares.</summary>
    private static string Function(IMethodSymbol invoke, ISet<string>? imports)
    {
        var parameters = invoke.Parameters.Select(parameter =>
            $"{parameter.Name.ToJsIdentifier()}: {Named(parameter.Type, imports)}");
        var returns = invoke.ReturnsVoid ? "void" : Named(invoke.ReturnType, imports);
        return $"({string.Join(", ", parameters)}) => {returns}";
    }

    private static string Named(ITypeSymbol type, ISet<string>? imports) =>
        type.SpecialType != SpecialType.None
            ? TypeScriptEmitter.CSharpTypeToTypeScript(type.ToDisplayString())
            : For(type, imports) ?? "any";
}
