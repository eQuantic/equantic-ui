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
        node is MemberAccessExpressionSyntax
        && (TryResolveConstant(node, context, out _) || TryResolveInlinable(node, context, out _, out _));

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        // A `const` is a COMPILE-TIME value by definition — inlining it is always faithful, and it
        // is what lets a page default to a constant from an assembly the client bundle never sees
        // (a server-side domain's `PackageStats.LastVerifiedDownloads`, say: emitting the reference
        // would be "PackageStats is not defined" the moment the page renders).
        if (TryResolveConstant(node, context, out var constant)) return constant;

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

    /// <summary>
    /// The JS literal for a `const` field access. ENUM members are excluded: their member-name
    /// string representation is the wire contract (EnumStrategy owns it), not their ordinal.
    /// </summary>
    private static bool TryResolveConstant(SyntaxNode node, ConversionContext context, out string literal)
    {
        literal = null!;
        if (context.SemanticModel is null) return false;
        if (context.SemanticHelper.GetSymbol(node) is not IFieldSymbol
            {
                IsConst: true, HasConstantValue: true, ContainingType: { } owner,
            } field)
        {
            return false;
        }
        // Track L D2: nothing that lives on a resource-shaped class may inline — the value is
        // per-culture data, and a baked literal is the build machine's language in production.
        // (The generated accessors are PROPERTIES and never reach this field-only path; this
        // guard is the belt for a hand-written const inside a Designer-shaped class, and for
        // whoever widens this strategy to properties one day.)
        if (Services.ResourceClasses.IsResourceClass(owner)) return false;
        if (owner.TypeKind == TypeKind.Enum) return false;

        switch (field.ConstantValue)
        {
            case null:
                literal = "null";
                return true;
            case string text:
                literal = $"'{Escape(text)}'";
                return true;
            case char character:
                literal = $"'{Escape(character.ToString())}'";
                return true;
            case bool flag:
                literal = flag ? "true" : "false";
                return true;
            // EXACTNESS FIRST: only values a JS number represents exactly are inlined. `long.MaxValue`
            // as a literal becomes 9223372036854776000 — the dedicated long/decimal strategies keep
            // those in their compat types, so leave them alone.
            case decimal:
                return false;
            case long or ulong or int or uint or short or ushort or byte or sbyte:
                var integral = System.Convert.ToDecimal(field.ConstantValue);
                if (System.Math.Abs(integral) > 9007199254740991m) return false;
                literal = integral.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            case double or float:
                literal = ((IFormattable)field.ConstantValue).ToString(
                    null, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            default:
                return false;
        }
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "\\r");

    /// <summary>The inlinable initializer for the accessed field, when this is an external constant
    /// of the write-once icon-glyph type with a reachable, side-effect-free value.</summary>
    private static bool TryResolveInlinable(SyntaxNode node, ConversionContext context,
        out ExpressionSyntax initializer, out string glyphTypeName)
    {
        initializer = null!;
        glyphTypeName = null!;

        if (context.SemanticModel is null) return false;
        // A FIELD or a PROPERTY — the rule was always about the VALUE, not about which kind of
        // member holds it, and the distinction turned out to matter for a reason far from here: a
        // pack of 16,284 `static readonly` fields is one static constructor, so the IL trimmer
        // cannot drop the 16,279 an app never names and a native bundle carries 14.5 MB to draw
        // five glyphs (measured). Expression-bodied properties are 16,284 separate method bodies,
        // which the trimmer removes one by one — the whole pack assembly disappears from the
        // publish. That shape only works if inlining follows it here.
        var member = context.SemanticHelper.GetSymbol(node);
        ITypeSymbol? memberType = member switch
        {
            IFieldSymbol { IsStatic: true } f => f.Type,
            IPropertySymbol { IsStatic: true, GetMethod: not null } p => p.Type,
            _ => null,
        };
        if (memberType is not INamedTypeSymbol fieldType || member is null) return false;

        // Track L D2: same belt as TryResolveConstant — resource-shaped owners never inline.
        if (Services.ResourceClasses.IsResourceClass(member.ContainingType)) return false;

        // Scoped to the write-once IconGlyph contract (the concrete, safe case). The type check is
        // by fully-qualified name so only the real Primitives type triggers.
        if (fieldType.Name != "IconGlyph"
            || fieldType.ContainingNamespace?.ToDisplayString() != "eQuantic.UI.Primitives")
        {
            return false;
        }

        // The value must be reachable — it lives in the pack SOURCE (a reference-source tree), not
        // in metadata, which carries neither field initializers nor property bodies.
        var declaration = member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        var value = declaration switch
        {
            VariableDeclaratorSyntax { Initializer.Value: { } initialized } => initialized,
            // `public static IconGlyph X => new("x", "M…");` — one expression, no accessor body,
            // which is exactly as side-effect-free as the field initializer it replaces.
            PropertyDeclarationSyntax { ExpressionBody.Expression: { } bodied } => bodied,
            _ => null,
        };
        if (value is null) return false;

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
