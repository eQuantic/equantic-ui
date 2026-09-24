using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Invocation;

/// <summary>
/// Strategy for Dictionary method invocations.
/// Handles:
/// - ContainsKey(key) → the object's own key
/// - TryGetValue(key, out var val), GetValueOrDefault(key[, fallback]) → see DictionaryLookup
/// - Add(key, value) → obj[key] = value
/// - Remove(key) → delete obj[key]
/// - Clear() → Object.keys(obj).forEach(($k) => delete obj[$k]), obj evaluated once
/// - Keys → Object.keys(obj)
/// - Values → Object.values(obj)
/// </summary>
public class DictionaryStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        // Handle method invocations
        if (node is InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                return false;

            var methodName = memberAccess.Name.Identifier.Text;
            if (methodName is not ("ContainsKey" or "TryGetValue" or "GetValueOrDefault" or "Add" or "Remove" or "Clear"))
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

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
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
            return context.Unhandled(node, "Dictionary");

        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.Text;
        var args = invocation.ArgumentList.Arguments;

        // The two lookups answer as .NET's do, and evaluate each argument once — DictionaryLookup.
        if (methodName == "TryGetValue" && args.Count > 1)
            return DictionaryLookup.PlainObject.TryGetValue(invocation, context);

        if (methodName == "GetValueOrDefault" && args.Count > 0)
            return DictionaryLookup.PlainObject.GetValueOrDefault(invocation, context);

        var receiver = context.Converter.ConvertIr(memberAccess.Expression);
        var caller = receiver.ToString();

        // ContainsKey(key) asks for the object's OWN key, never `key in obj`: `in` walks the
        // prototype chain, so an empty dictionary answers true for "constructor", "toString" and
        // every other Object.prototype member. A key that comes from user data is exactly where
        // that bites.
        //
        // hasOwnProperty.call, not Object.hasOwn: the runtime targets ES2021, and this is the form
        // it already uses for the same question (utils/equals, router/current-route).
        if (methodName == "ContainsKey" && args.Count > 0)
        {
            var key = context.Converter.ConvertExpression(args[0].Expression);
            return $"Object.prototype.hasOwnProperty.call({caller}, {key})";
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

        // Clear() deletes every own key of ONE object: the receiver is evaluated once (the writer
        // binds a part the template names twice), where the text used to name it again for every
        // key it deleted. And the arrow's parameter is `$k`, which no C# name can be: a dictionary
        // called `k` was shadowed by it, and `k.Clear()` deleted nothing.
        if (methodName == "Clear" && args.Count == 0)
        {
            return JsExpr.Template("Object.keys({0}).forEach(($k) => delete {0}[$k])", [receiver], context.TypeAnnotations);
        }

        // Fallback
        var argsStr = string.Join(", ", args.Select(a => context.Converter.ConvertExpression(a.Expression)));
        return $"{caller}.{methodName.ToCamelCase()}({argsStr})";
    }

    public int Priority => 20; // Higher than ListMethodStrategy (15) to handle Dictionary.Add vs List.Add
}
