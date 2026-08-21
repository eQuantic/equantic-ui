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
                var isAsync = parenthesized.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsyncKeyword);
                var inner = parenthesized.Block != null
                    ? TrimBraces(context.Converter.ConvertBlock(parenthesized.Block))
                    : $"return {context.Converter.ConvertExpression(parenthesized.ExpressionBody!)};";
                var wrapped = OutParameters.WrapBody(inner, byReference, isAsync);
                return JsExpr.ArrowBlock(kept, $"{{ {wrapped} }}", isAsync);
            }

            // The parameter TYPES come from the semantic model: a lambda handed to a config object
            // has nothing to infer from on the other side, and an untyped parameter is an error in
            // the runtime's own build rather than merely a missing type.
            var parameters = string.Join(", ", parenthesized.ParameterList.Parameters
                .Select(p => Typed(p.Identifier.Text.ToJsIdentifier(), p, context)));
            var isAsyncLambda = parenthesized.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsyncKeyword);
            return parenthesized.Block != null
                ? JsExpr.ArrowBlock(parameters, context.Converter.ConvertBlock(parenthesized.Block), isAsyncLambda)
                : ExpressionBody(parameters, parenthesized.ExpressionBody!, isAsyncLambda, context);
        }
        
        if (node is SimpleLambdaExpressionSyntax simple)
        {
            var param = Typed(simple.Parameter.Identifier.Text.ToJsIdentifier(), simple.Parameter, context);
            var isAsyncLambda = simple.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AsyncKeyword);
            return simple.Block != null
                ? JsExpr.ArrowBlock(param, context.Converter.ConvertBlock(simple.Block), isAsyncLambda)
                : ExpressionBody(param, simple.ExpressionBody!, isAsyncLambda, context);
        }
        
        return JsExpr.ArrowBlock("", "{}");
    }

    /// <summary>
    /// A concise lambda body, given a BLOCK when it binds pattern variables.
    /// <para>
    /// `x => Maybe(x) is { } y ? y : ""` converts to an assignment of `y` inside the condition, and
    /// a concise body has nowhere to declare it — so the name was assigned and never declared,
    /// which in a module (strict mode) is a ReferenceError the first time the lambda runs. The
    /// block is added only when there is something to declare, so every other lambda emits
    /// unchanged.
    /// </para>
    /// </summary>
    /// <summary>An expression body stays an expression — the writer parenthesizes an object
    /// literal there — unless it binds pattern variables, whose hoisted declarations need a block.</summary>
    private static JsExpr ExpressionBody(string parameters, ExpressionSyntax expression, bool isAsync,
        ConversionContext context)
    {
        var hoisted = PatternVariableScanner.Declarations(expression, context.TypeAnnotations);
        var body = context.Converter.ConvertIr(expression);
        if (hoisted.Length == 0) return JsExpr.Arrow(parameters, body, isAsync);

        var block = JsStatement.Block(new[] { JsStatement.Raw(hoisted), JsStatement.Return(body) });
        return JsExpr.ArrowBlock(parameters, JsStatementWriter.Write(block, context.Layout, context.Depth), isAsync);
    }

    /// <summary>The statements of a converted block, without its outer braces.</summary>
    private static string TrimBraces(string block)
    {
        var trimmed = block.Trim();
        return trimmed.StartsWith('{') && trimmed.EndsWith('}')
            ? trimmed[1..^1].Trim()
            : trimmed;
    }

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
