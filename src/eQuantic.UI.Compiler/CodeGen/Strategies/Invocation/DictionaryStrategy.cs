using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Invocation;

/// <summary>
/// Strategy for Dictionary method invocations.
/// Handles:
/// - ContainsKey(key) → key in obj
/// - TryGetValue(key, out var val) → (val = obj[key]) !== undefined
/// - Add(key, value) → obj[key] = value
/// - Remove(key) → delete obj[key]
/// - Clear() → Object.keys(obj).forEach(k => delete obj[k])
/// - Keys → Object.keys(obj)
/// - Values → Object.values(obj)
/// </summary>
public class DictionaryStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        // Handle method invocations
        if (node is InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                return false;

            var methodName = memberAccess.Name.Identifier.Text;
            if (methodName is not ("ContainsKey" or "TryGetValue" or "TryGetValueOrDefault" or "GetValueOrDefault" or "Add" or "Remove" or "Clear"))
                return false;

            // Check via semantic model if available
            var symbol = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
            if (symbol != null)
            {
                // Nullable<T>.GetValueOrDefault is NOT a dictionary method — leave it to NullableStrategy.
                if (symbol.ContainingType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
                    return false;

                var containingType = symbol.ContainingType.ToDisplayString();
                if (containingType.Contains("Dictionary") || containingType.Contains("IDictionary") || containingType.Contains("CollectionExtensions"))
                    return true;
                
                if (symbol.IsExtensionMethod && symbol.Parameters.Length > 0)
                {
                    var receiverType = symbol.Parameters[0].Type.ToDisplayString();
                    if (receiverType.Contains("Dictionary") || receiverType.Contains("IDictionary"))
                        return true;
                }
                
                // For GetValueOrDefault, we trust the name even if semantic check is ambiguous 
                // (could be a library method where containing type isn't clearly 'Dictionary')
                if (methodName == "GetValueOrDefault") return true;

                return false;
            }

            // Allow fallback for common patterns
            return true;
        }

        // Handle property access (Keys, Values)
        if (node is MemberAccessExpressionSyntax propertyAccess)
        {
            var propertyName = propertyAccess.Name.Identifier.Text;
            if (propertyName is not ("Keys" or "Values"))
                return false;

            // Check via semantic model if available
            var symbol = context.SemanticHelper.GetSymbol(propertyAccess);
            if (symbol != null)
            {
                var containingType = symbol.ContainingType?.ToDisplayString() ?? "";
                return containingType.Contains("Dictionary") ||
                       containingType.Contains("IDictionary");
            }

            return true;
        }

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        // Handle property access (Keys, Values)
        if (node is MemberAccessExpressionSyntax propertyAccess)
        {
            var propertyName = propertyAccess.Name.Identifier.Text;
            var propertyCaller = context.Converter.ConvertExpression(propertyAccess.Expression);

            if (propertyName == "Keys")
            {
                return $"Object.keys({propertyCaller})";
            }

            if (propertyName == "Values")
            {
                return $"Object.values({propertyCaller})";
            }
        }

        // Handle method invocations
        if (node is not InvocationExpressionSyntax invocation)
            return node.ToString();

        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.Text;
        var caller = context.Converter.ConvertExpression(memberAccess.Expression);

        var args = invocation.ArgumentList.Arguments;

        // ContainsKey(key) → key in obj
        if (methodName == "ContainsKey" && args.Count > 0)
        {
            var key = context.Converter.ConvertExpression(args[0].Expression);
            return $"({key} in {caller})";
        }

        // TryGetValue(key, out var val) → (val = obj[key]) !== undefined
        if ((methodName == "TryGetValue" || methodName == "TryGetValueOrDefault") && args.Count > 1)
        {
            var key = context.Converter.ConvertExpression(args[0].Expression);

            // Extract the out variable name
            string outVar;
            var outArg = args[1];
            if (outArg.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword))
            {
                if (outArg.Expression is DeclarationExpressionSyntax decl)
                {
                    outVar = decl.Designation.ToString();
                }
                else
                {
                    outVar = outArg.Expression.ToString().Trim();
                }
            }
            else
            {
                outVar = context.Converter.ConvertExpression(outArg.Expression);
            }

            return $"({outVar} = {caller}[{key}]) !== undefined";
        }

        // GetValueOrDefault(key, defaultValue?) → (caller[key] ?? defaultValue)
        if (methodName == "GetValueOrDefault" && args.Count > 0)
        {
            var key = context.Converter.ConvertExpression(args[0].Expression);
            var defaultValue = args.Count > 1 
                ? context.Converter.ConvertExpression(args[1].Expression)
                : "null";

            return $"({caller}[{key}] ?? {defaultValue})";
        }

        // Add(key, value) → obj[key] = value
        if (methodName == "Add" && args.Count >= 2)
        {
            var key = context.Converter.ConvertExpression(args[0].Expression);
            var value = context.Converter.ConvertExpression(args[1].Expression);
            return $"{caller}[{key}] = {value}";
        }

        // Remove(key) → delete obj[key]
        if (methodName == "Remove" && args.Count > 0)
        {
            var key = context.Converter.ConvertExpression(args[0].Expression);
            return $"delete {caller}[{key}]";
        }

        // Clear() → Object.keys(obj).forEach(k => delete obj[k])
        if (methodName == "Clear" && args.Count == 0)
        {
            return $"Object.keys({caller}).forEach(k => delete {caller}[k])";
        }

        // Fallback
        var argsStr = string.Join(", ", args.Select(a => context.Converter.ConvertExpression(a.Expression)));
        return $"{caller}.{ToCamelCase(methodName)}({argsStr})";
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    public int Priority => 20; // Higher than ListMethodStrategy (15) to handle Dictionary.Add vs List.Add
}
