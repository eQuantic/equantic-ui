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
        var createdType = context.SemanticHelper.GetType(creation);

        // Records and user structs are emitted as named JS classes (they carry instance methods) —
        // construct via `new`, mapping positional args and any object initializer onto the constructor.
        if (createdType is { IsRecord: true }
            || (createdType is { TypeKind: TypeKind.Struct } && createdType.IsStructuralValueType()))
        {
            return BuildValueTypeConstruction(creation, createdType, context);
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
    /// Builds a <c>new T(...)</c> construction for a record/struct, mapping positional arguments and any
    /// object initializer (<c>{ Name = … }</c>) onto the constructor's positional value members (in the
    /// type's declaration order). Members left unset before the last supplied one get their default
    /// literal; trailing unset members are omitted (the constructor's parameter defaults cover them).
    /// </summary>
    private static string BuildValueTypeConstruction(ObjectCreationExpressionSyntax creation, ITypeSymbol type, ConversionContext context)
    {
        var declSyntax = type.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();

        // No declaration available (external type) — best effort: positional args as-is.
        if (declSyntax == null)
        {
            var simple = creation.ArgumentList == null
                ? string.Empty
                : string.Join(", ", creation.ArgumentList.Arguments.Select(a => context.Converter.ConvertExpression(a.Expression)));
            return $"new {type.Name}({simple})";
        }

        var members = declSyntax.ValueMembers();
        var values = new string?[members.Count];

        // Positional constructor arguments fill the leading members.
        if (creation.ArgumentList != null)
        {
            var args = creation.ArgumentList.Arguments;
            for (var i = 0; i < args.Count && i < members.Count; i++)
                values[i] = context.Converter.ConvertExpression(args[i].Expression);
        }

        // Object initializer `{ Name = …, Age = … }` fills the named members by position.
        if (creation.Initializer != null)
        {
            foreach (var expr in creation.Initializer.Expressions)
            {
                if (expr is AssignmentExpressionSyntax assignment)
                {
                    var key = assignment.Left.ToString().ToCamelCase();
                    var idx = -1;
                    for (var i = 0; i < members.Count; i++) if (members[i].Js == key) { idx = i; break; }
                    if (idx >= 0) values[idx] = context.Converter.ConvertExpression(assignment.Right);
                }
            }
        }

        var lastSet = -1;
        for (var i = 0; i < values.Length; i++) if (values[i] != null) lastSet = i;

        var ctorArgs = new List<string>();
        for (var i = 0; i <= lastSet; i++) ctorArgs.Add(values[i] ?? members[i].Default);

        return $"new {type.Name}({string.Join(", ", ctorArgs)})";
    }

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
