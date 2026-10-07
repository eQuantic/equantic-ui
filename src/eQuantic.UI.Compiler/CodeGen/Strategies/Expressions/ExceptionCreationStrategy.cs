using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// <c>new T(…)</c>, and the target-typed <c>new(…)</c>, where T is an exception of .NET's: the browser's
/// exception, an <c>Error</c> carrying T and every type it derives from
/// (<see cref="ExceptionTypes.Construction(IReadOnlyList{string}, BaseObjectCreationExpressionSyntax, ConversionContext)"/>),
/// so a typed <c>catch</c> can tell it from another. Decided by the created type's SYMBOL, and
/// <c>Exception e = new("x")</c> is one too, where it constructed a class named <c>Exception</c> that
/// JavaScript does not have. An exception class of the app's is a class, built as one is by
/// <see cref="ObjectCreationStrategy"/>, over the base the runtime gives its twin (#611).
/// <para>
/// Every argument is evaluated, in the order it is written, as C# evaluates a constructor's: the
/// message is the one bound to the constructor's <c>message</c> parameter (signatures differ:
/// <c>ArgumentException(message, paramName)</c> and <c>ArgumentOutOfRangeException(paramName,
/// message)</c>), the last argument where the constructor cannot be asked, and the one argument of a
/// constructor that takes one. The others run and are carried nowhere: a parameter name and an inner
/// exception are not carried yet (#558). An object initializer is applied to the exception once it is
/// built (#587).
/// </para>
/// </summary>
public class ExceptionCreationStrategy : IExpressionIrStrategy
{
    /// <summary>Ahead of <see cref="ObjectCreationStrategy"/>, which builds every other creation.</summary>
    public int Priority => 6;

    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is BaseObjectCreationExpressionSyntax
        && context.SemanticHelper.GetType(node) is var type
        && ExceptionTypes.Is(type) && !ExceptionTypes.HasTwin(type);

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var creation = (BaseObjectCreationExpressionSyntax)node;
        var type = (INamedTypeSymbol)context.SemanticHelper.GetType(node)!;
        var built = ExceptionTypes.Construction(ExceptionTypes.ChainOf(type), creation, context);
        // Its object initializer, applied to what that built, as a record's is (#587): it was dropped,
        // so `new Retry { Attempts = 3 }.Attempts` read nothing.
        return creation.Initializer is { } initializer && initializer.IsKind(SyntaxKind.ObjectInitializerExpression)
            ? ObjectInitializer.Apply(built, initializer, context)
            : built;
    }

    /// <summary>The position of the message among the written arguments, or -1 where none was
    /// written. Internal for the creation the model cannot see, which
    /// <see cref="ObjectCreationStrategy"/> builds by name.</summary>
    internal static int MessageIndex(BaseObjectCreationExpressionSyntax creation, ConversionContext context)
    {
        if (creation.ArgumentList?.Arguments is not { Count: > 0 } arguments) return -1;
        if (arguments.Count == 1) return 0;

        if (context.SemanticHelper.GetSymbol(creation) is IMethodSymbol constructor)
        {
            for (var i = 0; i < arguments.Count && i < constructor.Parameters.Length; i++)
            {
                var parameter = arguments[i].NameColon?.Name.Identifier.ValueText is { } named
                    ? constructor.Parameters.FirstOrDefault(p => p.Name == named)
                    : constructor.Parameters[i];
                if (parameter?.Name == "message") return i;
            }
        }

        return arguments.Count - 1;
    }
}
