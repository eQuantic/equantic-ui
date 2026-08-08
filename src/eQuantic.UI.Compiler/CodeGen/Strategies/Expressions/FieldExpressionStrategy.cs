using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// C# 14's <c>field</c> — the compiler-provided backing store of the property being written, which
/// is what lets a property keep an INVARIANT without the author hand-rolling a private field:
/// <c>init => field = value.Count > 3 ? throw … : value;</c>.
///
/// <para>
/// JavaScript has no such thing, so the twin names the slot: <c>$name</c>, after the property. The
/// <c>$</c> is deliberate — C# identifiers cannot start with one, so the generated slot can never
/// collide with a field the author declared themselves.
/// </para>
/// </summary>
public class FieldExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) => node is FieldExpressionSyntax;

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var property = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        return property is null ? "undefined" : $"this.{BackingSlot(property)}";
    }

    /// <summary>The name of the emitted backing slot for a property that uses <c>field</c>.</summary>
    public static string BackingSlot(PropertyDeclarationSyntax property) =>
        $"${property.Identifier.Text.ToCamelCase()}";

    /// <summary>Whether this property's accessors read or write the compiler-provided store — the
    /// emitter needs the slot declared before the accessors that name it.</summary>
    public static bool UsesBackingField(PropertyDeclarationSyntax property) =>
        property.DescendantNodes().Any(node => node is FieldExpressionSyntax);

    public int Priority => 10;
}
