using Microsoft.CodeAnalysis;

namespace eQuantic.UI.Compiler.CodeGen.Extensions;

/// <summary>
/// The name a member of a C# type is written under in its twin, read off its SYMBOL, the one place that
/// derives it. An emitter writes a member from its declaration's identifier through
/// <see cref="eQuantic.UI.TwinName"/> (<c>MethodLowering</c> writes <c>method.Identifier</c>), and an
/// explicit interface implementation's identifier is the member it implements, while Roslyn names its
/// symbol after the interface: <c>int IReads.Value()</c> is the symbol <c>IReads.Value</c>, which lowered
/// to <c>iReads.Value</c>, so a field <c>value</c> beside it stayed in <c>value</c> and hid the
/// <c>value()</c> the emitter wrote, and so did one beside an explicit property or event (Copilot's third
/// review of #696). A field is written in its slot (<see cref="FieldSlotExtensions.TwinSlot"/>).
/// </summary>
internal static class MemberTwinNameExtensions
{
    /// <summary>The member's name as its declaration spells it: an explicit implementation's is the name
    /// of the member it implements, every other member's its own.</summary>
    internal static string DeclaredName(this ISymbol member) => member switch
    {
        IMethodSymbol { ExplicitInterfaceImplementations: [var implemented, ..] } => implemented.Name,
        IPropertySymbol { ExplicitInterfaceImplementations: [var implemented, ..] } => implemented.Name,
        IEventSymbol { ExplicitInterfaceImplementations: [var implemented, ..] } => implemented.Name,
        _ => member.Name,
    };

    /// <summary>The name <paramref name="member"/> is written under in its twin: a field's slot, and every
    /// other member's declared name through <see cref="eQuantic.UI.TwinName"/>.</summary>
    internal static string MemberTwinName(this ISymbol member) =>
        member is IFieldSymbol field ? field.TwinSlot() : TwinName.Of(member.DeclaredName());
}
