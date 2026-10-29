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
    /// override of that name and arity, one parameter for <c>Equals</c> and none for the others. A typed
    /// <c>Equals</c> holds a name of its own (<see cref="TwinMethodName.TypedEquals"/>), so the one that
    /// reaches the twin's <c>equals</c> is <c>Equals(object)</c>. A <c>[ServerOnly]</c> override is no
    /// member of the twin, so it takes nobody's place there, and the twin writes its own.
    /// </summary>
    internal static bool Overrides(MethodDeclarationSyntax method, string name) =>
        method.Identifier.ValueText == name
        && method.Modifiers.Any(SyntaxKind.OverrideKeyword)
        && !method.Modifiers.Any(SyntaxKind.StaticKeyword)
        && method.ParameterList.Parameters.Count == (name == nameof(Equals) ? 1 : 0)
        && !Parser.ComponentParser.StaysOnTheServer(method, inAComponent: false);

    /// <summary>Whether <paramref name="type"/> overrides object's member <paramref name="name"/>.</summary>
    internal static bool OverriddenIn(TypeDeclarationSyntax type, string name) =>
        type.Members.OfType<MethodDeclarationSyntax>().Any(method => Overrides(method, name));
}
