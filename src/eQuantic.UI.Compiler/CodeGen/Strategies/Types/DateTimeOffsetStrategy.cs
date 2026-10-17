using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps <c>System.DateTimeOffset</c> to the runtime <c>dateTimeOffset</c> compat type: statics
/// (<c>Now</c>, <c>FromUnixTimeSeconds</c>, <c>Parse</c>, …) -> factory members; instance members/methods ->
/// camelCase. Operators (+ - and comparisons) are handled by BinaryExpressionStrategy. Priority 15,
/// gated on the receiver type (robust against members resolving to interfaces). A constructor is
/// <see cref="DateTimeConstructionStrategy"/>'s.
/// <para>
/// Only what the twin and the factory carry is claimed (<see cref="Instance"/>, <see cref="Static"/>): any
/// other member is left to the fallbacks, which refuse it (EQ2004), where it was written camel-cased on a
/// twin that had no such member, <c>o.toFileTime()</c> and <c>dateTimeOffset.compare(…)</c> (#839).
/// Subtracting an instant is <c>diff</c>. A method group is the delegate its call is.
/// </para>
/// </summary>
public class DateTimeOffsetStrategy : ConversionStrategyBase
{
    private const string TypeName = "System.DateTimeOffset";

    /// <summary>The instance members the twin carries, by the name C# gives them.</summary>
    private static readonly HashSet<string> Instance = new(StringComparer.Ordinal)
    {
        "UtcTicks", "Year", "Month", "Day", "Hour", "Minute", "Second", "Millisecond", "Microsecond", "Nanosecond",
        "DayOfWeek", "DayOfYear", "Ticks", "Offset", "TotalOffsetMinutes", "DateTime", "LocalDateTime", "UtcDateTime",
        "Date", "TimeOfDay", "AddTicks", "AddDays", "AddHours", "AddMinutes", "AddSeconds", "AddMilliseconds",
        "AddMicroseconds", "AddMonths", "AddYears", "ToLocalTime", "ToOffset", "ToUniversalTime", "Add", "Subtract",
        "CompareTo", "Equals", "EqualsExact", "GetHashCode", "ToUnixTimeSeconds", "ToUnixTimeMilliseconds",
    };

    /// <summary>The statics the factory carries.</summary>
    private static readonly HashSet<string> Static = new(StringComparer.Ordinal)
    {
        "Now", "UtcNow", "MinValue", "MaxValue", "UnixEpoch", "FromUnixTimeSeconds", "FromUnixTimeMilliseconds",
        "Compare", "Equals",
    };

    public override bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            // A value's ToString is the formatter's (ToStringStrategy), as a DateTime's is (#469): the
            // twin's toString wrote the invariant text, and ignored a format and a provider.
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ToString" } }:
                return false;

            // No twin reads a date back through a format pattern: each was a call to a member the twin
            // does not have, a TypeError on the page. Left unclaimed, each is refused at the build (#717).
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ParseExact" or "TryParseExact" } }:
                return false;
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
                return IsMember(ma, context) && Carried(ma, context);
            // A read: a method named without a call is a group, the delegate its call is.
            case MemberAccessExpressionSyntax member when context.SemanticHelper.GetSymbol(member) is not IMethodSymbol:
                return IsMember(member, context) && Carried(member, context);
            default:
                return false;
        }
    }

    public override string Convert(SyntaxNode node, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        switch (node)
        {
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
            {
                var name = ma.Name.Identifier.ValueText;
                var method = context.SemanticHelper.GetSymbol(inv) as IMethodSymbol;
                if (IsStaticAccess(ma, context))
                    return ByParameter($"{Eq.DateTimeOffset}.{name.ToCamelCase()}", inv.ArgumentList, method, context);
                // Subtracting an instant is the span between the two, where `subtract` takes a span off it.
                var twin = name == "Subtract" && method is { Parameters: [{ Type: var operand }] }
                    && IsType(operand) ? "diff" : name.ToCamelCase();
                return ByParameter($".{twin}", inv.ArgumentList, method, context, ma.Expression);
            }

            case MemberAccessExpressionSyntax member:
            {
                var name = member.Name.Identifier.ValueText;
                if (IsStaticAccess(member, context))
                    return $"{Eq.DateTimeOffset}.{name.ToCamelCase()}()"; // Now/UtcNow/MinValue/MaxValue -> factory methods
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return $"{receiver}.{name.ToCamelCase()}";
            }

            default:
                return context.Unhandled(node, "DateTimeOffset");
        }
    }

    /// <summary>Whether the twin, or the factory for a static, carries the member <paramref name="ma"/>
    /// names.</summary>
    private static bool Carried(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var name = ma.Name.Identifier.ValueText;
        return IsStaticAccess(ma, context) ? Static.Contains(name) : Instance.Contains(name);
    }

    private static bool IsMember(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        if (IsType(context.SemanticHelper.GetType(ma.Expression))) return true;
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol?.ContainingType != null && symbol.ContainingType.ToDisplayString() == TypeName) return true;
        return ma.Expression.ToString() == "DateTimeOffset";
    }

    private static bool IsStaticAccess(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol != null) return symbol.IsStatic;
        return ma.Expression.ToString() == "DateTimeOffset";
    }

    private static bool IsType(ITypeSymbol? type) => type?.ToDisplayString() == TypeName;

    public override int Priority => 15;
}
