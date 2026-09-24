using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

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
    /// a Guid, which .NET orders by its fields and this side rides as text, and every other type with
    /// no <c>compareTo</c> to call here (<see cref="CarriesCompareTo"/>). A comparer argument has no
    /// JavaScript form to call.
    /// </summary>
    private static string Extreme(InvocationExpressionSyntax invocation, MemberAccessExpressionSyntax access,
        IMethodSymbol method, string helper, ConversionContext context)
    {
        var arguments = invocation.ArgumentList.Arguments;
        // `Enumerable.Max(list, f)` names the source as its first PARAMETER; `list.Max(f)` as the receiver.
        var staticForm = method.MethodKind != MethodKind.ReducedExtension;
        var parameters = staticForm ? method.Parameters.Skip(1).ToArray() : method.Parameters.ToArray();
        if (parameters is [{ Type.Name: "IComparer" }, ..])
            return context.Unhandled(invocation, "LINQ Max/Min with a comparer");
        if (OrderingOf(method.ReturnType) is not var (ordering, nullable))
            return context.Unhandled(invocation, $"LINQ Max/Min over {method.ReturnType.ToDisplayString()}");

        context.UsedHelpers.Add(Eq.Import);
        var how = $"'{ordering}', {(nullable ? "true" : "false")}";
        if (staticForm)
        {
            // Each argument in its parameter's place, and all of them run in the order they were
            // written: `Enumerable.Max(selector: f, source: xs)` handed the lambda over as the source.
            var selector = parameters.Length > 0 ? "{1}" : "undefined";
            var template = PrimitiveStaticStrategy.BindNamedArguments($"{helper}({{0}}, {selector}, {how})", invocation, method);
            var parts = arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)).ToArray();
            return JsExprWriter.Write(JsExpr.Template(template, parts, context.TypeAnnotations));
        }
        var source = context.Converter.ConvertExpression(access.Expression);
        var projection = arguments.Count > 0 ? context.Converter.ConvertExpression(arguments[0].Expression) : "undefined";
        return $"{helper}({source}, {projection}, {how})";
    }

    /// <summary>How the runtime orders the values a call answers, and whether it answers null: see
    /// <c>Ordering</c> in utils/linq.ts. Null where the values have no faithful order this side.</summary>
    private static (string Ordering, bool Nullable)? OrderingOf(ITypeSymbol type)
    {
        var nullable = type.IsReferenceType || type.IsNullableValue();
        var value = type.UnwrapNullable() ?? type;
        string? ordering = value.SpecialType switch
        {
            SpecialType.System_Double or SpecialType.System_Single => "real",
            SpecialType.System_Int32 or SpecialType.System_Int64 or SpecialType.System_Int16
                or SpecialType.System_Byte or SpecialType.System_SByte or SpecialType.System_UInt16
                or SpecialType.System_UInt32 or SpecialType.System_UInt64 or SpecialType.System_Char
                or SpecialType.System_Boolean => "value",
            SpecialType.System_String => "text",
            _ when CarriesCompareTo(value) => "comparable",
            _ => null,
        };
        return ordering is null ? null : (ordering, nullable);
    }

    /// <summary>
    /// Whether a value of this type orders by a <c>compareTo</c> it carries on this side: a decimal and
    /// the dates, whose runtime types have one, and a type of the app's own that is comparable through
    /// a <c>CompareTo</c> it wrote, which its twin carries by that name. Nothing else has one to call:
    /// an enum and a Guid are comparable in the BCL but cross as a name and as text, a type that is not
    /// comparable makes .NET's default comparer throw, a comparison written as an explicit interface
    /// member has no name to be called by, and a type parameter may be answered by a number.
    /// </summary>
    private static bool CarriesCompareTo(ITypeSymbol type)
    {
        if (type.SpecialType is SpecialType.System_Decimal or SpecialType.System_DateTime
            || type.IsNamed("System.TimeSpan") || type.IsNamed("System.DateOnly")
            || type.IsNamed("System.TimeOnly") || type.IsNamed("System.DateTimeOffset"))
            return true;
        return type.AllInterfaces
            .Where(contract => contract.ContainingNamespace?.ToDisplayString() == "System"
                && contract.OriginalDefinition.MetadataName is "IComparable" or "IComparable`1")
            .SelectMany(contract => contract.GetMembers("CompareTo"))
            .Select(type.FindImplementationForInterfaceMember)
            .Any(found => found is IMethodSymbol { MethodKind: MethodKind.Ordinary } method
                && method.Locations.Any(location => location.IsInSource));
    }

    public int Priority => 10;
}
