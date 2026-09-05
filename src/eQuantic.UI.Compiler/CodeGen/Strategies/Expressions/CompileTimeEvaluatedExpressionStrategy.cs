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
    // The evaluator binds to ONE semantic model. The strategy instance outlives the file it is
    // converting (the converter registers it once and drives every component through it), so a
    // `??=` pinned the FIRST file's model forever: every [CompileTimeEvaluate] expression in the
    // second file onward silently failed evaluation and took the failure path.
    private SemanticModel? _evaluatorModel;

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

        if (!IsCompileTimeEvaluatable(typeInfo.Type))
            return false;

        // Claim the node only when the claim MEANS something: a successful evaluation, or a failure
        // the attribute asked to handle here (Error/EmitNull). A failure whose configured fallback
        // is "EmitRuntimeCode" is declined instead, so the normal pipeline emits the actual runtime
        // code — the previous behaviour returned the raw C# text and called it a fallback.
        _pendingNode = expression;
        _pendingValue = Evaluator(context).TryEvaluate(expression);
        if (_pendingValue != null) return true;

        var config = GetEvaluationConfig(typeInfo.Type);
        if (config.FallbackBehavior is "Error" or "EmitNull") return true;

        if (config.WarnOnFailure)
        {
            Console.WriteLine(
                $"Warning: Could not evaluate compile-time expression at {expression.GetLocation()}. " +
                $"Falling back to runtime code. Expression: {expression}");
        }
        return false;
    }

    // CanConvert and Convert run back-to-back on the same node (FindStrategy → Convert), so the
    // evaluation done while DECIDING is handed to the emission instead of running twice.
    private ExpressionSyntax? _pendingNode;
    private string? _pendingValue;

    private CompileTimeEvaluator Evaluator(ConversionContext context)
    {
        if (_evaluator == null || !ReferenceEquals(_evaluatorModel, context.SemanticModel))
        {
            _evaluator = new CompileTimeEvaluator(context.SemanticModel!);
            _evaluatorModel = context.SemanticModel;
        }
        return _evaluator;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var expression = (ExpressionSyntax)node;

        var evaluatedValue = ReferenceEquals(_pendingNode, expression)
            ? _pendingValue
            : Evaluator(context).TryEvaluate(expression);

        if (evaluatedValue != null)
        {
            // Success: emit as string literal
            return $"'{EscapeString(evaluatedValue)}'";
        }

        // Evaluation failed and the attribute asked for Error/EmitNull (EmitRuntimeCode never
        // reaches Convert — CanConvert declines it so the normal pipeline emits the runtime code).
        var typeInfo = context.SemanticModel!.GetTypeInfo(expression);
        var config = GetEvaluationConfig(typeInfo.Type!);

        return HandleEvaluationFailure(expression, context, config);
    }

    private bool IsCompileTimeEvaluatable(ITypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes()
            .Any(attr =>
                attr.AttributeClass?.Name == "CompileTimeEvaluateAttribute" &&
                attr.AttributeClass.ContainingNamespace.ToDisplayString() == "eQuantic.UI.Web.Styling");
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

            default:
                // "EmitRuntimeCode" failures are declined in CanConvert so the normal pipeline
                // emits the actual runtime code; anything still landing here is a bug, not a path.
                return context.Unhandled(expression, "compile-time evaluation");
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
