using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

/// <summary>
/// The slot a field lives in on its twin: an instance field of a plain class on each instance, and a
/// static field on its class (see the last paragraph). A JavaScript object
/// has one member per name, and a C# class may hold a field and a property or a method a case apart,
/// the shape a C# class has most: <c>int value;</c> beside <c>int Value { get => value; set =>
/// this.value = value * 2; }</c> lowered both to <c>value</c>, the own field hid the accessors, and the
/// setter never ran (#396). The field is the one that moves, storage the class's own code names, where
/// a property's or a method's name is what callers and JSON read: it takes a <c>$</c> after its name,
/// which no C# name holds and which a property's own store, <c>$name</c>, never meets. Of two fields of
/// one type a case apart, the one the casing changed moves.
/// <para>
/// A slot an ancestor already holds is TAKEN, whatever the spellings: its twin's constructor writes it
/// before the derived one runs, so a derived field on the same slot wrote over the inherited one, and
/// two C# fields were one. The inherited slot is fixed by its own type, so the derived field moves, a
/// <c>$</c> more each time the slot it would take is held: <c>value</c> beside an inherited field
/// <c>Value</c> moved to <c>value$</c>, and beside an inherited <c>value$</c> to <c>value$$</c> (Copilot's
/// second review of #696).
/// </para>
/// <para>
/// A member a DERIVED type declares a case apart moves the field as its own type's does, however far
/// down the chain: the base's twin writes the field on each instance, where it hides the getter or the
/// method the derived twin puts on its prototype, so <c>Amount =&gt; 99</c> answered the base's
/// <c>amount</c> (#740). The derived types are the ones the field's own source declares, which is all a
/// build of that source can read, so every compilation that reaches the field writes it in one slot.
/// </para>
/// <para>
/// A component's fields, which the server hydrates by name, and a record's or a struct's, which
/// equality, printing and <c>with</c> read, keep the refusal they have (EQ1007): renaming them would
/// have to reach the hydration manifest and the value semantics too.
/// </para>
/// <para>
/// A STATIC field moves the same way, beside the statics of its type and of its chain, a constant's
/// included (#1175): the backing-field idiom, <c>private static int count;</c> beside <c>public static
/// int Count</c>, wrote both as <c>static count</c>, the field was defined over the accessor when the
/// class was, and the build refused what it would have written. A static is the class's, read by
/// nothing that hydrates, compares, prints or copies an instance, so it moves in every type eqc writes
/// a twin of, a component, a record and a struct included. Statics and instance members never meet,
/// one living on the class and the other on its prototype, so each is moved past its own kind; the
/// chain counts for statics too, since a class inherits its base's statics through its constructor
/// (<c>Derived.count</c> reaches <c>Base.count</c> unless <c>Derived</c> holds one of its own).
/// </para>
/// </summary>
internal static class FieldSlotExtensions
{
    /// <summary>The name <paramref name="field"/> is written under in its twin.</summary>
    internal static string TwinSlot(this IFieldSymbol field)
    {
        var name = TwinName.Of(field.Name);
        if (!Movable(field)) return name;
        var slot = MovesInItsType(field, name) || HeldByDescendants(field.ContainingType, field.IsStatic).Contains(name)
            ? name + "$"
            : name;
        var taken = HeldByAncestors(field.ContainingType.BaseType, field.IsStatic);
        while (taken.Contains(slot)) slot += "$";
        return slot;
    }

