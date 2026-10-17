using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps <c>System.DateTime</c> to the runtime <c>DateTime</c> compat type (tick-precise, .NET-faithful).
/// Statics route through the <c>dateTime</c> factory; an instance member is the value's, by the name
/// <see cref="Instance"/> gives it. Only what the twin and the factory carry is claimed: any other member
/// is left to the fallbacks, which refuse it (EQ2004), where it was written camel-cased on a twin that
/// had no such member, <c>dateTime.compare(…)</c> and <c>d.toBinary()</c> (#839). A method group is the
/// delegate its call is (<see cref="Expressions.MethodGroupStrategy"/>). A constructor is
/// <see cref="DateTimeConstructionStrategy"/>'s, and operators (+ - and comparisons) are handled by
/// BinaryExpressionStrategy.
/// </summary>
/// <remarks>
/// Priority 15 so it wins over the generic ToString (10), ObjectCreation (5) and member-access (0)
/// strategies for DateTime nodes. Gated on the semantic type, so only genuine DateTime nodes are taken.
/// </remarks>
public class DateTimeStrategy : ConversionStrategyBase
{
    private const string TypeName = "System.DateTime";

    /// <summary>The instance members the twin carries, by the name it carries each under. Subtracting a
    /// date is <c>diff</c>, and a span <c>subtract</c> (<see cref="Twin"/>).</summary>
    private static readonly Dictionary<string, string> Instance = new(StringComparer.Ordinal)
    {
        ["Year"] = "year", ["Month"] = "month", ["Day"] = "day", ["Hour"] = "hour", ["Minute"] = "minute",
        ["Second"] = "second", ["Millisecond"] = "millisecond", ["Microsecond"] = "microsecond",
        ["Nanosecond"] = "nanosecond", ["DayOfWeek"] = "dayOfWeek", ["DayOfYear"] = "dayOfYear", ["Date"] = "date",
        ["TimeOfDay"] = "timeOfDay", ["Ticks"] = "ticks", ["Kind"] = "kind", ["ToLocalTime"] = "toLocalTime",
        ["ToUniversalTime"] = "toUniversalTime", ["AddTicks"] = "addTicks", ["AddDays"] = "addDays",
        ["AddHours"] = "addHours", ["AddMinutes"] = "addMinutes", ["AddSeconds"] = "addSeconds",
        ["AddMilliseconds"] = "addMilliseconds", ["AddMicroseconds"] = "addMicroseconds", ["AddMonths"] = "addMonths",
        ["AddYears"] = "addYears", ["Add"] = "add", ["Subtract"] = "subtract", ["CompareTo"] = "compareTo",
        ["Equals"] = "equals", ["GetHashCode"] = "getHashCode", ["ToShortDateString"] = "", ["ToLongDateString"] = "",
        ["ToShortTimeString"] = "", ["ToLongTimeString"] = "",
    };

    /// <summary>The statics the factory carries: a property read as a call of its member, a method called
    /// through it.</summary>
    private static readonly HashSet<string> Static = new(StringComparer.Ordinal)
    {
        "Now", "UtcNow", "Today", "MinValue", "MaxValue", "UnixEpoch", "DaysInMonth", "IsLeapYear", "SpecifyKind",
        "Compare", "Equals",
    };

    /// <summary>The methods that are a standard specifier by another name.</summary>
    private static readonly Dictionary<string, string> CultureStrings = new(StringComparer.Ordinal)
    {
        ["ToShortDateString"] = "d",
        ["ToLongDateString"] = "D",
        ["ToShortTimeString"] = "t",
        ["ToLongTimeString"] = "T",
    };

    public override bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        switch (node)
        {
            // A value's ToString is the formatter's (ToStringStrategy), as a number's is: the twin's
            // toString(pattern) knew custom tokens only, so `d.ToString("D")` printed `D`, and a
            // provider crossed to the browser as a name no browser has (#388).
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ToString" } }:
                return false;

            // No twin reads a date back through a format pattern, nor lists a date's formats: each was a
            // call to a member the twin does not have, a TypeError on the page. Left unclaimed, each is
            // refused at the build (#717).
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ParseExact" or "TryParseExact" or "GetDateTimeFormats" } }:
                return false;

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma }:
                return IsDateTimeMember(ma, context) && Carried(ma, context);

            // A read: a method named without a call is a group, the delegate its call is.
            case MemberAccessExpressionSyntax member when context.SemanticHelper.GetSymbol(member) is not IMethodSymbol:
                return IsDateTimeMember(member, context) && Carried(member, context);

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
                var args = ConvertArgs(inv.ArgumentList, context);
                if (IsStaticAccess(ma, context))
                {
                    return $"{Eq.DateTime}.{name.ToCamelCase()}({args})";
                }
                var receiver = context.Converter.ConvertExpression(ma.Expression);
                // The short and long date and time strings are .NET's ToString("d"), ("D"), ("t")
                // and ("T") in the current culture, and go where ToString goes; the twin has no
                // method by those names, so each was a TypeError in the browser (found in review, #388).
                if (CultureStrings.TryGetValue(name, out var specifier))
                    return $"{Eq.Format}({receiver}, '{specifier}')";
                return $"{receiver}.{Twin(name, context.SemanticHelper.GetSymbol(inv) as IMethodSymbol)}({args})";
            }

            case MemberAccessExpressionSyntax member:
            {
                var name = member.Name.Identifier.ValueText;
                if (IsStaticAccess(member, context))
                {
                    // Static properties (Now/UtcNow/Today/MinValue/MaxValue) map to factory methods.
                    return $"{Eq.DateTime}.{name.ToCamelCase()}()";
                }
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return $"{receiver}.{Instance[name]}";
            }

            default:
                return context.Unhandled(node, "DateTime");
        }
    }

    /// <summary>Whether the twin, or the factory for a static, carries the member <paramref name="ma"/>
    /// names.</summary>
    private static bool Carried(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var name = ma.Name.Identifier.ValueText;
        return IsStaticAccess(ma, context) ? Static.Contains(name) : Instance.ContainsKey(name);
    }

    /// <summary>The twin's name for an instance method: <c>Subtract</c> of a date is the span between
    /// the two, <c>diff</c>, where its <c>subtract</c> takes a span off the date.</summary>
    private static string Twin(string name, IMethodSymbol? method) =>
        name == "Subtract" && method?.Parameters is [{ Type: var operand }] && IsType(operand) ? "diff" : Instance[name];

    private static bool IsDateTimeMember(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol?.ContainingType != null)
            return symbol.ContainingType.ToDisplayString() == TypeName;

        // No semantic info: static access via the type name, or an instance whose type we can read.
        if (ma.Expression.ToString() == "DateTime") return true;
        return IsType(context.SemanticHelper.GetType(ma.Expression));
    }

    private static bool IsStaticAccess(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol != null) return symbol.IsStatic;
        return ma.Expression.ToString() == "DateTime";
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
