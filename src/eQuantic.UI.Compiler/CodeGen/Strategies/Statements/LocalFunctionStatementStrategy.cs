using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Statements;

/// <summary>
/// A local function becomes a <c>const</c> arrow beside the code that calls it (the converter
/// hoists these to the top of their block, since C# hoists local functions and a const is not).
/// An ARROW rather than a <c>function</c>: a C# local function can use the instance —
/// <c>this._findText</c> — and a <c>function</c> declaration rebinds <c>this</c> to undefined in
/// a module, so every capture read as a TypeError the first time it ran.
/// </summary>
public class LocalFunctionStatementStrategy : IStatementIrStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is LocalFunctionStatementSyntax;
    }

    public JsStatement ConvertIr(StatementSyntax node, ConversionContext context)
    {
        var localFn = (LocalFunctionStatementSyntax)node;
        // The SAME pair of transformations the reference applies (IdentifierStrategy): camelCase,
        // then the JS-identifier rename. Hand-lowercasing here is how the declaration and the
        // reference drift — and a local function called `Delete` emitted `const delete = …`, which
        // is not a name JS will take at all.
        var name = localFn.Identifier.Text.ToCamelCase().ToJsIdentifier();
        var parameters = string.Join(", ", localFn.ParameterList.Parameters
            .Select(p => Parameter(p, context)));

        // The body, laid out where it was built; an expression body becomes a block with one
        // return, so both forms read the same.
        var block = localFn.Body != null
            ? context.Converter.ConvertBlock(localFn.Body)
            : JsStatementWriter.Write(
                JsStatement.Block(new[]
                {
                    JsStatement.Return(context.Converter.ConvertIr(localFn.ExpressionBody!.Expression)),
                }),
                context.Layout, context.Depth);

        return JsStatement.Const(name, JsExpr.ArrowBlock(parameters, block));
    }

    /// <summary>
    /// A parameter as TypeScript: typed when annotations are on, a <c>params</c> one as a rest
    /// parameter, a defaulted one as optional (or with its default, where the value is a real one).
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
