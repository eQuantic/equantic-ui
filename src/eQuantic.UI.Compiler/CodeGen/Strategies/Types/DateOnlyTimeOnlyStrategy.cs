using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// Maps <c>System.DateOnly</c> and <c>System.TimeOnly</c> to the runtime compat types:
/// <c>new DateOnly/TimeOnly(...)</c> -> <c>$eq.time.dateOnly/timeOnly(...)</c>; statics
/// (<c>MinValue</c>, <c>FromDateTime</c>, <c>Parse</c>, …) -> factory members; instance
/// members/methods -> camelCase. Only what the twins and their factories carry is claimed
/// (<see cref="Carried"/>): any other member is left to the fallbacks, which refuse it (EQ2004), where it
/// was written on a twin that had no such member, <c>d.toDateTime(…)</c> and <c>t.isBetween(…)</c> (#839).
/// A method that hands a value back through an out (<c>AddHours(h, out wrapped)</c>) is no twin's either.
/// A method group is the delegate its call is (<see cref="Expressions.MethodGroupStrategy"/>). Priority 15,
/// gated on the receiver type (robust against members that resolve to interfaces/extensions).
/// </summary>
public class DateOnlyTimeOnlyStrategy : ConversionStrategyBase
{
    /// <summary>The members each twin carries, instance and static, by the name C# gives them.</summary>
    private static readonly Dictionary<string, (HashSet<string> Instance, HashSet<string> Static)> Members = new(StringComparer.Ordinal)
    {
        ["dateOnly"] = (
            ["Year", "Month", "Day", "DayOfWeek", "DayOfYear", "DayNumber", "AddDays", "AddMonths", "AddYears", "ToDateTime",
                "CompareTo", "Equals", "GetHashCode", "ToShortDateString", "ToLongDateString"],
            ["MinValue", "MaxValue", "FromDayNumber", "FromDateTime"]),
        ["timeOnly"] = (
            ["Hour", "Minute", "Second", "Millisecond", "Microsecond", "Nanosecond", "Ticks", "AddHours", "AddMinutes", "Add",
                "IsBetween", "ToTimeSpan", "CompareTo", "Equals", "GetHashCode", "ToShortTimeString", "ToLongTimeString"],
            ["MinValue", "MaxValue", "FromTimeSpan", "FromDateTime"]),
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
            case BaseObjectCreationExpressionSyntax oc:
                return KindOf(context.SemanticHelper.GetType(oc)) != null
                    || (oc is ObjectCreationExpressionSyntax named && KindOfName(named.Type.ValueText()) != null);

            // No twin reads a date back through a format pattern: `ParseExact` emitted a call to a
            // member the runtime does not have, a TypeError the moment the page ran it, and the audit
            // graded it `eq` because the call named `$eq`. Left unclaimed, it is EQ1001 at build time.
            // A value's ToString is the formatter's (ToStringStrategy), as a DateTime's is (#469): the
            // twin's toString knew six tokens and no culture.
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } invocation:
                return Kind(ma, context) != null
                    && ma.Name.Identifier.ValueText is not ("ParseExact" or "TryParseExact" or "ToString")
                    && Carried(ma, context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol, context);

