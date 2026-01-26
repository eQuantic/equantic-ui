using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

public class LocalDeclarationStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is LocalDeclarationStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var decl = (LocalDeclarationStatementSyntax)node;
        // Simplified: only taking the first variable (C# allows 'int x, y;')
        // JS often uses one line per var or 'let x, y;'
        // We will assume standard single declaration for now or iterate
        
        var variable = decl.Declaration.Variables.First();
        var name = variable.Identifier.Text;
        var init = variable.Initializer != null 
            ? context.Converter.ConvertExpression(variable.Initializer.Value) 
            : "null";

        if (decl.UsingKeyword.IsKind(SyntaxKind.UsingKeyword))
        {
            // For a 'using var', we should ideally wrap the remainder of the block.
            // Since this strategy only sees the statement, we'll emit a declaration
            // and a comment. The true 100% implementation requires block-aware conversion.
            // For now, let's at least emit the declaration.
            return $"const {name} = {init}; /* using */";
        }

        return $"let {name} = {init};";
    }

    public int Priority => 0;
}
