using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// General strategy for method invocations.
/// Handles:
/// - Console.WriteLine -> console.log
/// - String methods (Join, IsNullOrEmpty)
/// - Math methods
/// - Dictionary methods (TryGetValue)
/// - General method calls
/// </summary>
public class InvocationStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        return node is InvocationExpressionSyntax;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var methodExpression = invocation.Expression;
        var fullMethodName = methodExpression.ToString();
        var methodName = fullMethodName;

        if (methodExpression is MemberAccessExpressionSyntax memberAccess)
        {
            methodName = memberAccess.Name.Identifier.Text;
        }

        // 1. Resolve Arguments
        var argsList = new List<string>();
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword))
            {
                if (arg.Expression is DeclarationExpressionSyntax decl)
                {
                    argsList.Add(decl.Designation.ToString());
                }
                else
                {
                    argsList.Add(arg.Expression.ToString().Trim());
                }
            }
            else
            {
                argsList.Add(context.Converter.ConvertExpression(arg.Expression));
            }
        }
        var args = string.Join(", ", argsList);

        // 2. Semantic Resolution for Library Calls
        string? libraryMethodName = null;
        var symbol = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        if (symbol != null)
        {
            var containingType = symbol.ContainingType.ToDisplayString();
            if (containingType.StartsWith("global::")) containingType = containingType.Substring(8);
            libraryMethodName = $"{containingType}.{symbol.Name}";
        }

        // 3. Handle specific conversions
        
        // No-Op conversions (Collections)
        if (methodName == "ToList" || methodName == "ToArray")
        {
             if (methodExpression is MemberAccessExpressionSyntax access)
             {
                 return context.Converter.ConvertExpression(access.Expression);
             }
        }

        // List Methods
        if (methodName == "Add" || methodName == "AddRange")
        {
             if (methodExpression is MemberAccessExpressionSyntax access)
             {
                 var caller = context.Converter.ConvertExpression(access.Expression);
                 return $"{caller}.push({args})";
             }
        }
        
        // AddChild -> Children.push
        if (methodName == "AddChild")
        {
             if (methodExpression is MemberAccessExpressionSyntax access)
             {
                 var caller = context.Converter.ConvertExpression(access.Expression);
                 return $"{caller}.Children.push({args})";
             }
        }
        
        // List.Insert -> splice
        if (methodName == "Insert" && argsList.Count >= 2)
        {
             if (methodExpression is MemberAccessExpressionSyntax access)
             {
                 var caller = context.Converter.ConvertExpression(access.Expression);
                 return $"{caller}.splice({argsList[0]}, 0, {argsList[1]})";
             }
        }

        // Console.WriteLine
        if (methodName == "WriteLine")
        {
            if (symbol != null && context.SemanticHelper.IsSystemConsole(symbol.ContainingType))
            {
                return $"console.log({args})";
            }
            // Fallback check
            if (context.SemanticModel == null && (fullMethodName.StartsWith("Console.") || fullMethodName.StartsWith("console.")))
            {
                return $"console.log({args})";
            }
        }

        // String.Join
        if (methodName == "Join" && (libraryMethodName == "System.String.Join" || fullMethodName.Contains("String.Join") || fullMethodName.Contains("string.Join")))
        {
            if (argsList.Count >= 2)
            {
                return $"{argsList[1]}.join({argsList[0]})";
            }
        }

        // String.IsNullOrEmpty / IsNullOrWhiteSpace
        if (methodName == "IsNullOrEmpty" || methodName == "IsNullOrWhiteSpace")
        {
             bool isStringMethod = libraryMethodName?.StartsWith("System.String") == true || fullMethodName.Contains("String") || fullMethodName.Contains("string");
             if (isStringMethod && argsList.Count > 0)
             {
                 var arg = argsList[0];
                 if (methodName == "IsNullOrWhiteSpace")
                     return $"(!{arg} || {arg}.trim() === '')";
                 return $"(!{arg} || {arg} === '')";
             }
        }

        // Math Methods
        if (libraryMethodName?.StartsWith("System.Math") == true || fullMethodName.StartsWith("Math."))
        {
            if (methodName == "Clamp" && argsList.Count >= 3)
            {
                 return $"Math.min(Math.max({argsList[0]}, {argsList[1]}), {argsList[2]})";
            }
            return $"Math.{ToCamelCase(methodName)}({args})";
        }

        // Common String Methods
        if (methodName == "ToLower" || methodName == "ToLowerInvariant")
        {
            if (methodExpression is MemberAccessExpressionSyntax access)
            {
                var caller = context.Converter.ConvertExpression(access.Expression);
                return $"{caller}.toLowerCase()";
            }
        }
        if (methodName == "ToUpper" || methodName == "ToUpperInvariant")
        {
            if (methodExpression is MemberAccessExpressionSyntax access)
            {
                var caller = context.Converter.ConvertExpression(access.Expression);
                return $"{caller}.toUpperCase()";
            }
        }
        if (methodName == "Trim")
        {
            if (methodExpression is MemberAccessExpressionSyntax access)
            {
                var caller = context.Converter.ConvertExpression(access.Expression);
                return $"{caller}.trim()";
            }
        }
        
        // ToString
        if (methodName == "ToString")
        {
             if (methodExpression is MemberAccessExpressionSyntax access)
             {
                 var caller = context.Converter.ConvertExpression(access.Expression);
                 // Safe conversion: String(val) handles null/undefined
                 return $"String({caller})";
             }
        }

        // HtmlNode.Text
        if (libraryMethodName == "eQuantic.UI.Core.HtmlNode.Text" || fullMethodName.EndsWith("HtmlNode.Text"))
        {
             return $"{{ tag: '#text', textContent: {argsList[0]} }}";
        }

        // Dictionary ContainsKey
        if (methodName == "ContainsKey" && argsList.Count > 0)
        {
             if (methodExpression is MemberAccessExpressionSyntax access)
             {
                 var caller = context.Converter.ConvertExpression(access.Expression);
                 var key = argsList[0];
                 return $"{key} in {caller}";
             }
        }

        // Dictionary TryGetValue
        if ((methodName == "TryGetValue" || methodName == "TryGetValueOrDefault") && argsList.Count > 1)
        {
             // Assumes MemberAccess like dict.TryGetValue(key, out var val)
             if (methodExpression is MemberAccessExpressionSyntax access)
             {
                 var caller = context.Converter.ConvertExpression(access.Expression);
                 var key = argsList[0];
                 var outVar = argsList[1]; // The out variable name
                 return $"({outVar} = {caller}[{key}]) !== undefined";
             }
        }

        // Service Provider Methods
        if (methodName == "GetService" || methodName == "GetRequiredService")
        {
            if (methodExpression is MemberAccessExpressionSyntax access) 
            {
               var caller = context.Converter.ConvertExpression(access.Expression);
                
               // Check if generic
               if (access.Name is GenericNameSyntax genericName && genericName.TypeArgumentList.Arguments.Count > 0)
               {
                   var typeArg = genericName.TypeArgumentList.Arguments[0];
                   var typeName = typeArg.ToString();
                   // Convert "GetService<T>()" to "getService('T')"
                   return $"{caller}.{ToCamelCase(methodName)}('{typeName}')";
               }
               
               // Non-generic GetService(typeof(T))
               if (argsList.Count > 0)
               {
                   var arg = argsList[0];
                   if (arg.StartsWith("typeof(")) 
                   {
                       // Extract T from typeof(T)
                       var typeName = arg.Substring(7, arg.Length - 8);
                       return $"{caller}.{ToCamelCase(methodName)}('{typeName}')";
                   }
                   return $"{caller}.{ToCamelCase(methodName)}({arg})";
               }
            }
        }

        // 4. General Method Call
        if (methodExpression is MemberAccessExpressionSyntax genAccess)
        {
            var caller = context.Converter.ConvertExpression(genAccess.Expression);
            
            // Local method call (this.Method)
            // If SemanticModel says it's not static and belongs to component/state
            bool isLocal = false;
            if (symbol != null && !symbol.IsStatic)
            {
                 // Crude check for component methods
                 isLocal = true; 
            }
            else if (context.SemanticModel == null && char.IsUpper(methodName[0])) 
            {
                isLocal = true; // Heuristic
            }

            if (isLocal)
            {
                return $"{caller}.{ToCamelCase(methodName)}({args})";
            }
            
            return $"{caller}.{ToCamelCase(methodName)}({args})";
        }

        // Direct invocation (Function() -> function())
        bool needsThis = false;
        
        // Use semantic resolution if available
        if (symbol != null && !symbol.IsStatic)
        {
            if (symbol.ContainingType != null)
            {
                // If it's a member of a class and not an extension method call (though extensions are usually MemberAccess)
                needsThis = true;
            }
        }
        
        // Heuristic fallback if we are in a class context
        if (!needsThis && !string.IsNullOrEmpty(context.CurrentClassName))
        {
            // If the method name starts with Uppercase, it's likely a member method
            if (char.IsUpper(methodName[0]) && symbol == null)
            {
                 needsThis = true;
            }
        }

        if (needsThis)
        {
            return $"this.{ToCamelCase(methodName)}({args})";
        }

        return $"{ToCamelCase(methodName)}({args})";
    }

    private string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 1; // Low priority (fallback for generic invocations)
}
