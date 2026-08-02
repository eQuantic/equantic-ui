using eQuantic.UI.Compiler.Analysis;
using eQuantic.UI.Compiler.CodeGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for handling expressions of types marked with [CompileTimeEvaluate].
/// Evaluates the expression at compile-time and emits the constant string result.
/// </summary>
public class CompileTimeEvaluatedExpressionStrategy : IConversionStrategy
{
    private CompileTimeEvaluator? _evaluator;

    public int Priority => 100; // Higher priority to check before other strategies

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not ExpressionSyntax expression)
            return false;

        // Need semantic model for compile-time evaluation
        if (context.SemanticModel == null)
            return false;

        // Check if the expression's type is marked with [CompileTimeEvaluate]
        if (!context.SemanticHelper.Knows(expression)) return false;
        var typeInfo = context.SemanticModel.GetTypeInfo(expression);
        if (typeInfo.Type == null)
            return false;

        return IsCompileTimeEvaluatable(typeInfo.Type);
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var expression = (ExpressionSyntax)node;

        // Initialize evaluator if needed
        _evaluator ??= new CompileTimeEvaluator(context.SemanticModel!);

        // Try to evaluate the expression
        var evaluatedValue = _evaluator.TryEvaluate(expression);

        if (evaluatedValue != null)
        {
            // Success: emit as string literal
            return $"'{EscapeString(evaluatedValue)}'";
        }

        // Evaluation failed - emit diagnostic and fall back
        var typeInfo = context.SemanticModel!.GetTypeInfo(expression);
        var config = GetEvaluationConfig(typeInfo.Type!);

        return HandleEvaluationFailure(expression, context, config);
    }

    private bool IsCompileTimeEvaluatable(ITypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes()
            .Any(attr =>
                attr.AttributeClass?.Name == "CompileTimeEvaluateAttribute" &&
                attr.AttributeClass.ContainingNamespace.ToDisplayString() == "eQuantic.UI.Core.Styling");
    }

    private CompileTimeEvaluateConfig GetEvaluationConfig(ITypeSymbol typeSymbol)
    {
        var attr = typeSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "CompileTimeEvaluateAttribute");

        if (attr == null)
            return new CompileTimeEvaluateConfig();

        var warnOnFailure = attr.NamedArguments
            .FirstOrDefault(a => a.Key == "WarnOnEvaluationFailure")
            .Value.Value as bool? ?? true;

        var fallbackBehavior = attr.NamedArguments
            .FirstOrDefault(a => a.Key == "FallbackBehavior")
            .Value.Value as string ?? "EmitRuntimeCode";

        return new CompileTimeEvaluateConfig
        {
            WarnOnFailure = warnOnFailure,
            FallbackBehavior = fallbackBehavior
        };
    }

    private string HandleEvaluationFailure(ExpressionSyntax expression, ConversionContext context, CompileTimeEvaluateConfig config)
    {
        switch (config.FallbackBehavior)
        {
            case "Error":
                throw new Exception(
                    $"Cannot evaluate compile-time expression at {expression.GetLocation()}. " +
                    $"Expression: {expression}");

            case "EmitNull":
                return "''";  // Empty string

            case "EmitRuntimeCode":
            default:
                if (config.WarnOnFailure)
                {
                    Console.WriteLine(
                        $"Warning: Could not evaluate compile-time expression at {expression.GetLocation()}. " +
                        $"Falling back to runtime code. Expression: {expression}");
                }

                // Fall back to normal expression conversion
                // Note: This may cause issues if the expression uses runtime values
                // but it's better than failing completely
                return expression.ToString();
        }
    }

    private string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}

public class CompileTimeEvaluateConfig
{
    public bool WarnOnFailure { get; set; } = true;
    public string FallbackBehavior { get; set; } = "EmitRuntimeCode";
}
