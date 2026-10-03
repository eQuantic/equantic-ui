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
/// a module, so every capture read as a TypeError the first time it ran. Its body reaches the
/// writer as IR, so each statement in it maps to its own line (#293).
/// </summary>
public class LocalFunctionStatementStrategy : IStatementStrategy
{
    public bool CanConvert(StatementSyntax node, ConversionContext context)
    {
        return node is LocalFunctionStatementSyntax;
    }

    public JsStatement Convert(StatementSyntax node, ConversionContext context)
    {
        var localFn = (LocalFunctionStatementSyntax)node;
        // The name every reference reaches too (LocalFunctionName): camel-cased, a legal JS
        // identifier, and renamed where the member already holds it. Naming it here by hand is how
        // the declaration and the references drifted.
        var name = LocalFunctionName.Of(localFn, context);
        var isAsync = localFn.Modifiers.Any(SyntaxKind.AsyncKeyword);

        // With `out`/`ref` parameters, the callee contract every call site unwraps (OutParameters):
        // the outs leave the signature and everything comes back in one {outs, $} object, as a
        // method's and a lambda's do. The local function kept its outs as plain parameters and
        // returned the bare value, so the call read each out as undefined (#541).
        var byReference = OutParameters.Of(localFn.ParameterList);
        if (byReference.Count > 0)
        {
            var kept = string.Join(", ", localFn.ParameterList.Parameters
                .Where(p => !OutParameters.IsOut(p))
                .Select(p => Parameter(p, context)));
            return JsStatement.Const(name, JsExpr.ArrowBlock(kept,
                OutParameters.ArrowBody(localFn.Body, localFn.ExpressionBody?.Expression, byReference, isAsync, context),
                context.Layout, context.Depth, isAsync));
        }

        var parameters = string.Join(", ", localFn.ParameterList.Parameters
            .Select(p => Parameter(p, context)));

        // An expression body becomes a block with one return, carrying the expression, so both
        // forms read and map the same — and what the expression declares is declared in front of
        // the return, inside the function, where each call has its own.
        var body = localFn.Body != null
            ? context.Converter.ConvertBlockIr(localFn.Body)
            : context.Converter.ConvertExpressionBodyIr(localFn.ExpressionBody!.Expression);

        // The `async` has to cross (read above). A C# local function that awaits becomes a JS arrow
        // that awaits, and an arrow that is not `async` makes `await` in its body a SyntaxError —
        // the module then fails to parse, which is a build that succeeds and a page that never
        // runs. Lambdas already carry it (LambdaExpressionStrategy) and so do component methods;
        // this one dropped it, so the shape only broke where somebody wrote a local async helper.
        // A const bound to the arrow any lambda's block is (#384); a statement in the body with no
        // origin of its own belongs to the function.
        return JsStatement.Const(name, JsExpr.ArrowBlock(parameters, body with { Origin = localFn }, context.Layout, context.Depth, isAsync));
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
