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
/// which no C# name holds and which a property's own store, <c>$name</c>, never meets. Of two fields a
/// case apart, the one the casing changed moves.
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
        return Moves(field, name) ? name + "$" : name;
    }

    /// <summary>The name a member access writes for <paramref name="name"/>: a field's slot when the
    /// model binds it to one, its twin name otherwise.</summary>
    internal static string MemberSlot(this SimpleNameSyntax name, ConversionContext context) =>
        context.SemanticHelper.GetSymbol(name) is IFieldSymbol field
            ? field.TwinSlot()
            : name.Identifier.Text.ToCamelCase();

    private static bool Moves(IFieldSymbol field, string name)
    {
        if (field.IsStatic || field.IsConst || field.IsImplicitlyDeclared) return false;
        if (field.ContainingType is not { TypeKind: TypeKind.Class, IsRecord: false } type || IsComponent(type)) return false;
        for (var holder = type; holder is not null && holder.SpecialType != SpecialType.System_Object; holder = holder.BaseType)
        {
            foreach (var member in holder.GetMembers())
            {
                if (SymbolEqualityComparer.Default.Equals(member, field) || member.IsStatic || member.IsImplicitlyDeclared) continue;
                if (TwinName.Of(member.Name) != name) continue;
                switch (member)
                {
                    case IPropertySymbol or IEventSymbol or IMethodSymbol { MethodKind: MethodKind.Ordinary }:
                        return true;
                    case IFieldSymbol other when !other.IsConst:
                        // Of two fields a case apart, the one whose name the casing changed.
                        if (field.Name != name) return true;
                        break;
                }
            }
        }
        return false;
    }

    /// <summary>A component, whose fields the server hydrates under their twin names.</summary>
    private static bool IsComponent(INamedTypeSymbol type)
    {
        for (var holder = type; holder is not null; holder = holder.BaseType)
            if (holder.Name is "StatelessComponent" or "StatefulComponent" or "UiComponent") return true;
        return false;
    }
}
