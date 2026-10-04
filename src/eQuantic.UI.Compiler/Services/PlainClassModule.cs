using eQuantic.UI.Compiler.CodeGen.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.Services;

/// <summary>
/// Whether a class declaration is a PLAIN CLASS that gets a module of its own (#423): the one rule the
/// parser, which writes the module, and the dependency resolver, which decides who imports it, both
/// read. Two copies of it disagreed: the parser skipped a class with no member, and the resolver still
/// answered that such a class with a base list was a module, so `class Mute : IGreeting { }` was
/// imported by every module that constructed it and written by none, and the bundle could not resolve
/// it.
/// <para>
/// What a class DECLARES has nothing to do with it. A class that takes every member from its
/// interface's defaults, an empty class over a base, an abstract class with nothing in it: each is a
/// type the browser constructs, tests and extends, and C# gives each an identity of its own. The rule is
/// about what the class IS:
/// </para>
/// <list type="bullet">
/// <item>a NESTED class is its owner's scope, and a STATIC one is a static helper's module;</item>
/// <item><c>[RuntimeProvided]</c> says the runtime exports it, and <c>[ServerOnly]</c> that it never
/// crosses, and so does a class whose base stays on the server, since its twin could not extend it;</item>
/// <item>an ATTRIBUTE is metadata the browser never reads, and an EXCEPTION is thrown as the
/// JavaScript <c>Error</c> its construction lowers to, so neither has a base a twin could extend;</item>
/// <item>a PARTIAL declaration that declares nothing is not a module of its own: another declaration of
/// the type carries its members, and one module per declaration is what <see cref="EmittedTwins"/>
/// reports as divided.</item>
/// </list>
/// <para>
/// Decided from the SYNTAX, because the resolver scans files with no semantic model. The one fact that
/// lives in another file, whether a base stays on the server, is asked of the caller: the parser asks its
/// model, and the resolver the names its scan saw marked <c>[ServerOnly]</c>.
/// </para>
/// </summary>
internal static class PlainClassModule
{
    /// <param name="declaration">The class.</param>
    /// <param name="baseStaysOnServer">Whether the class's base, as written, is a type that never
    /// crosses (marked <c>[ServerOnly]</c>, or over one that is).</param>
    internal static bool Is(ClassDeclarationSyntax declaration, Func<TypeSyntax, bool> baseStaysOnServer)
    {
        if (declaration.Parent is TypeDeclarationSyntax) return false;
        if (declaration.Modifiers.Any(SyntaxKind.StaticKeyword)) return false;
        if (declaration.AttributeLists.SelectMany(list => list.Attributes)
            .Any(attribute => attribute.IsNamed("RuntimeProvided") || attribute.IsNamed("ServerOnly")))
            return false;
        if (declaration.Modifiers.Any(SyntaxKind.PartialKeyword) && declaration.Members.Count == 0) return false;
        if (IsException(declaration.Identifier.Text)) return false;
        if (declaration.BaseList?.Types.FirstOrDefault()?.Type is { } written)
        {
            var name = SimpleName(written);
            if (name == "Attribute" || name.EndsWith("Attribute", StringComparison.Ordinal)) return false;
            if (IsException(name)) return false;
            if (baseStaysOnServer(written)) return false;
        }
        return true;
    }

    /// <summary>The rule the construction and the import already follow: a type named for an
    /// exception is one, and lowers to JavaScript's <c>Error</c>.</summary>
    private static bool IsException(string name) => name.EndsWith("Exception", StringComparison.Ordinal);

    /// <summary>A type's name as written, without its namespace, its alias or its arguments.</summary>
    internal static string SimpleName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => SimpleName(qualified.Right),
        AliasQualifiedNameSyntax aliased => SimpleName(aliased.Name),
        GenericNameSyntax generic => generic.Identifier.Text,
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        _ => type.ToString(),
    };
}
