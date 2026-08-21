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

        if (context.SemanticHelper.GetSymbol(invocation) is not IMethodSymbol methodSymbol)
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
        if (context.SemanticHelper.GetSymbol(invocation) is not IMethodSymbol methodSymbol)
            return context.Unhandled(invocation, "runtime utility");

        var typeName = methodSymbol.ContainingType.Name;
        var methodName = methodSymbol.Name;
        var tsMethodName = methodName.ToCamelCase();

        // Emitting $eq.* requires the single $eq import (resolved by the page import map).
        context.UsedHelpers.Add(Eq.Import);

        // Static method calls (e.g. StyleBuilder.Create()) start the chain on the $eq.css namespace:
        // StyleBuilder -> $eq.css.styleBuilder.
        if (methodSymbol.IsStatic)
        {
            var arguments = ConvertArguments(invocation, context);
            return $"$eq.css.{typeName.ToCamelCase()}.{tsMethodName}({arguments})";
        }

        // Handle instance methods (chaining): builder.Add(...), builder.When(...), etc.
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var receiver = context.Converter.ConvertExpression(memberAccess.Expression);
            var arguments = ConvertArguments(invocation, context);
            return $"{receiver}.{tsMethodName}({arguments})";
        }

        // A shape the two branches above don't cover — `builder?.When(…)` arrives as a member
        // BINDING, not a member access — used to ship raw C# here.
        return context.Unhandled(invocation, "runtime utility");
    }

    private string ConvertArguments(InvocationExpressionSyntax invocation, ConversionContext context)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
            return "";

        var args = invocation.ArgumentList.Arguments
            .Select(arg => context.Converter.ConvertExpression(arg.Expression));

        return string.Join(", ", args);
    }

}
