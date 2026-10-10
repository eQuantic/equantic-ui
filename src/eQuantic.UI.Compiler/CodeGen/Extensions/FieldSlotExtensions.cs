using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

/// <summary>
/// The slot an instance field of a plain class lives in on each instance of its twin. A JavaScript object
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
/// second review of #696). A base never reads its derived types, so a derived member a case apart from
/// an inherited field is not this rule's to settle.
/// </para>
/// <para>
/// A component's fields, which the server hydrates by name, and a record's or a struct's, which
/// equality, printing and <c>with</c> read, keep the refusal they have (EQ1007): renaming them would
/// have to reach the hydration manifest and the value semantics too.
/// </para>
/// </summary>
internal static class FieldSlotExtensions
{
    /// <summary>The name <paramref name="field"/> is written under in its twin.</summary>
    internal static string TwinSlot(this IFieldSymbol field)
    {
        var name = TwinName.Of(field.Name);
        if (!Movable(field)) return name;
        var slot = MovesInItsType(field, name) ? name + "$" : name;
        var taken = HeldByAncestors(field.ContainingType.BaseType);
        while (taken.Contains(slot)) slot += "$";
        return slot;
    }

    /// <summary>The name a member access writes for <paramref name="name"/>: a field's slot when the
    /// model binds it to one, its twin name otherwise.</summary>
    internal static string MemberSlot(this SimpleNameSyntax name, ConversionContext context) =>
        context.SemanticHelper.GetSymbol(name) is IFieldSymbol field
            ? field.TwinSlot()
            : name.Identifier.Text.ToCamelCase();

    /// <summary>An instance field of a plain class the source declares, whose twin eqc writes by this
    /// same rule: a framework type's twin is the runtime's, and names its members as they are.</summary>
    private static bool Movable(IFieldSymbol field) =>
        !field.IsStatic && !field.IsConst && !field.IsImplicitlyDeclared
        && field.ContainingType is { TypeKind: TypeKind.Class, IsRecord: false } type && !IsComponent(type)
        && type.Locations.Any(location => location.IsInSource);

    /// <summary>Whether a member of the field's own type holds its twin name: a property, an event or a
    /// method, an explicit implementation's included under the name of the member it implements
    /// (<see cref="MemberTwinNameExtensions.MemberTwinName"/>), or another field, of which the one whose
    /// name the casing changed moves.</summary>
    private static bool MovesInItsType(IFieldSymbol field, string name) =>
        field.ContainingType.GetMembers().Any(member =>
            !SymbolEqualityComparer.Default.Equals(member, field) && !member.IsStatic && !member.IsImplicitlyDeclared
            && member switch
            {
                IFieldSymbol { IsConst: false } other => TwinName.Of(other.Name) == name && field.Name != name,
                _ => HoldsAName(member) && member.MemberTwinName() == name,
            });

    /// <summary>The names the twins of <paramref name="type"/> and its bases hold on each instance: every
    /// property's, event's and method's twin name, an explicit implementation's included, and every
    /// field's slot, which may have moved itself.</summary>
    private static HashSet<string> HeldByAncestors(INamedTypeSymbol? type)
    {
        var held = new HashSet<string>(StringComparer.Ordinal);
        for (var holder = type; holder is not null && holder.SpecialType != SpecialType.System_Object; holder = holder.BaseType)
        {
            foreach (var member in holder.GetMembers())
            {
                if (member.IsStatic || member.IsImplicitlyDeclared) continue;
                if (member is IFieldSymbol { IsConst: false } || HoldsAName(member)) held.Add(member.MemberTwinName());
            }
        }
        return held;
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
