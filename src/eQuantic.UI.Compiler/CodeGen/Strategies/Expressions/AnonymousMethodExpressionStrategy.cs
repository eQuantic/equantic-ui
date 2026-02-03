using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

public class AnonymousMethodExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is AnonymousMethodExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var anon = (AnonymousMethodExpressionSyntax)node;
        var parameters = "";
        if (anon.ParameterList != null)
        {
            parameters = string.Join(", ", anon.ParameterList.Parameters.Select(p => p.Identifier.Text));
        }

        var body = context.Converter.ConvertBlock(anon.Block);
        return $"({parameters}) => {body}";
    }

    public int Priority => 10;
}
