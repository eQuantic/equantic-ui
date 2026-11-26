using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// Where a static member of a static generic class is reached (#1855). C# keeps one set of statics per closed
/// type: <c>Cache&lt;int&gt;.Last</c> and <c>Cache&lt;string&gt;.Last</c> are two fields, each type initialized on its
/// own first use. The twin is one class for every type argument, so each closed type is an object the runtime
/// makes from the class once (<c>$eq.statics(Cache, 'int')</c>), and the class's static members, reached on it,
/// keep their state on it: a static's getter and setter, its type initializer and a static method read
/// <c>this</c>, which is the closed type they were reached through.
/// <para>
/// A static generic class only. A generic class that is not static keeps its statics on the class, where an
/// instance member reads them with no closed type in hand.
/// </para>
/// </summary>
internal static class ClosedStatics
{
    /// <summary>Whether <paramref name="type"/> is a static generic class whose twin the build writes, so its
    /// statics are kept per closed type.</summary>
    internal static bool KeepsPerClosure(this INamedTypeSymbol? type) =>
        type is { IsStatic: true, IsGenericType: true, TypeKind: TypeKind.Class }
        && (type.OriginalDefinition.Locations.Any(location => location.IsInSource) || type.IsRuntimeProvided());

    /// <summary>Whether the declaration <paramref name="type"/> is a static generic class, read from its syntax
    /// where its twin is written.</summary>
    internal static bool KeepsPerClosure(TypeDeclarationSyntax type) =>
        type is ClassDeclarationSyntax { TypeParameterList.Parameters.Count: > 0 } cls
        && cls.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword);

    /// <summary>The receiver its static members read: <c>this</c>, the closed type they were reached through.</summary>
    internal const string Own = "this";

    /// <summary>
    /// The receiver of a static member of <paramref name="declaring"/> written at <paramref name="at"/>, or null for
    /// a type whose statics are its class's own. Inside the class's own code, through its own type parameters, it
    /// is <c>this</c>. Through a closed type it is that closure's statics. Through a type argument known only at
    /// run time (<c>Cache&lt;U&gt;</c> in generic code), which no text names, it is refused (EQ2018).
    /// </summary>
    internal static JsExpr? Home(INamedTypeSymbol? declaring, SyntaxNode at, ConversionContext context)
    {
        if (!declaring.KeepsPerClosure()) return null;
        var definition = declaring!.OriginalDefinition;
        if (SymbolEqualityComparer.Default.Equals(declaring, definition) && WrittenIn(definition, at, context))
            return JsExpr.Identifier(Own);
        var twin = definition.Locations.Any(location => location.IsInSource)
            ? definition.IntroduceTwin(context)
            : definition.TwinReference();
        if (ObjectCreationStrategy.ClosureOf(declaring) is { } closure)
        {
            context.UsedHelpers.Add(Eq.Import);
            return JsExpr.Call(JsExpr.Identifier(Eq.Statics), JsExpr.Identifier(twin), JsExpr.Literal(JsStringLiteral.Quote(closure)));
        }
        context.Report(context.SemanticHelper.Original(at), ConversionSeverity.Error, "EQ2018",
            $"'{declaring.ToDisplayString()}' is a static generic class reached through a type argument known only at run "
            + "time. C# keeps one set of statics per closed type, and the browser carries no type arguments at run time, "
            + "so nothing can say which closed type's statics this is. Reach it through a closed type "
            + $"('{definition.Name}<int>'), or pass in what it holds.");
        return JsExpr.Identifier(twin);
    }

    /// <summary>Whether <paramref name="at"/> is code of <paramref name="definition"/>'s own declarations, not of a
    /// type declared inside them, whose code runs with no closed type of the outer one in hand.</summary>
    private static bool WrittenIn(INamedTypeSymbol definition, SyntaxNode at, ConversionContext context)
    {
        var written = context.SemanticHelper.Original(at);
        if (written.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } enclosing) return false;
        return definition.ReadDeclarations().Any(reference =>
            reference.SyntaxTree == enclosing.SyntaxTree && reference.Span == enclosing.Span);
    }
}
