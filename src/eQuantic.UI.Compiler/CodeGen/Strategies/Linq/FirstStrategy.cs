using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.Services;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// Converts LINQ .First()/.FirstOrDefault() to JavaScript.
/// - First() -> [0]
/// - First(predicate) -> find(predicate)
/// </summary>
public class FirstStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var name = memberAccess.Name.Identifier.Text;
        if (name != "First" && name != "FirstOrDefault")
            return false;

        // Semantic Check
        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType))
        {
            return true;
        }

        // Name decides ONLY where guessing is honest — see ConversionContext.CanGuess. Under an
        // AUTHORITATIVE model, in-tree-but-unbindable is reported (EQ2006), never guessed.
        if (symbol == null && context.CanGuess(node))
        {
            return true;
        }

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        
        if (WithADefaultValue(invocation, memberAccess, Eq.LinqFirstOrDefault, context) is { } withDefault)
            return withDefault;

        var caller = LinqSource.Text(memberAccess.Expression, context);
        var args = invocation.ArgumentList.Arguments;
        
        // FirstOrDefault answers the ELEMENT's default when nothing matches — 0 for an int
        // sequence, not null (DefaultValue). First() keeps the bare lookup: C# throws on an empty
        // sequence and this hands back undefined, a divergence documented here since before the
        // conformance suite existed and left as it was.
        var orDefault = memberAccess.Name.Identifier.Text == "FirstOrDefault"
            ? $" ?? {DefaultValue.OfElement(context.SemanticHelper.GetType(memberAccess.Expression), context)}"
            : "";

        if (args.Count > 0)
        {
            // First(predicate) -> find(predicate)
            var predicate = context.Converter.ConvertExpression(args[0].Expression);
            return orDefault.Length > 0
                ? $"({caller}.find({predicate}){orDefault})"
                : $"{caller}.find({predicate})";
        }

        return orDefault.Length > 0 ? $"({caller}[0]{orDefault})" : $"{caller}[0]";
    }

    /// <summary>
    /// <c>FirstOrDefault(defaultValue)</c> and <c>FirstOrDefault(predicate, defaultValue)</c>, .NET 6's
    /// overloads, and <c>LastOrDefault</c>'s twins of them, through the runtime's (<paramref name="helper"/>):
    /// the element that matches, a null one included, or the value handed (#720). They took the shape of
    /// the overload with a predicate, which called the default value as a function, and a <c>??</c> over
    /// the element would hand the default for a null element .NET answers. Null for every other overload.
    /// </summary>
    internal static string? WithADefaultValue(InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax access,
        string helper, ConversionContext context)
    {
        if (context.SemanticHelper.GetSymbol(invocation) is not IMethodSymbol method
            || !method.Parameters.Any(parameter => parameter.Name == "defaultValue"))
            return null;
        var call = LinqArguments.Call(helper, LinqSource.Ir(access.Expression, context), ["predicate", "defaultValue"],
            invocation, argument => context.Converter.ConvertIr(argument.Expression), context);
        return call is null
            ? context.Unhandled(invocation, $"{method.Name} with a default value")
            : Ir.JsExprWriter.Write(call);
    }

    public int Priority => 10;
}
