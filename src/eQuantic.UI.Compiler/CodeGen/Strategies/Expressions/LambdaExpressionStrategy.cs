using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for converting lambda expressions.
/// Handles:
/// - () => expr
/// - (a, b) => { stmt; }
/// - x => x + 1
/// </summary>
public class LambdaExpressionStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is LambdaExpressionSyntax;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        if (node is ParenthesizedLambdaExpressionSyntax parenthesized)
        {
            // A lambda with `out`/`ref` parameters must honour the SAME callee contract methods do
            // (see OutParameters): outs leave the signature and everything comes back in one
            // {outs, $} object, because that is what every call site unwraps. C# 14's modifier-on-
            // untyped-parameters (`(text, out result) => …`) made these easy to write — and the
            // lambda used to keep its outs as plain parameters, so the unwrap read undefined.
            var byReference = OutParameters.Of(parenthesized.ParameterList);
            if (byReference.Count > 0)
            {
                var kept = string.Join(", ", parenthesized.ParameterList.Parameters
                    .Where(p => !OutParameters.IsOut(p))
                    .Select(p => Typed(p.Identifier.Text.ToJsIdentifier(), p, context)));
                // Never async: C# refuses an `out` or a `ref` parameter on an async lambda (CS1988).
                return JsExpr.ArrowBlock(kept,
                    OutParameters.Body(parenthesized.Block, parenthesized.ExpressionBody, byReference, context.Converter),
                    context.Layout, context.Depth);
            }

            // The parameter TYPES come from the semantic model: a lambda handed to a config object
            // has nothing to infer from on the other side, and an untyped parameter is an error in
            // the runtime's own build rather than merely a missing type.
            var parameters = string.Join(", ", parenthesized.ParameterList.Parameters
                .Select(p => Typed(p.Identifier.Text.ToJsIdentifier(), p, context)));
            var isAsyncLambda = parenthesized.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsyncKeyword);
            return parenthesized.Block != null
                ? Block(parameters, parenthesized.Block, isAsyncLambda, context)
                : ExpressionBody(parameters, parenthesized.ExpressionBody!, isAsyncLambda, context);
        }
        
        if (node is SimpleLambdaExpressionSyntax simple)
        {
            var param = Typed(simple.Parameter.Identifier.Text.ToJsIdentifier(), simple.Parameter, context);
            var isAsyncLambda = simple.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsyncKeyword);
            return simple.Block != null
                ? Block(param, simple.Block, isAsyncLambda, context)
                : ExpressionBody(param, simple.ExpressionBody!, isAsyncLambda, context);
        }

        // A lambda's syntax is one of the two above; there is no third to write.
        throw new System.Diagnostics.UnreachableException($"A lambda of kind {node.GetType().Name}.");
    }

    /// <summary>
    /// A lambda's block as the statement IR, laid out where the lambda stands: each statement keeps
    /// the C# it came from, so wherever a writer places the arrow, its lines map to their own
    /// (#384). As text, the block was laid out before any writer saw it, and its marks went with it.
    /// </summary>
    internal static JsExpr Block(string parameters, BlockSyntax block, bool isAsync, ConversionContext context) =>
        JsExpr.ArrowBlock(parameters, context.Converter.ConvertBlockIr(block), context.Layout, context.Depth, isAsync);

    /// <summary>
    /// A concise lambda body, given a BLOCK when it declares variables.
    /// <para>
    /// `x => Maybe(x) is { } y ? y : ""` converts to an assignment of `y` inside the condition, and
    /// a concise body has nowhere to declare it — so the name was assigned and never declared,
    /// which in a module (strict mode) is a ReferenceError the first time the lambda runs. An
    /// `out var` there (`t => int.TryParse(t, out var n) ? n : -1`) was declared by the enclosing
    /// METHOD instead, one slot for every call. The block is added only when there is something
    /// to declare, so every other lambda stays an expression — the writer parenthesizes an object
    /// literal there.
    /// </para>
    /// </summary>
    private static JsExpr ExpressionBody(string parameters, ExpressionSyntax expression, bool isAsync,
        ConversionContext context) =>
        // Asked before the expression is converted: converted, it is cached at the depth it was
        // converted at, and the block's return sits one level deeper than the arrow.
        ExpressionVariableScanner.Names(expression).Count == 0
            ? JsExpr.Arrow(parameters, context.Converter.ConvertIr(expression), isAsync)
            : JsExpr.ArrowBlock(parameters, context.Converter.ConvertExpressionBodyIr(expression, returns: true), context.Layout, context.Depth, isAsync);

    /// <summary>A parameter with its TS type, resolved through the model. Falls back to the bare
    /// name when nothing can be said — an untyped parameter beats a wrong one.</summary>
    private static string Typed(string name, ParameterSyntax parameter, ConversionContext context)
    {
        // A lambda parameter is DECLARED by the lambda, so it answers to GetDeclaredSymbol —
        // GetSymbolInfo is for references, and asking it here came back empty every time.
        var declared = parameter.Type is { } syntax
            ? context.SemanticHelper.GetSymbol(syntax) as ITypeSymbol
            : context.SemanticHelper.GetDeclaredSymbol(parameter) is IParameterSymbol symbol
                ? symbol.Type
                : null;
        if (declared is null || !context.TypeAnnotations) return name;
        // From the SPECIAL type, not the name: a `float` parameter's symbol is named "Single", and
        // the name-keyed mapper answers "Single" — a type nothing declares.
        var ts = declared.SpecialType switch
        {
            SpecialType.System_String or SpecialType.System_Char => "string",
            SpecialType.System_Boolean => "boolean",
            SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Double
                or SpecialType.System_Single or SpecialType.System_Decimal or SpecialType.System_Int16
                or SpecialType.System_Byte => "number",
            _ => declared.TypeKind switch
            {
                TypeKind.Enum => "string",
                TypeKind.Interface or TypeKind.TypeParameter => "any",
                _ => TypeScriptEmitter.CSharpTypeToTypeScript(declared.Name),
            },
        };
        // `any` is worse than nothing: an unannotated parameter INFERS from what it is passed, and
        // only says nothing where there was nothing to say.
        return ts == "any" || ts == declared.Name ? name : $"{name}: {ts}";
    }

    public int Priority => 10;
}
