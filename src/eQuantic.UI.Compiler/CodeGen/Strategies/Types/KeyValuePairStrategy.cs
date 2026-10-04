using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// A <c>KeyValuePair</c> built by the app: <c>new KeyValuePair&lt;K, V&gt;(key, value)</c>,
/// <c>KeyValuePair.Create(key, value)</c> and the parameterless constructor, which is the pair of the
/// two defaults. Each is the shape a dictionary yields (<c>$eq.collections.pair</c>), which destructures
/// as <c>[key, value]</c> and reads <c>.key</c> and <c>.value</c>: a pair built by hand named a class
/// nothing defines, where one a dictionary yielded worked (#433). Decided by the bound symbol.
/// </summary>
public class KeyValuePairStrategy : IExpressionIrStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context) => node switch
    {
        BaseObjectCreationExpressionSyntax creation => IsPair(context.SemanticHelper.GetType(creation)),
        InvocationExpressionSyntax invocation => context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol
        {
            Name: "Create", IsStatic: true, ContainingType: { Name: "KeyValuePair", IsGenericType: false } type,
        } && type.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic",
        _ => false,
    };

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        var operation = context.SemanticHelper.GetOperation(node);
        var arguments = operation switch
        {
            IObjectCreationOperation creation => creation.Arguments,
            IInvocationOperation call => call.Arguments,
            _ => default,
        };
        // The parameterless constructor (or `new()` with none) is the pair of the two defaults.
        if (arguments.IsDefaultOrEmpty && context.SemanticHelper.GetType(node) is INamedTypeSymbol { TypeArguments: [var keyType, var valueType] })
            return JsExpr.Call(JsExpr.Identifier(Eq.Pair),
                JsExpr.Literal(context.Converter.DefaultOf(keyType)), JsExpr.Literal(context.Converter.DefaultOf(valueType)));
        // The key and the value by the parameter each binds, in the order they are written.
        var ordered = arguments.Where(argument => argument.ArgumentKind == ArgumentKind.Explicit)
            .OrderBy(argument => argument.Syntax.SpanStart)
            .ToList();
        var parts = ordered.Select(argument => context.Converter.ConvertIr((ExpressionSyntax)argument.Value.Syntax)).ToList();
        var key = ordered.FindIndex(argument => argument.Parameter?.Ordinal == 0);
        var value = ordered.FindIndex(argument => argument.Parameter?.Ordinal == 1);
        if (key < 0 || value < 0) return JsExpr.Opaque(context.Unhandled(node, "KeyValuePair"));
        return JsExpr.Template($"{Eq.Pair}({{{key}}}, {{{value}}})", parts, context.TypeAnnotations);
    }

    private static bool IsPair(ITypeSymbol? type) =>
        type is INamedTypeSymbol { OriginalDefinition: { MetadataName: "KeyValuePair`2" } definition }
        && definition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic";

    public int Priority => 12;
}
