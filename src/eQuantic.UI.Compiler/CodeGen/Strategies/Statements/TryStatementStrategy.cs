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

            // Emit a binding only when C# named one. A typed catch with no variable
            // (`catch (OverflowException)`) or an untyped `catch` both use JS's optional catch binding
            // (`catch { … }`) — the exception type itself isn't a JS construct, so every catch is catch-all.
            var identifier = catchClause.Declaration?.Identifier.Text;
            if (!string.IsNullOrEmpty(identifier))
            {
                // ANNOTATED `any`, because the emitted TypeScript is type-checked and a catch
                // binding is `unknown` there. C# hands you a typed exception, so the body reads
                // `error.Message` without asking — and the emitted module refused to compile on
                // exactly that line. The type is gone either way (every catch is catch-all in JS);
                // this only stops the annotation from being narrower than the language it came from.
                builder.Append(context.TypeAnnotations ? $" ({identifier}: any)" : $" ({identifier})");
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
