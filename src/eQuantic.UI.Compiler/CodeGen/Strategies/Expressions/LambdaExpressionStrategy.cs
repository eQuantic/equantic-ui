using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for converting lambda expressions.
/// Handles:
/// - () => expr
/// - (a, b) => { stmt; }
/// - x => x + 1
/// </summary>
public class LambdaExpressionStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is LambdaExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        if (node is ParenthesizedLambdaExpressionSyntax parenthesized)
        {
            // The parameter TYPES come from the semantic model: a lambda handed to a config object
            // has nothing to infer from on the other side, and an untyped parameter is an error in
            // the runtime's own build rather than merely a missing type.
            var parameters = string.Join(", ", parenthesized.ParameterList.Parameters
                .Select(p => Typed(p.Identifier.Text.ToJsIdentifier(), p, context)));
            var body = parenthesized.Block != null
                ? context.Converter.ConvertBlock(parenthesized.Block)
                : ExpressionBody(parenthesized.ExpressionBody!, context);
            return $"({parameters}) => {body}";
        }
        
        if (node is SimpleLambdaExpressionSyntax simple)
        {
            var param = Typed(simple.Parameter.Identifier.Text.ToJsIdentifier(), simple.Parameter, context);
            var body = simple.Block != null
                ? context.Converter.ConvertBlock(simple.Block)
                : ExpressionBody(simple.ExpressionBody!, context);
            return $"({param}) => {body}";
        }
        
        return "() => {}";
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
    private static string ExpressionBody(ExpressionSyntax expression, ConversionContext context)
    {
        var hoisted = PatternVariableScanner.Declarations(expression, context.TypeAnnotations);
        var converted = context.Converter.ConvertExpression(expression);
        return hoisted.Length == 0 ? converted : $"{{ {hoisted}return {converted}; }}";
    }

    /// <summary>A parameter with its TS type, resolved through the model. Falls back to the bare
    /// name when nothing can be said — an untyped parameter beats a wrong one.</summary>
    private static string Typed(string name, ParameterSyntax parameter, ConversionContext context)
    {
        // A lambda parameter is DECLARED by the lambda, so it answers to GetDeclaredSymbol —
        // GetSymbolInfo is for references, and asking it here came back empty every time.
        var declared = parameter.Type is { } syntax
            ? context.SemanticHelper.GetSymbol(syntax) as ITypeSymbol
            : context.SemanticModel?.GetDeclaredSymbol(parameter) is IParameterSymbol symbol
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
