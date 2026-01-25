using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// Strategy for object creation (new T() or new()).
/// Handles:
/// - List<T> -> []
/// - Dictionary<K,V> -> {}
/// - HtmlNode -> {} (UI config)
/// - UI Components -> new Component(config) or just config
/// </summary>
public class ObjectCreationStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is ObjectCreationExpressionSyntax || node is ImplicitObjectCreationExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        if (node is ObjectCreationExpressionSyntax objCreation)
        {
            return ConvertExplicit(objCreation, context);
        }
        else if (node is ImplicitObjectCreationExpressionSyntax implicitCreation)
        {
            return ConvertImplicit(implicitCreation, context);
        }
        throw new InvalidOperationException("Invalid node type");
    }

    private string ConvertExplicit(ObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var typeName = creation.Type.ToString();
        var arguments = "";
        
        if (creation.ArgumentList != null && creation.ArgumentList.Arguments.Count > 0)
        {
             arguments = string.Join(", ", creation.ArgumentList.Arguments.Select(a => context.Converter.ConvertExpression(a.Expression)));
        }

        var initializer = "";
        if (creation.Initializer != null)
        {
            initializer = context.Converter.ConvertInitializer(creation.Initializer);
            // Append initializer to arguments if likely a UI component
            if (string.IsNullOrEmpty(arguments))
            {
                arguments = initializer;
            }
            else
            {
                arguments += ", " + initializer;
            }
        }

        // Special handling for Collections
        if (typeName.StartsWith("List<") || typeName.StartsWith("IEnumerable<"))
        {
            // If argument is collection initializer { ... }, it's converted to [ ... ] by ConvertInitializer if it's a CollectionInitializer
            // But if ConvertInitializer returns { ... } format, we might need adjustment? 
            // Actually BaseConverter.ConvertInitializer handles Array/Collection initializers by returning [ ... ].
            return string.IsNullOrEmpty(arguments) || arguments == "{}" ? "[]" : arguments;
        }
        if (typeName.StartsWith("Dictionary<"))
        {
            return string.IsNullOrEmpty(arguments) || arguments == "[]" ? "{}" : arguments;
        }
        
        // HtmlNode -> Plain Object
        if (typeName == "HtmlNode")
        {
            return string.IsNullOrEmpty(arguments) ? "{}" : arguments;
        }
        
        // RenderContext -> Mock or Plain Object (since it's a TS interface)
        if (typeName == "RenderContext")
        {
            return "{ getService: () => null }";
        }

        return $"new {typeName}({arguments})";
    }

    private string ConvertImplicit(ImplicitObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        if (creation.Initializer != null)
        {
             return context.Converter.ConvertInitializer(creation.Initializer);
        }

        // Try to get type from semantic model
        var symbol = context.SemanticHelper.GetSymbol(creation);
        if (symbol is IMethodSymbol ms)
        {
            var typeName = ms.ContainingType.ToDisplayString();
            if (typeName.Contains("List<") || typeName.Contains("IEnumerable<") || typeName.Contains("Collection<"))
            {
                return "[]";
            }
            if (typeName.Contains("Dictionary<"))
            {
                return "{}";
            }
        }

        // Fallback to ExpectedType hint
        if (context.ExpectedType != null)
        {
            if (context.ExpectedType.Contains("List<") || context.ExpectedType.Contains("IEnumerable<") || context.ExpectedType.EndsWith("[]"))
            {
                return "[]";
            }
            if (context.ExpectedType.Contains("Dictionary<") || context.ExpectedType.Contains("IDictionary<"))
            {
                return "{}";
            }
        }
        
        return "{}";
    }

    public int Priority => 5;
}
