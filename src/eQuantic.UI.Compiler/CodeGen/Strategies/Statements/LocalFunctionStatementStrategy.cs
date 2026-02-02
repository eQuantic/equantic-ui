using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

public class LocalFunctionStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is LocalFunctionStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var localFn = (LocalFunctionStatementSyntax)node;
        var name = localFn.Identifier.Text;
        if (!string.IsNullOrEmpty(name) && char.IsUpper(name[0]))
        {
            name = char.ToLower(name[0]) + name.Substring(1);
        }

        var parameters = string.Join(", ", localFn.ParameterList.Parameters.Select(p => p.Identifier.Text));
        
        var sb = new StringBuilder();
        sb.Append($"function {name}({parameters}) ");

        if (localFn.Body != null)
        {
            sb.Append(context.Converter.ConvertBlock(localFn.Body));
        }
        else if (localFn.ExpressionBody != null)
        {
            sb.Append("{ return ");
            sb.Append(context.Converter.ConvertExpression(localFn.ExpressionBody.Expression));
            sb.Append("; }");
        }

        return sb.ToString();
    }

    public int Priority => 10;
}
