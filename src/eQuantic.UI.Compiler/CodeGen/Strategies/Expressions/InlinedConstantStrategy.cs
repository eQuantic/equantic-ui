using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Inlines a reference to an external <c>static readonly</c>/<c>const</c> constant whose value is a
/// pure literal or constructor — the write-once ICON PACK contract: <c>LucideIcons.Camera</c> (a
/// <see cref="global::eQuantic.UI.Primitives.IconGlyph"/> field in a pack assembly) transpiles to
/// <c>new IconGlyph('camera', 'M…', 'stroke')</c> AT THE USE SITE. That tree-shakes each pack to the
/// glyphs actually referenced (no giant per-pack JS module) and needs no runtime pack registry —
/// the only import is IconGlyph itself, which the runtime already provides.
///
/// The initializer lives in the PACK's syntax tree (added to the compilation as a reference source),
/// so it is converted with the pack tree's own semantic model. Triggers ONLY when the field's
/// initializer is reachable and side-effect-free; otherwise the fallback member-access applies.
/// </summary>
public class InlinedConstantStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is MemberAccessExpressionSyntax && TryResolveInlinable(node, context, out _, out _);

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        if (!TryResolveInlinable(node, context, out var initializer, out var glyphTypeName))
            return context.Converter.ConvertExpression(((MemberAccessExpressionSyntax)node).Expression);

        var model = context.SemanticModel!.Compilation.GetSemanticModel(initializer.SyntaxTree);
        var inlined = context.Converter.ConvertExpressionWithModel(initializer, model);

        // The inlined `new IconGlyph(...)` references a runtime-provided type the COMPONENT never
        // named itself — register it so the emitter adds the import (the same channel UsedHelpers
        // uses for runtime symbols).
        context.UsedHelpers.Add(glyphTypeName);
        return inlined;
    }

    /// <summary>The inlinable initializer for the accessed field, when this is an external constant
    /// of the write-once icon-glyph type with a reachable, side-effect-free value.</summary>
    private static bool TryResolveInlinable(SyntaxNode node, ConversionContext context,
        out ExpressionSyntax initializer, out string glyphTypeName)
    {
        initializer = null!;
        glyphTypeName = null!;

        if (context.SemanticModel is null) return false;
        if (context.SemanticHelper.GetSymbol(node) is not IFieldSymbol
            {
                IsStatic: true,
                Type: INamedTypeSymbol fieldType,
            } field)
        {
            return false;
        }

        // Scoped to the write-once IconGlyph contract (the concrete, safe case). The type check is
        // by fully-qualified name so only the real Primitives type triggers.
        if (fieldType.Name != "IconGlyph"
            || fieldType.ContainingNamespace?.ToDisplayString() != "eQuantic.UI.Primitives")
        {
            return false;
        }

        // The initializer must be reachable — it lives in the pack SOURCE (a reference-source tree),
        // not in metadata (which carries no field initializer bodies).
        var declaration = field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        if (declaration is not VariableDeclaratorSyntax { Initializer.Value: { } value })
            return false;

        // Side-effect-free values only: a constructor of the glyph, or a literal.
        if (value is not (ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax
            or LiteralExpressionSyntax))
        {
            return false;
        }

        initializer = value;
        glyphTypeName = fieldType.Name;
        return true;
    }

    public int Priority => 25; // Above MemberAccessStrategy (0) and the value-type member specializers.
}
