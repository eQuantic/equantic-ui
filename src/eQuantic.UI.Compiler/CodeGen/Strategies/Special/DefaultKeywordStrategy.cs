using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Special;

/// <summary>
/// Strategy for the default keyword: <c>default(T)</c> and the <c>default</c> literal are T's
/// default as the semantic model gives it (#380), the same default a field of T starts with (see
/// <see cref="DefaultValue"/>): a long's <c>0n</c>, a decimal's zero, a char's <c>'\0'</c>, an
/// enum's zero member, a struct's zero instance (or, where its twin cannot build one, the twin's own
/// default), and null for a nullable or a reference. The type's SPELLING decided before, so <c>default(long)</c> was a plain 0, <c>default(int?)</c> was 0 where
/// C# has null, and the literal was <c>undefined</c> wherever it stood: <c>int x = default</c> left
/// x undefined, and <c>x + 1</c> was NaN.
/// </summary>
public class DefaultKeywordStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        // Handle default(T) expression
        if (node is DefaultExpressionSyntax)
            return true;

        // Handle default literal
        if (node is LiteralExpressionSyntax literal && literal.Kind() == SyntaxKind.DefaultLiteralExpression)
            return true;

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        // default(T) is T's default; the literal is the default of the type C# converts it to, the
        // target's: a local's, a parameter's, the other arm's of a conditional.
        var type = node is DefaultExpressionSyntax named
            ? context.SemanticHelper.GetType(named.Type)
            : context.SemanticHelper.GetConvertedType(node) ?? context.SemanticHelper.GetType(node);
        if (type is not null and not { TypeKind: TypeKind.Error })
        {
            var zero = DefaultValue.Of(type, context);
            // A struct whose twin cannot build its zero (a hand-written vocabulary twin not marked
            // [ZeroConstructs]) gets the twin's own default, which is what `undefined` asks for: a
            // hand-written constructor's default parameters (`style: BoxStyle = new BoxStyle()`)
            // and its `!== undefined` checks apply for undefined and never for null, and C# has no
            // null struct to answer with. `UI.Box(style: default)` would build a Box with no style.
            return zero == "null" && type.IsValueType && !type.IsNullableValue() ? "undefined" : zero;
        }

        // No model: default(T) is what T's name says, and the literal names nothing.
        if (node is not DefaultExpressionSyntax spelled) return "undefined";
        var value = TypeDeclarationExtensions.DefaultFor(spelled.Type);
        if (value.Contains("$eq.")) context.UsedHelpers.Add(Eq.Import);
        return value;
    }

    public int Priority => 15; // Higher than LiteralExpressionStrategy
}
