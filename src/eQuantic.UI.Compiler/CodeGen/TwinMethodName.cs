using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen;

/// <summary>
/// The name a method holds on its type's twin, which its declaration is written under and every call,
/// method group and base call the model binds to it reaches it by (#563).
/// <para>
/// A JavaScript class chain has one member per name, and a method on a derived class's prototype IS
/// the override of its base's method of that name. So a method that HIDES an inherited member, with
/// <c>new</c> or with the same signature and no <c>override</c>, could not hold the name it is written
/// with: <c>A a = new B(); a.Name()</c> ran B's in the browser, where .NET runs A's. Where the base was
/// declared in the app the build refused it (EQ1007), legal C# and all, and where it was not, the
/// runtime met it through the slot it asks: <c>public new int GetHashCode()</c> answered every hash of
/// the object, a set's and <c>((object)c).GetHashCode()</c> included, where .NET answers object's.
/// </para>
/// <para>
/// So a method that hides a member holds a name of its own: the name the hidden member holds, with
/// <c>$</c> and how many hidden slots of that name it stands over (<c>name$1</c>, and <c>name$2</c> for
/// one that hides that one in turn). No C# name holds a <c>$</c>, so no member the author declares can
/// meet it, and the hidden member keeps its name, which every call bound to it, and the runtime, reach.
/// An override holds the name of the method it overrides, whatever that one holds, so an override of a
/// <c>new virtual</c> method fills that method's slot; every other method holds its own name.
/// </para>
/// <para>
/// The same holds beside an explicit interface implementation of the same name (<see cref="ExplicitBeside"/>):
/// the explicit one holds the interface member's name, which every call through the interface reaches,
/// and the method of the type's own holds a name of its own. C# lets the two answer differently, and a
/// <c>foreach</c> over the class binds the public <c>GetEnumerator()</c> where LINQ binds the interface's.
/// </para>
/// <para>
/// And so does an <c>Equals</c> that is no <c>Equals(object)</c> (<see cref="TypedEquals"/>): the twin's
/// <c>equals</c> is the slot the runtime asks for <c>Equals(object)</c>, so a typed overload under that name
/// answered <c>object.Equals(new C(), new C())</c>, which .NET answers by reference (Copilot's second review
/// of #947). Every call through an interface finds it by the contract it binds, which the twin's
/// <c>static $equatable</c> maps to the method, and each holds a name of its own (<see cref="TypedEqualsName"/>).
/// </para>
/// </summary>
internal static class TwinMethodName
{
    /// <summary>The name <paramref name="method"/> holds on its type's twin.</summary>
    public static string Of(IMethodSymbol method)
    {
        var slot = Slot(method);
        if (TypedEquals(slot)) return TypedEqualsName(slot);
        if (Hidden(slot) is { } hidden) return Next(NameOf(hidden));
        var own = SimpleName(slot).ToCamelCase();
        return ExplicitBeside(slot) is not null ? Next(own) : own;
    }

    /// <summary>
    /// The name a typed <c>Equals</c> holds: <c>equals$</c> and its place along the class chain, counted from
    /// the top, the typed ones each base declares before the type's own, its explicit implementations first and
    /// then its own methods, each in the order they are declared. So no two meet on one name: a method beside
    /// an explicit <c>IEquatable&lt;C&gt;.Equals</c> held <c>equals$1</c> with it, and JavaScript kept the later
    /// (Copilot's third review of #947), and two overloads, or a base's and its derived class's, were refused
    /// on one name (EQ1007). An override fills the slot it overrides and takes no place. Calls through an
    /// interface reach these methods by the contract they bind (<c>static $equatable</c>), never by the name.
    /// </summary>
    private static string TypedEqualsName(IMethodSymbol slot)
    {
        var above = 0;
        for (var at = slot.ContainingType.BaseType; at is not null; at = at.BaseType)
            above += NumberedTypedEquals(at).Count;
        var own = NumberedTypedEquals(slot.ContainingType);
        return $"equals${above + own.FindIndex(member => SymbolEqualityComparer.Default.Equals(member, slot)) + 1}";
    }

    /// <summary>The typed <c>Equals</c> a type declares that take a place of their own (<see cref="TypedEqualsName"/>):
    /// every one that is no override, its explicit implementations first, each in the order they are declared.</summary>
    private static List<IMethodSymbol> NumberedTypedEquals(INamedTypeSymbol type) =>
        type.GetMembers().OfType<IMethodSymbol>()
            .Where(member => !member.IsOverride && TypedEquals(member))
            .OrderBy(member => member.MethodKind == MethodKind.ExplicitInterfaceImplementation ? 0 : 1)
            .ThenBy(member => member.Locations.FirstOrDefault()?.SourceTree?.FilePath, StringComparer.Ordinal)
            .ThenBy(member => member.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0)
            .ToList();

