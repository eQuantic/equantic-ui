using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The overrides that take the place of a member a twin writes itself. A record's or a struct's twin
/// writes its <c>equals</c>, its <c>getHashCode</c> and its <c>toString</c> from its members, as C#
/// synthesizes them, and the runtime asks those names wherever .NET calls <c>Equals(object)</c>,
/// <c>GetHashCode()</c> and <c>ToString()</c>. An override of one of those is the member .NET calls
/// there, so the twin writes its body in place of its own (#1146). The record emitter writes by this
/// and EQ1007 reads it, so a member the build lets stand on one of those names is the one the twin
/// holds there: an overload (<c>ToString(string)</c>) took the name too, and either answered every
/// concatenation of the value or lost to the twin's own.
/// </summary>
internal static class TwinOwnMembers
{
    /// <summary>
    /// Whether <paramref name="method"/> overrides object's member <paramref name="name"/>: an instance
    /// override whose chain of overridden methods ends at <c>object</c>'s, asked of the model, and of the
    /// syntax where there is none, one parameter of type <c>object</c> for <c>Equals</c> and none for the
    /// others. An override of a typed <c>Equals</c> a base declares (<c>Equals(int)</c>) fills that one's
    /// slot, which holds a name of its own (<see cref="TwinMethodName.TypedEquals"/>), so it takes nothing
    /// of the twin's <c>equals</c>: the twin of a derived record wrote none for it, and its values compared
    /// as its base's. A <c>[ServerOnly]</c> override is no member of the twin, so it takes nobody's place
    /// there, and the twin writes its own.
    /// </summary>
    internal static bool Overrides(MethodDeclarationSyntax method, string name, SemanticModel? model)
    {
        if (method.Identifier.ValueText != name
            || !method.Modifiers.Any(SyntaxKind.OverrideKeyword)
            || method.Modifiers.Any(SyntaxKind.StaticKeyword)
            || Parser.ComponentParser.StaysOnTheServer(method, inAComponent: false))
            return false;
        if (model?.SyntaxTree == method.SyntaxTree && model.GetDeclaredSymbol(method) is IMethodSymbol symbol)
        {
            var root = symbol.OriginalDefinition;
            while (root.OverriddenMethod is { } overridden) root = overridden.OriginalDefinition;
            // An override at the top of the chain overrides a base the model could not bind, and the syntax
            // says what it overrides.
            if (!root.IsOverride) return root.ContainingType.SpecialType == SpecialType.System_Object;
        }
        var parameters = method.ParameterList.Parameters;
        return name == nameof(Equals)
            ? parameters.Count == 1 && TakesAnObject(parameters[0])
            : parameters.Count == 0;
    }

    /// <summary>Whether <paramref name="type"/> overrides object's member <paramref name="name"/>.</summary>
    internal static bool OverriddenIn(TypeDeclarationSyntax type, string name, SemanticModel? model) =>
        type.Members.OfType<MethodDeclarationSyntax>().Any(method => Overrides(method, name, model));

    /// <summary>A parameter written <c>object</c> or <c>object?</c>, as the syntax alone can tell.</summary>
    private static bool TakesAnObject(ParameterSyntax parameter) =>
        (parameter.Type is NullableTypeSyntax nullable ? nullable.ElementType : parameter.Type)
            is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.ObjectKeyword);
}
