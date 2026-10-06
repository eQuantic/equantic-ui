using eQuantic.UI.Compiler.CodeGen;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Strategies;
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
/// whose base stays on the server could not extend it. Each is a fact of the whole CHAIN of base
/// CLASSES, not of the name the class's first base has: <c>class Retry : Failure</c> over
/// <c>class Failure : Exception</c> is an exception too, and was given a module extending one nothing
/// wrote, while <c>class ColorAttribute : IProductAttribute</c> derives from no attribute at all, an
/// interface in a base list being no base class.</item>
/// </list>
/// <para>
/// The chain is asked of the SYMBOL wherever the host has the project's compilation, which sees every
/// declaration of the app and every assembly it references: the parser asks its model, and the
/// resolver the same compilation, so the two read one answer by construction. A base from a library
/// is judged as what it is there, whatever its name says: <c>ColorAttribute : ProductAttribute</c>
/// over a library's plain class is a class, and <c>OrderFailed : DomainError</c> over a library's
/// exception is an exception. Only a host with no compilation at all walks the chain by NAME, through
/// the <see cref="Scan"/> of every file it read, and judges by its name only a base the scan never saw
/// declared (<see cref="KeepsOutByName"/>); the parser of such a host reads the resolver's scan, which
/// reaches across files where its own file does not.
/// </para>
/// </summary>
internal static class PlainClassModule
{
    /// <summary>
    /// What the rule reads of one class declaration, kept by a caller that answers after reading every
    /// file (the resolver), so no syntax tree has to outlive its scan.
    /// </summary>
    /// <param name="Name">The class's simple name.</param>
    /// <param name="OnItsOwn">Whether the declaration, by itself, takes a module: it is not static, and
    /// neither it nor a type it is nested in is marked <c>[RuntimeProvided]</c> or <c>[ServerOnly]</c>.</param>
    /// <param name="Base">The base it writes first, by the name its twin has; null for none.</param>
    /// <param name="Partial">Whether it is one declaration of a partial type, whose base another of its
    /// declarations may write.</param>
    /// <param name="Empty">Whether it is a partial declaration that declares nothing, which is no module
    /// of its own when another declaration of its type carries the members.</param>
    /// <param name="Module">The name its module and its twin take: its own, after the types it is nested
    /// in, joined by <c>$</c> (<c>Cart$Item</c>, #584).</param>
    internal readonly record struct Declared(string Name, bool OnItsOwn, string? Base, bool Partial, bool Empty, string Module)
    {
        internal static Declared Of(ClassDeclarationSyntax declaration) =>
            new(declaration.Identifier.ValueText, IsOnItsOwn(declaration), WrittenBase(declaration),
                declaration.Modifiers.Any(SyntaxKind.PartialKeyword),
                declaration.Modifiers.Any(SyntaxKind.PartialKeyword) && declaration.Members.Count == 0,
                declaration.TwinTypeName());
    }

    /// <summary>Whether <paramref name="declaration"/> gets a plain-class module.</summary>
    /// <param name="declaration">The class.</param>
    /// <param name="symbol">Its type in the project's compilation, or null for a host that has none, which
    /// then walks the chain through <paramref name="scan"/> by name.</param>
    /// <param name="scan">What the scan saw of the app's declarations, by name: the chain of a host with no
    /// compilation, and of a base no compilation in hand can bind.</param>
    internal static bool Is(ClassDeclarationSyntax declaration, INamedTypeSymbol? symbol, Scan scan) =>
        Is(Declared.Of(declaration), symbol, scan);

    /// <inheritdoc cref="Is(ClassDeclarationSyntax, INamedTypeSymbol?, Scan)"/>
    /// <remarks>
    /// A partial declaration that declares nothing is no module of its own only beside another
    /// declaration of its type: <c>partial</c> asks for no second one, and a lone
    /// <c>public partial class Hollow { }</c> is constructed in C#, where the browser got no module for
    /// <c>new Hollow()</c> (found by Copilot's review of #608). The type's symbol counts its
    /// declarations; a host with no compilation counts the scan's, by name.
    /// </remarks>
    internal static bool Is(Declared declared, INamedTypeSymbol? symbol, Scan scan) =>
        declared.OnItsOwn
        && !(declared.Empty && (symbol is not null ? symbol.DeclaringSyntaxReferences.Length > 1 : scan.DeclaredMoreThanOnce(declared.Name)))
        && !(symbol is not null ? KeptOut(symbol, scan) : scan.KeepsOut(declared));

    /// <summary>What the declaration says by itself: nested, static, or marked <c>[RuntimeProvided]</c> or
    /// <c>[ServerOnly]</c>.</summary>
    private static bool IsOnItsOwn(ClassDeclarationSyntax declaration) =>
        !declaration.Modifiers.Any(SyntaxKind.StaticKeyword)
        && !Marked(declaration)
        && OwnersCross(declaration);

