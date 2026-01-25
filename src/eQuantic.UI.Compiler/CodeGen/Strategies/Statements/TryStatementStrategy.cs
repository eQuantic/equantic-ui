using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Strategies;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

public class TryStatementStrategy : IStatementStrategy
{
    public int Priority => 0;

    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is TryStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var tryStmt = (TryStatementSyntax)node;
        var builder = new System.Text.StringBuilder();
        var converter = context.Converter;

        builder.Append("try ");
        builder.Append(converter.Convert(tryStmt.Block));

        foreach (var catchClause in tryStmt.Catches)
        {
            builder.Append(" catch");
            if (catchClause.Declaration != null)
            {
                builder.Append(" (");
                builder.Append(catchClause.Declaration.Identifier.Text);
                builder.Append(")");
            }
            else
            {
                // JavaScript catch allows 'catch' but binding is usually expected in older envs
                // Modern JS supports optional catch binding (catch {})
                // We will output catch (e) if implicit or just catch {} if that's safe. 
                // Let's stick to catch {} for untyped catches to match C# semantics of "swallow everything"
            }
            
            builder.Append(" ");
            builder.Append(converter.Convert(catchClause.Block));
        }

        if (tryStmt.Finally != null)
        {
            builder.Append(" finally ");
            builder.Append(converter.Convert(tryStmt.Finally.Block));
        }

        return builder.ToString();
    }
}