    /// <summary>The name a declared method holds, asked of its symbol where the model knows it, and its
    /// own name camelCased where none does.</summary>
    public static string Of(MethodDeclarationSyntax declaration, SemanticModel? model) =>
        model?.GetDeclaredSymbol(declaration) is IMethodSymbol method
            ? Of(method)
            : declaration.Identifier.ValueText.ToCamelCase();

    /// <summary>The name a call of <paramref name="written"/> reaches: the bound method's twin name, and
    /// the written name camelCased where nothing is bound.</summary>
    public static string Of(IMethodSymbol? method, string written) =>
        method is { MethodKind: MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation }
            ? Of(method)
            : written.ToCamelCase();

    /// <summary>Whether <paramref name="method"/> holds a name of its own on its twin, because it hides a
    /// member, stands beside an explicit implementation of its name (<see cref="ExplicitBeside"/>), or fills
    /// the slot of one that does: a call bound to it is the app's own method, whatever the name it is
    /// written with means to the runtime (<c>ToString</c>, <c>GetHashCode</c>).</summary>
    public static bool HoldsANameOfItsOwn(IMethodSymbol method)
    {
        var slot = Slot(method);
        return Hidden(slot) is not null || ExplicitBeside(slot) is not null || TypedEquals(slot);
    }

    /// <summary>
    /// Whether <paramref name="slot"/> is an <c>Equals</c> of one parameter that is no <c>object</c>, an instance
    /// method this compilation declares on a class, a struct or an interface: a typed overload, an
    /// <c>IEquatable&lt;T&gt;</c> implementation and an interface's own <c>Equals</c> among them, which C# never
    /// asks for <c>Equals(object)</c>. Each holds a name of its own (<see cref="TypedEqualsName"/>), even where it
    /// hides another, and a call through the interface reaches it by the contract it binds.
    /// </summary>
    public static bool TypedEquals(IMethodSymbol slot) =>
        slot is { MethodKind: MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation, IsStatic: false,
            IsImplicitlyDeclared: false, Parameters.Length: 1 }
        && SimpleName(slot) == "Equals"
        && slot.Parameters[0].Type.SpecialType != SpecialType.System_Object
        && slot.ContainingType is { TypeKind: TypeKind.Class or TypeKind.Struct or TypeKind.Interface, IsExtension: false }
        && slot.Locations.Any(location => location.IsInSource);

    /// <summary>
    /// The explicit interface implementation that <paramref name="method"/> shares its name with in its
    /// type, or null: one the twin writes (<see cref="ExplicitImplementations.Written"/>), on the same side of
    /// the twin, under the name of the interface member it implements. That one holds the name, which every
    /// call through the interface reaches, so the method holds a name of its own beside it. They were written
    /// under one name and the later replaced the other: a class whose public <c>GetEnumerator()</c> and
    /// explicit <c>IEnumerable&lt;T&gt;.GetEnumerator()</c> answer differently, which C# allows, walked one
    /// sequence for a <c>foreach</c> over the class and for LINQ alike (Copilot's review of #708). One that
    /// hands back what the method answers is not written, and leaves it the name (#1139). Only a method this
    /// compilation declares can be given a name of its own, as with one that hides a member. An override is
    /// asked of the slot it fills, whose name it holds.
    /// </summary>
    public static IMethodSymbol? ExplicitBeside(IMethodSymbol method)
    {
        method = Slot(method);
        if (method.MethodKind != MethodKind.Ordinary || !method.Locations.Any(location => location.IsInSource)
            || method.ContainingType is not { TypeKind: TypeKind.Class or TypeKind.Struct, IsExtension: false } type)
            return null;
        return ExplicitImplementations.Written(type, SimpleName(method).ToCamelCase(), method.IsStatic)
            .OfType<IMethodSymbol>().FirstOrDefault();
    }