    /// <summary>
    /// The field of a base declared in ANOTHER project that <paramref name="member"/> lands on, or null.
    /// A field moves aside for a member a type of its own source declares below it (#740), but the twin of
    /// a base another project declares is written by that project's build, which never reads the types
    /// derived from it here: its field stays on the name the member takes and hides it on each instance.
    /// The build refuses the member (EQ1007) rather than writing a twin whose member answers the field.
    /// </summary>
    internal static IFieldSymbol? FieldItLandsOn(this ISymbol member)
    {
        if (member.IsImplicitlyDeclared || !HoldsAName(member) || member.ContainingType is not { } type) return null;
        var name = member.MemberTwinName();
        for (var holder = type.BaseType; holder is not null && holder.SpecialType != SpecialType.System_Object; holder = holder.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(holder.ContainingAssembly, type.ContainingAssembly)) continue;
            foreach (var field in holder.GetMembers().OfType<IFieldSymbol>())
                if (field.IsStatic == member.IsStatic && Movable(field) && field.TwinSlot() == name) return field;
        }
        return null;
    }

    /// <summary>The name a field's variable is written under: its slot where the model binds it
    /// (<see cref="TwinSlot"/>), its twin name where there is no model to say where it moved.</summary>
    internal static string SlotOf(this VariableDeclaratorSyntax variable, SemanticModel? model) =>
        model?.SyntaxTree == variable.SyntaxTree && model.GetDeclaredSymbol(variable) is IFieldSymbol field
            ? field.TwinSlot()
            : TwinName.Of(variable.Identifier.ValueText);

    /// <summary>The name a member access writes for <paramref name="name"/>: a field's slot when the
    /// model binds it to one, its twin name otherwise.</summary>
    internal static string MemberSlot(this SimpleNameSyntax name, ConversionContext context) =>
        context.SemanticHelper.GetSymbol(name) is IFieldSymbol field
            ? field.TwinSlot()
            : name.Identifier.ValueText.ToCamelCase();

    /// <summary>A field of a type the source declares, whose twin eqc writes by this same rule (a
    /// framework type's twin is the runtime's, and names its members as they are): an instance field of
    /// a plain class, and a static of a class, a record or a struct, a constant's included (#1175).</summary>
    private static bool Movable(IFieldSymbol field) =>
        !field.IsImplicitlyDeclared
        && field.ContainingType is { TypeKind: TypeKind.Class or TypeKind.Struct } type
        && type.Locations.Any(location => location.IsInSource)
        && (field.IsStatic || (type is { TypeKind: TypeKind.Class, IsRecord: false } && !IsComponent(type)));

    /// <summary>Whether a member of the field's own type holds its twin name: a property, an event or a
    /// method, an explicit implementation's included under the name of the member it implements
    /// (<see cref="MemberTwinNameExtensions.MemberTwinName"/>), or another field, of which the one whose
    /// name the casing changed moves.</summary>
    private static bool MovesInItsType(IFieldSymbol field, string name) =>
        field.ContainingType.GetMembers().Any(member =>
            !SymbolEqualityComparer.Default.Equals(member, field) && member.IsStatic == field.IsStatic
            && !member.IsImplicitlyDeclared
            && member switch
            {
                IFieldSymbol other => TwinName.Of(other.Name) == name && field.Name != name,
                _ => HoldsAName(member) && member.MemberTwinName() == name,
            });

    /// <summary>The names the twins of <paramref name="type"/> and its bases hold on each instance, or on
    /// the class where <paramref name="statics"/>: every property's, event's and method's twin name, an
    /// explicit implementation's included, and every field's slot, which may have moved itself.</summary>
    private static HashSet<string> HeldByAncestors(INamedTypeSymbol? type, bool statics)
    {
        var held = new HashSet<string>(StringComparer.Ordinal);
        for (var holder = type; holder is not null && holder.SpecialType != SpecialType.System_Object; holder = holder.BaseType)
        {
            foreach (var member in holder.GetMembers())
            {
                if (member.IsStatic != statics || member.IsImplicitlyDeclared) continue;
                if (member is IFieldSymbol || HoldsAName(member)) held.Add(member.MemberTwinName());
            }
        }
        return held;
    }

    /// <summary>The names the twins of the types derived from <paramref name="type"/> hold on their
    /// prototypes, or on their classes where <paramref name="statics"/>: every property's, event's and
    /// method's twin name that a type of its own source declares below it. A derived field is not one: it
    /// moves past the slots it inherits itself.</summary>
    private static IReadOnlySet<string> HeldByDescendants(INamedTypeSymbol type, bool statics) =>
        Descendants.GetValue(type.ContainingAssembly, IndexDescendants)
            .TryGetValue((type.OriginalDefinition, statics), out var held)
            ? held
            : NoNames;

    private static readonly IReadOnlySet<string> NoNames = new HashSet<string>();

    /// <summary>Each source assembly's index of <see cref="HeldByDescendants"/>, read once.</summary>
    private static readonly ConditionalWeakTable<IAssemblySymbol, Dictionary<(INamedTypeSymbol, bool), HashSet<string>>> Descendants = new();

    private static Dictionary<(INamedTypeSymbol, bool), HashSet<string>> IndexDescendants(IAssemblySymbol assembly)
    {
        var index = new Dictionary<(INamedTypeSymbol, bool), HashSet<string>>(Keys.Instance);
        foreach (var type in TypesIn(assembly.GlobalNamespace))
        {
            foreach (var named in type.GetMembers().Where(member => !member.IsImplicitlyDeclared && HoldsAName(member))
                         .GroupBy(member => member.IsStatic))
            {
                var names = named.Select(member => member.MemberTwinName()).ToList();
                for (var ancestor = type.BaseType;
                     ancestor is not null && SymbolEqualityComparer.Default.Equals(ancestor.ContainingAssembly, assembly);
                     ancestor = ancestor.BaseType)
                {
                    if (!index.TryGetValue((ancestor.OriginalDefinition, named.Key), out var held))
                        index[(ancestor.OriginalDefinition, named.Key)] = held = new HashSet<string>(StringComparer.Ordinal);
                    held.UnionWith(names);
                }
            }
        }
        return index;
    }

    /// <summary>A type and whether the names are its statics, compared by the type's symbol.</summary>
    private sealed class Keys : IEqualityComparer<(INamedTypeSymbol Type, bool Statics)>
    {
        public static readonly Keys Instance = new();

        public bool Equals((INamedTypeSymbol Type, bool Statics) x, (INamedTypeSymbol Type, bool Statics) y) =>
            x.Statics == y.Statics && SymbolEqualityComparer.Default.Equals(x.Type, y.Type);

        public int GetHashCode((INamedTypeSymbol Type, bool Statics) key) =>
            HashCode.Combine(SymbolEqualityComparer.Default.GetHashCode(key.Type), key.Statics);
    }

    /// <summary>Every type a namespace declares, its nested ones and those of its namespaces included.</summary>
    private static IEnumerable<INamedTypeSymbol> TypesIn(INamespaceOrTypeSymbol container)
    {
        foreach (var member in container.GetMembers())
        {
            if (member is not INamespaceOrTypeSymbol inner) continue;
            if (inner is INamedTypeSymbol type) yield return type;
            foreach (var nested in TypesIn(inner)) yield return nested;
        }
    }

    /// <summary>A member a twin holds under a name on each instance besides its fields: a property, an
    /// event, or a method its class declares, an explicit implementation of an interface's included.</summary>
    private static bool HoldsAName(ISymbol member) =>
        member is IPropertySymbol or IEventSymbol
            or IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation };

    /// <summary>A component, whose fields the server hydrates under their twin names.</summary>
    private static bool IsComponent(INamedTypeSymbol type)
    {
        for (var holder = type; holder is not null; holder = holder.BaseType)
            if (holder.Name is "StatelessComponent" or "StatefulComponent" or "UiComponent") return true;
        return false;
    }
}
