using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;
using Microsoft.CodeAnalysis.CSharp;

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
        // A reserved JS word takes a trailing underscore — declaration and references go through
        // the same rule, so `var package = …` stays one identifier on both sides.
        var name = variable.Identifier.Text.ToJsIdentifier();
        var patternVars = PatternVariableScanner.Declarations(variable.Initializer?.Value);
        var init = variable.Initializer != null
            ? context.Converter.ConvertExpression(variable.Initializer.Value)
            : "null";

        if (decl.UsingKeyword.IsKind(SyntaxKind.UsingKeyword))
        {
            // For a 'using var', we should ideally wrap the remainder of the block.
            // Since this strategy only sees the statement, we'll emit a declaration
            // and a comment. The true 100% implementation requires block-aware conversion.
            // For now, let's at least emit the declaration.
            return $"{patternVars}const {name} = {init}; /* using */";
        }

        return $"{patternVars}let {name} = {init};";
    }

    public int Priority => 0;
}