    /// <summary>Marked <c>[RuntimeProvided]</c> or <c>[ServerOnly]</c>.</summary>
    private static bool Marked(MemberDeclarationSyntax declaration) =>
        IsServerOnlyDeclaration(declaration)
        || declaration.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => attribute.IsNamed("RuntimeProvided"));

    /// <summary>Marked <c>[ServerOnly]</c> where it is declared, which the parser writes no module for, as
    /// it reads it.</summary>
    internal static bool IsServerOnlyDeclaration(MemberDeclarationSyntax declaration) =>
        declaration.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => attribute.IsNamed("ServerOnly"));

    /// <summary>
    /// Whether no type a declaration is nested in is marked <c>[RuntimeProvided]</c> or
    /// <c>[ServerOnly]</c>: a nested type is a module of its own (#584), where its owner crosses. One
    /// inside a server-only class never crosses, as its owner never does, and one inside a type the
    /// runtime provides is the runtime's.
    /// </summary>
    internal static bool OwnersCross(BaseTypeDeclarationSyntax declaration)
    {
        for (var owner = declaration.Parent as BaseTypeDeclarationSyntax; owner is not null; owner = owner.Parent as BaseTypeDeclarationSyntax)
            if (Marked(owner)) return false;
        return true;
    }

    /// <summary>
    /// Whether the type's own declarations, or its chain of base CLASSES, keep it out: the type or a
    /// class it derives from is marked <c>[ServerOnly]</c> (on any of its partial declarations), or it
    /// derives from <c>System.Attribute</c> or <c>System.Exception</c>. An interface is never on the
    /// chain. A base the compilation cannot bind is an error type, known by its name alone, and the
    /// walk goes on through the scan from that name.
    /// </summary>
    private static bool KeptOut(INamedTypeSymbol type, Scan scan)
    {
        // A nested class crosses only where its owner does (#584): one inside a server-only class, an
        // exception or an attribute has none, as its owner has none.
        if (type.ContainingType is { } owner && KeptOut(owner, scan)) return true;
        for (var at = type; at is not null; at = at.BaseType)
        {
            if (at.TypeKind == TypeKind.Error) return scan.KeepsOutFrom(at.Name);
            if (IsServerOnly(at) || ExceptionTypes.IsRoot(at) || IsAttributeRoot(at)) return true;
        }
        return false;
    }

    /// <summary>
    /// Whether <paramref name="type"/> or a type it derives from is marked <c>[ServerOnly]</c>: the
    /// attribute is inherited, so a record over a server-only record is server-only too, as a class
    /// over a server-only class is (<see cref="KeptOut"/>), and the record emitter asks it of the
    /// whole chain (<see cref="CodeGen.RecordTypeEmitter.CanEmit"/>).
    /// </summary>
    internal static bool ServerOnlyAlongChain(INamedTypeSymbol type)
    {
        for (var at = type; at is not null; at = at.BaseType)
            if (IsServerOnly(at)) return true;
        return false;
    }

    /// <summary>
    /// Whether a type a nested record or struct sits in never crosses: it, or a type it derives from, is
    /// marked <c>[ServerOnly]</c>, or it is an exception or an attribute, which have no twin (#584). A
    /// nested record has a twin where its owner would have a module.
    /// </summary>
    internal static bool OwnerKeptOut(INamedTypeSymbol type)
    {
        for (var owner = type.ContainingType; owner is not null; owner = owner.ContainingType)
        {
            if (ServerOnlyAlongChain(owner)) return true;
            for (var at = owner; at is not null; at = at.BaseType)
                if (ExceptionTypes.IsRoot(at) || IsAttributeRoot(at)) return true;
        }
        return false;
    }

    /// <summary>Marked <c>[ServerOnly]</c>: by its full name, or by the name as written where the
    /// attribute binds to an error type, a compilation that does not reference it.</summary>
    private static bool IsServerOnly(INamedTypeSymbol type) =>
        type.GetAttributes().Any(attribute => attribute.AttributeClass?.Name is "ServerOnly" or "ServerOnlyAttribute");

    /// <summary>Whether <paramref name="type"/> is <c>System.Attribute</c> itself.</summary>
    private static bool IsAttributeRoot(INamedTypeSymbol type) =>
        type is { Name: "Attribute", ContainingType: null, Arity: 0 }
        && type.ContainingNamespace is { Name: "System", ContainingNamespace.IsGlobalNamespace: true };

    /// <summary>
    /// Whether a type that no declaration in sight declares, known by its name alone, keeps a class over
    /// it out: an attribute or an exception of .NET, named so by the convention every one of them
    /// follows. Asked only at the end of a chain the caller could not walk further, by a host with no
    /// compilation to ask.
    /// </summary>
    internal static bool KeepsOutByName(string name) =>
        name is "Attribute" or "Exception"
        || name.EndsWith("Attribute", StringComparison.Ordinal)
        || name.EndsWith("Exception", StringComparison.Ordinal);

    /// <summary>
    /// What a scan saw of the app's type declarations, by simple name, every file it read together: the
    /// base each class writes first, which classes are marked <c>[ServerOnly]</c>, and which names are
    /// declared as something that is no class (an interface, a struct, a record, an enum, a delegate).
    /// The chain a host with no compilation walks, and the only one such a host has.
    /// </summary>
    internal sealed class Scan
    {
        /// <summary>A class's first written base, by the name its twin has; null for one that writes
        /// none. A partial type is one entry: the first of its declarations that writes a base gives it.</summary>
        private readonly Dictionary<string, string?> _bases = new(StringComparer.Ordinal);

        private readonly HashSet<string> _serverOnly = new(StringComparer.Ordinal);

        private readonly HashSet<string> _notClasses = new(StringComparer.Ordinal);

        /// <summary>How many declarations of a class each name has, a partial type's halves counted apart.</summary>
        private readonly Dictionary<string, int> _declarations = new(StringComparer.Ordinal);

        /// <summary>The scan of one file's declarations, what a parser that was handed no resolver
        /// has.</summary>
        internal static Scan Of(SyntaxNode root)
        {
            var scan = new Scan();
            scan.Add(root);
            return scan;
        }

        /// <summary>Adds every type a file declares, a nested one included: a base can be written as
        /// one (<c>Outer.Inner</c>). A type is declared in a namespace or a type, never in a member's
        /// body, so the walk goes no deeper.</summary>
        internal void Add(SyntaxNode root)
        {
            foreach (var declaration in root
                         .DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax)
                         .OfType<MemberDeclarationSyntax>())
            {
                switch (declaration)
                {
                    case ClassDeclarationSyntax type:
                        var name = type.Identifier.ValueText;
                        _declarations[name] = _declarations.GetValueOrDefault(name) + 1;
                        var written = WrittenBase(type);
                        if (!_bases.TryGetValue(name, out var known) || known is null) _bases[name] = written;
                        if (type.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => attribute.IsNamed("ServerOnly")))
                            _serverOnly.Add(name);
                        break;
                    case BaseTypeDeclarationSyntax other:
                        _notClasses.Add(other.Identifier.ValueText);
                        break;
                    case DelegateDeclarationSyntax other:
                        _notClasses.Add(other.Identifier.ValueText);
                        break;
                }
            }
        }

        /// <summary>Whether the scan saw more than one declaration of a class of this name.</summary>
        internal bool DeclaredMoreThanOnce(string name) => _declarations.GetValueOrDefault(name) > 1;

        /// <summary>
        /// Whether a declaration is kept out, by name: a partial one by another declaration of its type
        /// marked <c>[ServerOnly]</c> (C# allows the attribute on one of them only), or any one by the
        /// chain from its base (<see cref="KeepsOutFrom"/>): the base it writes, or, for a partial one that
        /// writes none, the one another declaration of the type writes.
        /// </summary>
        internal bool KeepsOut(Declared declared) =>
            (declared.Partial && _serverOnly.Contains(declared.Name))
            || KeepsOutFrom(declared.Base
                ?? (declared.Partial && _bases.TryGetValue(declared.Name, out var other) ? other : null));

        /// <summary>
        /// Whether a chain of bases that starts at the one named <paramref name="name"/> keeps a class
        /// out: a class of the chain is marked <c>[ServerOnly]</c>, or the chain leaves the scan at a name
        /// <see cref="KeepsOutByName"/> judges. It follows only names the scan saw declared as classes,
        /// and ends, keeping nothing out, at one it saw declared as anything else: an interface in a base
        /// list is no base class, and `IProductAttribute` named no attribute.
        /// </summary>
        internal bool KeepsOutFrom(string? name)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (name is not null && seen.Add(name))
            {
                if (_serverOnly.Contains(name)) return true;
                if (!_bases.TryGetValue(name, out var next)) return !_notClasses.Contains(name) && KeepsOutByName(name);
                name = next;
            }
            return false;
        }
    }

    /// <summary>The base a class writes first, by the name its twin has; null for none.</summary>
    private static string? WrittenBase(ClassDeclarationSyntax declaration) =>
        declaration.BaseList?.Types.FirstOrDefault()?.Type is { } written ? written.TwinTypeName(model: null) : null;
}
