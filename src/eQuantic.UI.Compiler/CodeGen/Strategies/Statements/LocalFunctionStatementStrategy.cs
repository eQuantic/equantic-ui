using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        // The SAME pair of transformations the reference applies (IdentifierStrategy): camelCase,
        // then the JS-identifier rename. Hand-lowercasing here is how the declaration and the
        // reference drift — and a local function called `Delete` emitted `const delete = …`, which
        // is not a name JS will take at all.
        var name = localFn.Identifier.Text.ToCamelCase().ToJsIdentifier();

        // TYPED, and an ARROW rather than a `function`. A C# local function inside a method can use
        // the instance — `this._findText` — and a `function` declaration rebinds `this` to
        // undefined in a module, so every capture read as a TypeError the first time it ran.
        var parameters = string.Join(", ", localFn.ParameterList.Parameters
            .Select(p => Parameter(p, context)));

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

    /// <summary>
    /// One parameter, with everything C# lets it carry. The DEFAULT was dropped here while the
    /// method emitter has always kept it, so a local function written `Pane(title, body, leading =
    /// null)` emitted three REQUIRED parameters and every two-argument call site stopped
    /// type-checking — TS2554, from source that compiles in C#.
    /// <para>
    /// The three shapes match <c>TypeScriptEmitter.ParamWithDefault</c> deliberately: a `null`
    /// default becomes `?:` rather than `= undefined` (that is what the annotation is FOR, and it
    /// keeps the twin idiomatic), `params` becomes a rest parameter, and anything else keeps its
    /// converted value.
    /// </para>
    /// </summary>
    private static string Parameter(ParameterSyntax parameter, ConversionContext context)
    {
        var name = parameter.Identifier.Text.ToJsIdentifier();
        var type = TypeScriptEmitter.CSharpTypeToTypeScript(parameter.Type?.ToString());
        var rest = parameter.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.ParamsKeyword));
        var name_ = context.TypeAnnotations ? $"{name}: {type}" : name;

        if (rest) return $"...{name_}";
        if (parameter.Default is null) return name_;

        var value = context.Converter.ConvertExpression(parameter.Default.Value);
        if (value is "undefined" or "null")
            return context.TypeAnnotations ? $"{name}?: {type}" : $"{name} = {value}";
        return $"{name_} = {value}";
    }

    public int Priority => 10;
}
