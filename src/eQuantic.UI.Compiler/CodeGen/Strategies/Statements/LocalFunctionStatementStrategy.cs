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

        // TYPED, and an ARROW rather than a `function`. A C# local function inside a method can use
        // the instance — `this._findText` — and a `function` declaration rebinds `this` to
        // undefined in a module, so every capture read as a TypeError the first time it ran.
        var parameters = string.Join(", ", localFn.ParameterList.Parameters
            .Select(p => context.TypeAnnotations
                ? $"{p.Identifier.Text.ToJsIdentifier()}: {TypeScriptEmitter.CSharpTypeToTypeScript(p.Type?.ToString())}"
                : p.Identifier.Text.ToJsIdentifier()));

        var sb = new StringBuilder();
        sb.Append($"const {name} = ({parameters}) => ");

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

        // An arrow is an ASSIGNMENT, so it ends in a semicolon where a `function` declaration did
        // not — without it the next statement runs on and the module fails to parse.
        sb.Append(';');
        return sb.ToString();
    }

    public int Priority => 10;
}
