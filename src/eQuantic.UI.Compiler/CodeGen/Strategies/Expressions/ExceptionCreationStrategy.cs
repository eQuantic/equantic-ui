using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// <c>new T(…)</c>, and the target-typed <c>new(…)</c>, where T is an exception: the browser's
/// exception, an <c>Error</c> carrying T and every type it derives from
/// (<see cref="ExceptionTypes.Construction(INamedTypeSymbol, JsExpr?, ConversionContext)"/>), so a typed
/// <c>catch</c> can tell it from another. Decided by the created type's SYMBOL: an exception of the
/// app's own is one whatever its name, and <c>Exception e = new("x")</c> is one too, where it
/// constructed a class named <c>Exception</c> that JavaScript does not have.
/// <para>
/// The message is the argument bound to the constructor's <c>message</c> parameter (signatures differ:
/// <c>ArgumentException(message, paramName)</c> and <c>ArgumentOutOfRangeException(paramName,
/// message)</c>), the last argument where the constructor cannot be asked, and the one argument of a
/// constructor that takes one. A parameter name, an inner exception and an initializer are not carried.
/// </para>
/// </summary>
public class ExceptionCreationStrategy : IExpressionIrStrategy
{
    /// <summary>Ahead of <see cref="ObjectCreationStrategy"/>, which builds every other creation.</summary>
    public int Priority => 6;

    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is BaseObjectCreationExpressionSyntax
        && ExceptionTypes.Is(context.SemanticHelper.GetType(node));

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var creation = (BaseObjectCreationExpressionSyntax)node;
        var type = (INamedTypeSymbol)context.SemanticHelper.GetType(node)!;
        return ExceptionTypes.Construction(type, Message(creation, context), context);
    }

    /// <summary>The message argument, converted, or null where none was written. Internal for the
    /// creation the model cannot see, which <see cref="ObjectCreationStrategy"/> builds by name.</summary>
    internal static JsExpr? Message(BaseObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        if (creation.ArgumentList?.Arguments is not { Count: > 0 } arguments) return null;
        if (arguments.Count == 1) return context.Converter.ConvertIr(arguments[0].Expression);

        if (context.SemanticHelper.GetSymbol(creation) is IMethodSymbol constructor)
        {
            for (var i = 0; i < arguments.Count && i < constructor.Parameters.Length; i++)
            {
                var parameter = arguments[i].NameColon?.Name.Identifier.ValueText is { } named
                    ? constructor.Parameters.FirstOrDefault(p => p.Name == named)
                    : constructor.Parameters[i];
                if (parameter?.Name == "message")
                    return context.Converter.ConvertIr(arguments[i].Expression);
            }
        }

        return context.Converter.ConvertIr(arguments[^1].Expression);
    }
}
