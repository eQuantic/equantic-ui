using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.UI;

/// <summary>
/// Generic strategy for runtime utility classes from eQuantic.UI.Core.* namespaces.
/// Automatically detects types that have TypeScript equivalents in @equantic/runtime.
/// Maps C# API (PascalCase) to TypeScript runtime API (camelCase).
///
/// This strategy is namespace-driven: any type from eQuantic.UI.Core.* namespaces
/// is assumed to have a runtime equivalent unless explicitly excluded.
/// </summary>
public class RuntimeUtilityStrategy : IConversionStrategy
{
    // Core namespaces that contain runtime utilities
    private static readonly HashSet<string> RuntimeNamespaces = new()
    {
        "eQuantic.UI.Core.Styling",    // ClassBuilder, StyleBuilder
        "eQuantic.UI.Core.Utils",      // Future: formatters, validators
        "eQuantic.UI.Core.Helpers"     // Future: other helpers
    };

    // Types to explicitly exclude (e.g., base classes, interfaces that don't have runtime equivalents)
    private static readonly HashSet<string> ExcludedTypes = new()
    {
        // Add types here that should NOT be converted to runtime imports
    };

    public int Priority => 50; // Higher priority to intercept before generic invocation

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        var symbolInfo = context.SemanticModel?.GetSymbolInfo(invocation);
        if (symbolInfo?.Symbol is not IMethodSymbol methodSymbol)
            return false;

        var containingType = methodSymbol.ContainingType;
        if (containingType == null)
            return false;

        var typeName = containingType.Name;

        // Explicitly excluded types
        if (ExcludedTypes.Contains(typeName))
            return false;

        // Check if the type is from a runtime namespace
        var namespaceName = containingType.ContainingNamespace?.ToDisplayString();
        return namespaceName != null && RuntimeNamespaces.Contains(namespaceName);
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var symbolInfo = context.SemanticModel?.GetSymbolInfo(invocation);

        if (symbolInfo?.Symbol is not IMethodSymbol methodSymbol)
            return invocation.ToString();

        var typeName = methodSymbol.ContainingType.Name;
        var methodName = methodSymbol.Name;
        var tsMethodName = ToCamelCase(methodName);

        // Track that this type is used (TypeScriptEmitter will handle import)
        context.UsedHelpers.Add(typeName);

        // Handle static method calls (e.g., ClassBuilder.Create())
        if (methodSymbol.IsStatic)
        {
            var arguments = ConvertArguments(invocation, context);
            return $"{typeName}.{tsMethodName}({arguments})";
        }

        // Handle instance methods (chaining): builder.Add(...), builder.When(...), etc.
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var receiver = context.Converter.ConvertExpression(memberAccess.Expression);
            var arguments = ConvertArguments(invocation, context);
            return $"{receiver}.{tsMethodName}({arguments})";
        }

        // Fallback
        return invocation.ToString();
    }

    private string ConvertArguments(InvocationExpressionSyntax invocation, ConversionContext context)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
            return "";

        var args = invocation.ArgumentList.Arguments
            .Select(arg => context.Converter.ConvertExpression(arg.Expression));

        return string.Join(", ", args);
    }

    private static string ToCamelCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        // Convert PascalCase to camelCase: Create -> create, Add -> add
        return char.ToLowerInvariant(pascalCase[0]) + pascalCase.Substring(1);
    }
}
