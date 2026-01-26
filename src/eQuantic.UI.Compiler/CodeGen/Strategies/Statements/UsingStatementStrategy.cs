using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

public class UsingStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is UsingStatementSyntax;
    }

    public string Convert(StatementSyntax node, ConversionContext context)
    {
        var usingStmt = (UsingStatementSyntax)node;
        
        // 1. Variable extraction
        // using (var x = new X()) { ... }
        string resourceVar = "";
        string declaration = "";

        if (usingStmt.Declaration != null)
        {
            var variable = usingStmt.Declaration.Variables.First();
            resourceVar = variable.Identifier.Text;
            // Manual conversion since Declaration is not a Statement
            var init = variable.Initializer != null 
                ? context.Converter.ConvertExpression(variable.Initializer.Value) 
                : "null";
            declaration = $"const {resourceVar} = {init};";
        }
        else if (usingStmt.Expression != null)
        {
            // using (expr) ... (Cleaner syntax, no var)
            // We need to capture this expression to dispose it.
            // Generate a temporary var name.
            resourceVar = "_disposable_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            var expr = context.Converter.ConvertExpression(usingStmt.Expression);
            declaration = $"const {resourceVar} = {expr};";
        }

        var statement = context.Converter.Convert(usingStmt.Statement);

        return $@"
{{
    {declaration}
    try {{
        {statement}
    }} finally {{
        if ({resourceVar} && typeof {resourceVar}.dispose === 'function') {{
            {resourceVar}.dispose();
        }}
    }}
}}";
    }

    public int Priority => 0;
}
