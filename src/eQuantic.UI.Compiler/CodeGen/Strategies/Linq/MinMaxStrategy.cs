using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// LINQ's <c>Min()</c>, <c>Min(selector)</c>, <c>Max()</c> and <c>Max(selector)</c>, through the
/// runtime's <c>$eq.linq.min</c>/<c>max</c> with the ordering the answered type calls for. Only a call
/// no model binds keeps the old <c>Math.min</c>/<c>Math.max</c> spread.
/// </summary>
public class MinMaxStrategy : IConversionStrategy
{
    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return false;

        var methodName = memberAccess.Name.Identifier.Text;
        if (methodName != "Min" && methodName != "Max")
            return false;

        var symbol = context.SemanticHelper.GetSymbol(invocation);
        if (symbol is IMethodSymbol ms && context.SemanticHelper.IsLinqExtension(ms.ContainingType))
            return true;

        if (symbol == null && context.CanGuess(node))
            return true;

        return false;
    }

    public string Convert(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var methodName = memberAccess.Name.Identifier.Text;

        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method)
            return Extreme(invocation, memberAccess, method, methodName == "Min" ? Eq.LinqMin : Eq.LinqMax, context);

        // No model to ask what the call answers: the numeric shape it always had.
        var caller = context.Converter.ConvertExpression(memberAccess.Expression);
        var args = invocation.ArgumentList.Arguments;
        var mathFunc = methodName == "Min" ? "Math.min" : "Math.max";

        if (args.Count > 0)
        {
            // Min(x => x.Value) -> Math.min(...array.map(selector))
            var selector = context.Converter.ConvertExpression(args[0].Expression);
            return $"{mathFunc}(...{caller}.map({selector}))";
        }

        // Min() without selector
        return $"{mathFunc}(...{caller})";
    }

    /// <summary>
    /// <c>Max</c> and <c>Min</c> by the type the bound call ANSWERS, which orders the values and says
    /// whether the answer may be null (a reference type or a <c>Nullable&lt;T&gt;</c>, which pass a
    /// null over and answer null for a sequence with no value; anything else throws for an empty one).
    /// <c>Math.max</c> over the values answered -Infinity for an empty list, let a NaN win a
    /// <c>Max</c>, and made NaN of two strings and a TypeError of two longs. An enum is refused: its
    /// values cross as member NAMES, which order alphabetically where .NET orders by value; and so is
    /// a Guid, which .NET orders by its fields and this side rides as text. A comparer argument has
    /// no JavaScript form to call.
    /// </summary>
    private static string Extreme(InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax access,
        IMethodSymbol method, string helper, ConversionContext context)
    {
        var arguments = invocation.ArgumentList.Arguments;
        // `Enumerable.Max(list, f)` names the source as its first argument; `list.Max(f)` as the receiver.
        var staticForm = method.MethodKind != MethodKind.ReducedExtension;
        var parameters = staticForm ? method.Parameters.Skip(1).ToArray() : method.Parameters.ToArray();
        if (parameters is [{ Type.Name: "IComparer" }, ..])
            return context.Unhandled(invocation, "LINQ Max/Min with a comparer");
        if (OrderingOf(method.ReturnType) is not var (ordering, nullable))
            return context.Unhandled(invocation, $"LINQ Max/Min over {method.ReturnType.ToDisplayString()}");

        context.UsedHelpers.Add(Eq.Import);
        var source = context.Converter.ConvertExpression(staticForm ? arguments[0].Expression : access.Expression);
        var rest = staticForm ? arguments.Skip(1).ToArray() : arguments.ToArray();
        var selector = rest.Length > 0 ? context.Converter.ConvertExpression(rest[0].Expression) : "undefined";
        return $"{helper}({source}, {selector}, '{ordering}', {(nullable ? "true" : "false")})";
    }

    /// <summary>How the runtime orders the values a call answers, and whether it answers null: see
    /// <c>Ordering</c> in utils/linq.ts. Null where the values have no faithful order this side.</summary>
    private static (string Ordering, bool Nullable)? OrderingOf(ITypeSymbol type)
    {
        var nullable = type.IsReferenceType || type.IsNullableValue();
        var value = type.UnwrapNullable() ?? type;
        if (value.TypeKind == TypeKind.Enum || value.IsNamed("System.Guid")) return null;
        var ordering = value.SpecialType switch
        {
            SpecialType.System_Double or SpecialType.System_Single => "real",
            SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Int16
                or SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_UInt16
                or SpecialType.System_UInt32 or SpecialType.System_UInt64 or SpecialType.System_Char
                or SpecialType.System_Boolean => "value",
            SpecialType.System_String => "text",
            _ => "comparable",
        };
        return (ordering, nullable);
    }

    public int Priority => 10;
}
