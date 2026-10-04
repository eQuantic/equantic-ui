using eQuantic.UI.Compiler.CodeGen;
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
/// crosses;</item>
/// <item>a PARTIAL declaration that declares nothing is not a module of its own: another declaration of
/// the type carries its members, and one module per declaration is what <see cref="EmittedTwins"/>
/// reports as divided;</item>
/// <item>and a class over a base that keeps it out has none: an ATTRIBUTE is metadata the browser never
/// reads, an EXCEPTION is built as the JavaScript <c>Error</c> its construction lowers to, and a class
/// whose base stays on the server could not extend it. Each is a fact of the whole CHAIN of bases, not
/// of the base the class names: <c>class Retry : Failure</c> over <c>class Failure : Exception</c> is an
/// exception too, and was given a module extending one nothing wrote.</item>
/// </list>
/// <para>
/// Decided from the SYNTAX, because the resolver scans files with no semantic model. The chain lives
/// in other declarations, maybe in other files, so it is asked of the caller: the parser asks its
/// model, and the resolver walks the classes its scan saw.
/// </para>
/// </summary>
internal static class PlainClassModule
{
    /// <param name="declaration">The class.</param>
    /// <param name="baseKeepsItOut">Whether the class's base, as written, keeps it out: the base, or a
    /// base of its own, is an attribute, an exception, or a type marked <c>[ServerOnly]</c>.</param>
    internal static bool Is(ClassDeclarationSyntax declaration, Func<TypeSyntax, bool> baseKeepsItOut)
    {
        if (declaration.Parent is TypeDeclarationSyntax) return false;
        if (declaration.Modifiers.Any(SyntaxKind.StaticKeyword)) return false;
        if (declaration.AttributeLists.SelectMany(list => list.Attributes)
            .Any(attribute => attribute.IsNamed("RuntimeProvided") || attribute.IsNamed("ServerOnly")))
            return false;
        if (declaration.Modifiers.Any(SyntaxKind.PartialKeyword) && declaration.Members.Count == 0) return false;
        return declaration.BaseList?.Types.FirstOrDefault()?.Type is not { } written || !baseKeepsItOut(written);
    }

    /// <summary>
    /// Whether a type that no declaration in sight declares, known by its name alone, keeps a class over
    /// it out: an attribute or an exception of .NET, named so by the convention every one of them
    /// follows. Asked only at the end of a chain the caller could not walk further.
    /// </summary>
    internal static bool KeepsOutByName(string name) =>
        name is "Attribute" or "Exception"
        || name.EndsWith("Attribute", StringComparison.Ordinal)
        || name.EndsWith("Exception", StringComparison.Ordinal);
}
