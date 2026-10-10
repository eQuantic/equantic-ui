using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// <c>new T(…)</c>, and the target-typed <c>new(…)</c>, where T is an exception: the browser's
/// exception, an <c>Error</c> carrying T and every type it derives from
/// (<see cref="ExceptionTypes.Construction(IReadOnlyList{string}, BaseObjectCreationExpressionSyntax, ConversionContext)"/>),
/// so a typed <c>catch</c> can tell it from another. Decided by the created type's SYMBOL: an exception
/// of the app's own is one whatever its name, and <c>Exception e = new("x")</c> is one too, where it
/// constructed a class named <c>Exception</c> that JavaScript does not have.
/// <para>
/// Every argument is evaluated, in the order it is written, as C# evaluates a constructor's: the
/// message is the one bound to the constructor's <c>message</c> parameter (signatures differ:
/// <c>ArgumentException(message, paramName)</c> and <c>ArgumentOutOfRangeException(paramName,
/// message)</c>), the last argument where the constructor cannot be asked, and the one argument of a
/// constructor that takes one. A framework constructor's parameter name, actual value, inner exception
/// and object name are carried by their parameters, and its message composed from them as .NET
/// composes it (#558); an app's own arguments past the message run and are carried nowhere. An object
/// initializer is applied to the exception once it is built (#587); the members an app exception
/// declares are not written yet (#611).
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
        var constructor = context.SemanticHelper.GetSymbol(creation) as IMethodSymbol;
        // A framework constructor says which argument is the message by its parameter's name, and one
        // with no message parameter has none: `new ArgumentNullException(nameof(x))` took the
        // parameter's name for its message (#558). An app's own is read as it always was, its one
        // argument or the one named so, since its constructor may hand any parameter to its base.
        if (constructor is not null && ExceptionTypes.IsFramework(constructor.ContainingType))
            return ArgumentIndex(creation, constructor, "message");
        if (arguments.Count == 1) return 0;
        if (constructor is not null && ArgumentIndex(creation, constructor, "message") is var message and >= 0)
            return message;
        return arguments.Count - 1;
    }

    /// <summary>The written position of the argument the constructor binds to the parameter named
    /// <paramref name="parameter"/>, a named argument included, or -1 where none was written.</summary>
    internal static int ArgumentIndex(BaseObjectCreationExpressionSyntax creation, IMethodSymbol constructor, string parameter)
    {
        var arguments = creation.ArgumentList?.Arguments ?? default;
        for (var i = 0; i < arguments.Count && i < constructor.Parameters.Length; i++)
        {
            var bound = arguments[i].NameColon?.Name.Identifier.ValueText is { } named
                ? constructor.Parameters.FirstOrDefault(p => p.Name == named)
                : constructor.Parameters[i];
            if (bound?.Name == parameter) return i;
        }
        return -1;
    }
}
