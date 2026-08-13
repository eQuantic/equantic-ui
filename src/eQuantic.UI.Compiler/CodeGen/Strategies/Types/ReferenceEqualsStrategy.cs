using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// <c>object.ReferenceEquals(a, b)</c> — IDENTITY, which JavaScript spells <c>===</c> for objects.
/// <para>
/// Without this the call fell to the generic invocation path and came out
/// <c>Object.referenceEquals(a, b)</c>: a method the language does not have, so the first shared
/// model that asked "is this the same instance?" threw at run time in the browser while the .NET
/// side answered correctly.
/// </para>
/// <para>
/// Identity is exactly what <c>===</c> gives for a reference, which is why this is a rewrite and
/// not a helper: two structurally equal records are two objects, and both languages agree they are
/// not the same one. Structural equality is a different question, and
/// <see cref="StructuralEqualsStrategy"/> answers it.
/// </para>
/// </summary>
public class ReferenceEqualsStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax { ArgumentList.Arguments.Count: 2 } invocation)
            return false;

        // Written as `ReferenceEquals(a, b)` or `object.ReferenceEquals(a, b)` — the same method
        // either way, and a model that has both spellings must not translate only one of them.
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null,
        };
        if (name != "ReferenceEquals") return false;

        // Ask the model when it can: an app is free to declare its own ReferenceEquals, and
        // rewriting THAT to `===` would quietly change what the app means.
        var symbol = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        return symbol is null || symbol.ContainingType?.SpecialType == SpecialType.System_Object;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var left = context.Converter.ConvertExpression(invocation.ArgumentList.Arguments[0].Expression);
        var right = context.Converter.ConvertExpression(invocation.ArgumentList.Arguments[1].Expression);
        return $"({left} === {right})";
    }

    public int Priority => 12;
}
