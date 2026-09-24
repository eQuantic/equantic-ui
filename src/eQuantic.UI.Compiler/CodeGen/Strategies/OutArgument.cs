using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// The <c>out</c> argument of a method a strategy lowers INLINE — <c>int.TryParse</c>,
/// <c>decimal.TryParse</c>, <c>TryGetValue</c> — where the lowering writes the variable itself,
/// because no emitted callee hands an object back (that is <see cref="OutParameters"/>' shape, for
/// a method the transpiler writes).
/// <para>
/// One owner, because the copies diverged. The parse lowerings had learnt that a discard receives
/// nothing and that a field is written through <c>this</c>; TryGetValue kept a copy that took the
/// argument's SOURCE TEXT, so <c>out _</c> assigned an undeclared <c>_</c> and <c>out _field</c> an
/// undeclared <c>_field</c> — a ReferenceError at the first hit, since a module is strict.
/// </para>
/// </summary>
internal static class OutArgument
{
    /// <summary><c>out _</c>, <c>out var _</c> and <c>out T _</c>: a discard, which nothing reads.
    /// Converted as a name, <c>_</c> read as a member of the component: <c>this._ = …</c> gave it a
    /// property nobody declared.</summary>
    public static bool IsDiscard(ArgumentSyntax argument, ConversionContext context) => argument.Expression switch
    {
        DeclarationExpressionSyntax { Designation: DiscardDesignationSyntax } => true,
        IdentifierNameSyntax { Identifier.ValueText: "_" } discard =>
            context.SemanticHelper.GetSymbol(discard) is null or IDiscardSymbol,
        _ => false,
    };

    /// <summary>What the argument assigns: the variable it declares (hoisted by
    /// <see cref="OutParameters"/> under the same name), or the one it names.</summary>
    public static string Target(ArgumentSyntax argument, ConversionContext context) => argument.Expression switch
    {
        DeclarationExpressionSyntax { Designation: SingleVariableDesignationSyntax single } =>
            single.Identifier.Text.ToJsIdentifier(),
        DeclarationExpressionSyntax => "",
        var expression => context.Converter.ConvertExpression(expression),
    };

    /// <summary>A target a second mention cannot change: a bare name, which is a local or a
    /// parameter here (the template writer's own rule: a plain name's second read cannot be
    /// observed). An element or a member (<c>this.x</c>) may have an effect or an accessor of its
    /// own, so it is named once.</summary>
    public static bool IsBareName(string target) =>
        System.Text.RegularExpressions.Regex.IsMatch(target, @"^[A-Za-z_$][A-Za-z0-9_$]*$");

    /// <summary>
    /// The target as a PLACE in a template. A bare name is itself. Anything else is split into the
    /// parts it reads — <c>values[index++]</c> into the array and the index, <c>this.total</c> into
    /// the object — each handed to <paramref name="hole"/>, which gives it a part of the template
    /// and answers the hole that names it, in the order it is handed over. So the template's
    /// writer evaluates each part ONCE, however many branches assign the place, and a named
    /// argument that puts the out first still has its effects run first.
    /// </summary>
    public static string Place(ArgumentSyntax argument, ConversionContext context, Func<JsExpr, string> hole)
    {
        var target = Target(argument, context);
        if (IsBareName(target)) return target;

        var place = context.Converter.ConvertIr(argument.Expression);
        return place switch
        {
            JsIndex index => $"{hole(index.Target)}[{hole(index.IndexExpression)}]",
            JsMember member => $"{hole(member.Target)}.{member.Name}",
            _ => JsExprWriter.Write(place),
        };
    }
}
