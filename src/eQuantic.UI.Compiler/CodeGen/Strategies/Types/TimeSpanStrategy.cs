using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps <c>System.TimeSpan</c> to the runtime <c>TimeSpan</c> compat type (tick-precise, .NET "c"
/// formatting). Construction and statics (<c>FromSeconds</c>, …) route through the <c>timeSpan</c>
/// factory; an instance member is the twin's, by the name <see cref="Instance"/> gives it, and a call to
/// <c>ToString</c> is the formatter's. Only what the twin carries is claimed: any other member is left to
/// the fallbacks, which refuse it (EQ2004), where it was written camel-cased on a twin that had no such
/// member (<c>ts.tryFormat(…)</c>, #839). A method group is no member access here: it is the delegate its
/// call is (<see cref="Expressions.MethodGroupStrategy"/>). Operators are handled by
/// BinaryExpressionStrategy. Priority 15, gated on the semantic type.
/// </summary>
public class TimeSpanStrategy : ConversionStrategyBase
{
    private const string TypeName = "System.TimeSpan";

    /// <summary>The instance members the twin carries, by the name it carries each under.</summary>
    private static readonly Dictionary<string, string> Instance = new(StringComparer.Ordinal)
    {
        ["Days"] = "days", ["Hours"] = "hours", ["Minutes"] = "minutes", ["Seconds"] = "seconds",
        ["Milliseconds"] = "milliseconds", ["Microseconds"] = "microseconds", ["Nanoseconds"] = "nanoseconds",
        ["Ticks"] = "ticks", ["TotalDays"] = "totalDays", ["TotalHours"] = "totalHours", ["TotalMinutes"] = "totalMinutes",
        ["TotalSeconds"] = "totalSeconds", ["TotalMilliseconds"] = "totalMilliseconds",
        ["TotalMicroseconds"] = "totalMicroseconds", ["TotalNanoseconds"] = "totalNanoseconds",
        ["Add"] = "add", ["Subtract"] = "sub", ["Negate"] = "negate", ["Duration"] = "duration",
        ["Multiply"] = "multiply", ["Divide"] = "divide", ["CompareTo"] = "compareTo", ["Equals"] = "equals",
        ["GetHashCode"] = "getHashCode",
    };

    /// <summary>The statics the factory carries: a property read as its member, a method called through it.</summary>
    private static readonly HashSet<string> Static = new(StringComparer.Ordinal)
    {
        "Zero", "MaxValue", "MinValue", "FromDays", "FromHours", "FromMinutes", "FromSeconds", "FromMilliseconds",
        "FromMicroseconds", "FromTicks", "Compare", "Equals",
    };

    public override bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            case BaseObjectCreationExpressionSyntax oc:
                return IsType(context.SemanticHelper.GetType(oc))
                    || (oc is ObjectCreationExpressionSyntax named && named.Type.ValueText() == "TimeSpan");

            // A value's ToString is the formatter's (ToStringStrategy), as a date's is: the twin's
            // toString takes no format, so every format wrote the constant text, and a null one was
            // read through null (#837).
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ToString" } }:
                return false;

            // No twin reads a time span back through a format pattern: each was a call to a member the
            // twin does not have, a TypeError on the page. Left unclaimed, each is refused at the build
            // (#717).
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ParseExact" or "TryParseExact" } }:
                return false;

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
                return IsTimeSpanMember(ma, context) && Carried(ma, context);

            // A read: a method named without a call is a group, the delegate its call is.
            case MemberAccessExpressionSyntax member when context.SemanticHelper.GetSymbol(member) is not IMethodSymbol:
                return IsTimeSpanMember(member, context) && Carried(member, context);

            default:
                return false;
        }
    }

    public override string Convert(SyntaxNode node, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        switch (node)
        {
            case BaseObjectCreationExpressionSyntax oc:
                return $"{Eq.TimeSpan}({ConvertArgs(oc.ArgumentList, context)})";

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
            {
                var name = ma.Name.Identifier.ValueText;
                if (IsStaticAccess(ma, context))
                {
                    var callee = $"{Eq.TimeSpan}.{name.ToCamelCase()}";
                    return context.SemanticHelper.GetSymbol(inv) is IMethodSymbol method
                        ? ByParameter(callee, inv, method, context)
                        : $"{callee}({ConvertArgs(inv.ArgumentList, context)})";
                }
                var args = ConvertArgs(inv.ArgumentList, context);
                var receiver = context.Converter.ConvertExpression(ma.Expression);
                return $"{receiver}.{Instance[name]}({args})";
            }

            case MemberAccessExpressionSyntax member:
            {
                var name = member.Name.Identifier.ValueText;
                if (IsStaticAccess(member, context))
                {
                    // Static properties (Zero/MinValue/MaxValue). Zero is a field, the others methods.
                    return name == "Zero" ? $"{Eq.TimeSpan}.zero" : $"{Eq.TimeSpan}.{name.ToCamelCase()}()";
                }
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return $"{receiver}.{Instance[name]}";
            }

            default:
                return context.Unhandled(node, "TimeSpan");
        }
    }

    /// <summary>
    /// A static call with each argument in its PARAMETER's place, for a twin that takes them by
    /// position: an optional parameter the call leaves out is <c>undefined</c>, up to the last one it
    /// fills. Passed as written, <c>TimeSpan.FromHours(1, seconds: 5)</c> put the 5 in the minutes.
    /// The arguments stay in the order they were written, which is the order C# evaluates them, and
    /// the template writer keeps it where the places do not follow it.
    /// </summary>
    private static string ByParameter(string callee, InvocationExpressionSyntax invocation, IMethodSymbol method,
        ConversionContext context)
    {
        var arguments = invocation.ArgumentList.Arguments;
        var places = new string?[method.Parameters.Length];
        for (var i = 0; i < arguments.Count; i++)
        {
            var named = arguments[i].NameColon?.Name.Identifier.ValueText;
            var ordinal = named is null ? i : method.Parameters.FirstOrDefault(p => p.Name == named)?.Ordinal ?? -1;
            if (ordinal < 0 || ordinal >= places.Length) return $"{callee}({ConvertArgs(invocation.ArgumentList, context)})";
            places[ordinal] = "{" + i + "}";
        }
        var filled = places.Take(System.Array.FindLastIndex(places, place => place is not null) + 1)
            .Select(place => place ?? "undefined");
        var parts = arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)).ToArray();
        return JsExprWriter.Write(JsExpr.Template($"{callee}({string.Join(", ", filled)})", parts));
    }

    /// <summary>Whether the twin, or the factory for a static, carries the member <paramref name="ma"/>
    /// names.</summary>
    private static bool Carried(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var name = ma.Name.Identifier.ValueText;
        return IsStaticAccess(ma, context) ? Static.Contains(name) : Instance.ContainsKey(name);
    }

    private static bool IsTimeSpanMember(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol?.ContainingType != null)
            return symbol.ContainingType.ToDisplayString() == TypeName;

        if (ma.Expression.ToString() == "TimeSpan") return true;
        return IsType(context.SemanticHelper.GetType(ma.Expression));
    }

    private static bool IsStaticAccess(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol != null) return symbol.IsStatic;
        return ma.Expression.ToString() == "TimeSpan";
    }

    private static bool IsType(ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol named
            && named.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1)
        {
            type = named.TypeArguments[0];
        }
        return type?.ToDisplayString() == TypeName;
    }

    public override int Priority => 15;
}