    /// <summary>
    /// The member <paramref name="method"/> hides, as C# decides it, or null: the nearest base that
    /// declares a member of its name it inherits, a method of the same signature or any member that is no
    /// method, on the same side of the twin (an instance member on the prototype, a static one on the
    /// class). An override hides nothing, and nor does a member of an interface, an extension block, or
    /// one that is no ordinary method (an explicit implementation is named after its interface's member).
    /// Only a method eqc writes into a twin, one this compilation declares, can be given a name of its
    /// own: a type the runtime carries is reached by the names its twin was written with.
    /// </summary>
    public static ISymbol? Hidden(IMethodSymbol method)
    {
        method = method.OriginalDefinition;
        if (method.MethodKind != MethodKind.Ordinary || method.IsOverride
            || !method.Locations.Any(location => location.IsInSource)
            || method.ContainingType is not { TypeKind: TypeKind.Class or TypeKind.Struct, IsExtension: false } type)
            return null;
        for (var at = type.BaseType; at is not null; at = at.BaseType)
        {
            foreach (var member in at.GetMembers(method.Name))
            {
                if (member.IsStatic != method.IsStatic || member.IsImplicitlyDeclared || !Inherits(type, member)) continue;
                switch (member)
                {
                    case IMethodSymbol { MethodKind: MethodKind.Ordinary } other when SameSignature(method, other):
                        return other;
                    case IFieldSymbol or IPropertySymbol { IsIndexer: false } or IEventSymbol:
                        return member;
                }
            }
        }
        return null;
    }

    /// <summary>The method whose slot <paramref name="method"/> fills: itself, or, for an override, the
    /// method it overrides, followed until one is no override.</summary>
    public static IMethodSymbol Slot(IMethodSymbol method)
    {
        var at = method.OriginalDefinition;
        while (at.IsOverride && at.OverriddenMethod is { } overridden) at = overridden.OriginalDefinition;
        return at;
    }

    /// <summary>The name a hidden member holds: a method's twin name, and a field's, a property's or an
    /// event's own name, which its twin reads it by.</summary>
    private static string NameOf(ISymbol hidden) =>
        hidden is IMethodSymbol method ? Of(method) : hidden.Name.ToCamelCase();

    /// <summary>The name one more hidden slot along: <c>name</c> is <c>name$1</c>, and <c>name$1</c> is
    /// <c>name$2</c>.</summary>
    private static string Next(string held)
    {
        var mark = held.LastIndexOf('$');
        return mark > 0 && int.TryParse(held[(mark + 1)..], out var count)
            ? $"{held[..mark]}${count + 1}"
            : held + "$1";
    }

    /// <summary>A method's own name: an explicit implementation's symbol is named after its interface
    /// (<c>System.Collections.IEnumerable.GetEnumerator</c>), and lowers under the member's name.</summary>
    private static string SimpleName(IMethodSymbol method) =>
        method.MethodKind == MethodKind.ExplicitInterfaceImplementation
            && method.ExplicitInterfaceImplementations is [var implemented, ..]
            ? implemented.Name
            : method.Name;

    /// <summary>Whether <paramref name="type"/> inherits <paramref name="member"/>: a private member is its
    /// declaring type's alone, and an internal one is inherited inside its own assembly.</summary>
    private static bool Inherits(INamedTypeSymbol type, ISymbol member) => member.DeclaredAccessibility switch
    {
        Accessibility.Private => false,
        Accessibility.Internal or Accessibility.ProtectedAndInternal =>
            SymbolEqualityComparer.Default.Equals(member.ContainingAssembly, type.ContainingAssembly),
        _ => true,
    };

    /// <summary>Whether two methods have the signature C# hides by: as many type parameters, and the same
    /// parameters, each of the same type, by value or by reference alike. Each method declares its own type
    /// parameters, so the other's are read as this one's, by position: <c>F&lt;U&gt;(List&lt;U&gt;)</c> hides
    /// <c>F&lt;T&gt;(List&lt;T&gt;)</c>, where comparing a bare type parameter alone saw two signatures and
    /// left the method that hides one refused (EQ1007).</summary>
    private static bool SameSignature(IMethodSymbol method, IMethodSymbol other)
    {
        if (method.Arity != other.Arity || method.Parameters.Length != other.Parameters.Length) return false;
        if (method.Arity > 0) other = other.Construct([.. method.TypeParameters]);
        for (var i = 0; i < method.Parameters.Length; i++)
        {
            var (mine, theirs) = (method.Parameters[i], other.Parameters[i]);
            if ((mine.RefKind == RefKind.None) != (theirs.RefKind == RefKind.None)) return false;
            if (!SymbolEqualityComparer.Default.Equals(mine.Type, theirs.Type)) return false;
        }
        return true;
    }
}
