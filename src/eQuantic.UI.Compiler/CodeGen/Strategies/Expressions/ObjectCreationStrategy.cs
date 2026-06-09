using System.Collections.Generic;
using System.Linq;
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

        // User records / structs are value-shaped: represent as a plain object (consistent with
        // anonymous types, object initializers, and value-based Distinct/equality). Positional args map
        // to the constructor's parameter names; an object initializer contributes named members.
        if (SemanticHelper.IsStructuralValueType(context.SemanticHelper.GetType(creation)))
        {
            return BuildValueObject(creation, context);
        }

        var arguments = "";
        
        if (creation.ArgumentList != null && creation.ArgumentList.Arguments.Count > 0)
        {
             arguments = string.Join(", ", creation.ArgumentList.Arguments.Select(a => context.Converter.ConvertExpression(a.Expression)));
        }

        var initializer = "";
        if (creation.Initializer != null)
        {
            initializer = context.Converter.ConvertExpression(creation.Initializer);
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

        // Special handling for Collections (handle both short and fully-qualified names)
        if (typeName.StartsWith("List<") || typeName.Contains(".List<")
            || typeName.StartsWith("IEnumerable<") || typeName.Contains(".IEnumerable<"))
        {
            return string.IsNullOrEmpty(arguments) || arguments == "{}" ? "[]" : arguments;
        }
        if (typeName.StartsWith("Dictionary<") || typeName.Contains(".Dictionary<"))
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

        // Exception types -> JavaScript Error
        if (typeName.EndsWith("Exception") || typeName == "Exception")
        {
            return $"new Error({arguments})";
        }

        return $"new {typeName}({arguments})";
    }

    /// <summary>
    /// Builds the plain-object form of a record/struct construction. Positional arguments are named
    /// after the matched constructor's parameters (camelCased); named arguments and an object
    /// initializer (<c>{ X = 1 }</c>) contribute their members directly.
    /// </summary>
    private static string BuildValueObject(ObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        var parts = new List<string>();
        var ctor = context.SemanticHelper.GetSymbol(creation) as IMethodSymbol;

        if (creation.ArgumentList != null)
        {
            var args = creation.ArgumentList.Arguments;
            for (var i = 0; i < args.Count; i++)
            {
                var value = context.Converter.ConvertExpression(args[i].Expression);
                string name;
                if (args[i].NameColon != null)
                    name = args[i].NameColon!.Name.Identifier.Text;          // explicit `x: value`
                else if (ctor != null && i < ctor.Parameters.Length)
                    name = ctor.Parameters[i].Name;                          // positional -> param name
                else
                    name = $"item{i + 1}";                                   // fallback (untyped)
                parts.Add($"{Camel(name)}: {value}");
            }
        }

        if (creation.Initializer != null)
        {
            foreach (var expr in creation.Initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax assignment)
                {
                    var value = context.Converter.ConvertExpression(assignment.Right);
                    parts.Add($"{Camel(assignment.Left.ToString())}: {value}");
                }
            }
        }

        return parts.Count == 0 ? "{}" : "{ " + string.Join(", ", parts) + " }";
    }

    private static string Camel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private string ConvertImplicit(ImplicitObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        if (creation.Initializer != null)
        {
             return context.Converter.ConvertExpression(creation.Initializer);
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