            // A read: a method named without a call is a group, the delegate its call is.
            case MemberAccessExpressionSyntax member when context.SemanticHelper.GetSymbol(member) is not IMethodSymbol:
                return Kind(member, context) != null && Carried(member, null, context);

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
            {
                // A calendar was dropped, so a null one was never refused and any other was read as the
                // Gregorian one (#725): it is refused, as DateTime's is.
                if (context.SemanticHelper.GetSymbol(oc) is IMethodSymbol constructor
                    && DateTimeConstructionStrategy.RefusesCalendar(oc, constructor, constructor.ContainingType.Name, context))
                    return oc.ToString();
                var kind = KindOf(context.SemanticHelper.GetType(oc))
                    ?? (oc is ObjectCreationExpressionSyntax named ? KindOfName(named.Type.ValueText()) : null);
                return $"$eq.time.{kind}({ConvertArgs(oc.ArgumentList, context)})";
            }

            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax ma } inv:
            {
                var name = ma.Name.Identifier.ValueText;
                var args = ConvertArgs(inv.ArgumentList, context);
                if (IsStaticAccess(ma, context))
                    return $"$eq.time.{StaticKind(ma, context)}.{name.ToCamelCase()}({args})";
                var receiver = context.Converter.ConvertExpression(ma.Expression);
                // The short and long date and time strings are .NET's ToString("d"), ("D"), ("t") and
                // ("T") in the current culture, and go where ToString goes: the twins have no method by
                // those names, so each was a TypeError in the browser (#469).
                if (CultureStrings.TryGetValue(name, out var specifier))
                    return $"{Eq.Format}({receiver}, '{specifier}')";
                return $"{receiver}.{name.ToCamelCase()}({args})";
            }

            case MemberAccessExpressionSyntax member:
            {
                var name = member.Name.Identifier.ValueText;
                if (IsStaticAccess(member, context))
                    // Static properties (MinValue/MaxValue) map to factory methods.
                    return $"$eq.time.{StaticKind(member, context)}.{name.ToCamelCase()}()";
                var receiver = context.Converter.ConvertExpression(member.Expression);
                return $"{receiver}.{name.ToCamelCase()}";
            }

            default:
                return context.Unhandled(node, "DateOnly/TimeOnly");
        }
    }

    /// <summary>Kind ("dateOnly"/"timeOnly") for a member access, by receiver type then member symbol.</summary>
    private static string? Kind(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var byReceiver = KindOf(context.SemanticHelper.GetType(ma.Expression));
        if (byReceiver != null) return byReceiver;
        var symbol = context.SemanticHelper.GetSymbol(ma);
        var bySymbol = symbol?.ContainingType != null ? KindOf(symbol.ContainingType) : null;
        if (bySymbol != null) return bySymbol;
        // Type NAME only where guessing is honest — a user type named DateOnly must not route here.
        return context.CanGuess(ma.Expression) ? KindOfName(ma.Expression.ToString()) : null;
    }

    private static string StaticKind(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        var bySymbol = symbol?.ContainingType != null ? KindOf(symbol.ContainingType) : null;
        return bySymbol
            ?? (context.CanGuess(ma.Expression) ? KindOfName(ma.Expression.ToString()) : null)
            ?? "dateOnly";
    }

    private static bool IsStaticAccess(MemberAccessExpressionSyntax ma, ConversionContext context)
    {
        var symbol = context.SemanticHelper.GetSymbol(ma);
        if (symbol != null) return symbol.IsStatic;
        return context.CanGuess(ma.Expression)
            && KindOfName(ma.Expression.ToString()) != null; // "DateOnly"/"TimeOnly"
    }

    /// <summary>
    /// Whether the twin, or its factory for a static, carries the member <paramref name="ma"/> names: by
    /// its name, where a call has none of its parameters an out, which no twin hands back. Parse and
    /// TryParse are TimeParseStrategy's, which runs first. Where no model says which type it is, the name
    /// is all there is.
    /// </summary>
    private static bool Carried(MemberAccessExpressionSyntax ma, IMethodSymbol? method, ConversionContext context)
    {
        if (method?.Parameters.Any(parameter => parameter.RefKind is RefKind.Out or RefKind.Ref) == true) return false;
        if (Kind(ma, context) is not { } kind || !Members.TryGetValue(kind, out var members)) return false;
        var name = ma.Name.Identifier.ValueText;
        return IsStaticAccess(ma, context) ? members.Static.Contains(name) : members.Instance.Contains(name);
    }

    private static string? KindOf(ITypeSymbol? type) => type?.ToDisplayString() switch
    {
        "System.DateOnly" => "dateOnly",
        "System.TimeOnly" => "timeOnly",
        _ => null,
    };

    private static string? KindOfName(string name) =>
        name == "DateOnly" ? "dateOnly" : name == "TimeOnly" ? "timeOnly" : null;

    public override int Priority => 15;
}
