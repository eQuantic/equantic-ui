using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// <c>new DateTime(…)</c> and <c>new DateTimeOffset(…)</c>, the target-typed <c>new(…)</c> included: the
/// runtime's factory for the constructor's SHAPE, each argument in the place of the parameter it binds
/// (#606). The factories took their components by how many arguments they got, so every overload past
/// the second read its arguments in the wrong places: a kind was read as the millisecond, a millisecond
/// as the offset, and a microsecond was dropped. The bound constructor names each parameter, so the shape
/// is read from it and never counted, and the arguments still run in the order they are written
/// (<see cref="ParameterTemplate"/>).
/// <para>
/// An overload that takes a <c>Calendar</c> is refused: the browser has none of .NET's calendars. A
/// creation the model cannot bind is refused too, where a count of its arguments was guessed at.
/// </para>
/// </summary>
public class DateTimeConstructionStrategy : IExpressionIrStrategy
{
    /// <summary>Ahead of <see cref="DateTimeStrategy"/> and <see cref="DateTimeOffsetStrategy"/>, which
    /// translate the two types' members.</summary>
    public int Priority => 16;

    /// <summary>A clock time's components, in the order the factories' <c>of</c> takes them.</summary>
    private static readonly string[] Components =
        ["year", "month", "day", "hour", "minute", "second", "millisecond", "microsecond"];

    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is BaseObjectCreationExpressionSyntax creation
        && (context.SemanticHelper.GetType(node) is { } type
            ? type.IsNamed("System.DateTime") || type.IsNamed("System.DateTimeOffset")
            : creation is ObjectCreationExpressionSyntax { Type: var named }
                && named.ToString() is "DateTime" or "DateTimeOffset");

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var creation = (BaseObjectCreationExpressionSyntax)node;
        if (context.SemanticHelper.GetSymbol(creation) is not IMethodSymbol constructor)
        {
            var created = context.SemanticHelper.GetType(node)?.Name
                ?? (creation as ObjectCreationExpressionSyntax)?.Type.ToString();
            return JsExpr.Opaque(context.Unhandled(node, $"{created} constructor"));
        }

        var offset = constructor.ContainingType.IsNamed("System.DateTimeOffset");
        var type = offset ? "DateTimeOffset" : "DateTime";
        if (constructor.Parameters.Any(parameter => parameter.Type.IsNamed("System.Globalization.Calendar")))
        {
            context.Report(node, ConversionSeverity.Error, "EQ1004",
                $"This {type} constructor takes a Calendar, and the browser has none of .NET's calendars. "
                + "Build the value from its Gregorian components, or convert it on the server.");
            return JsExpr.Opaque(creation.ToString());
        }

        context.UsedHelpers.Add(Eq.Import);
        var factory = offset ? Eq.DateTimeOffset : Eq.DateTime;
        return ParameterTemplate.Construction($"{factory}.{Shape(constructor, offset)}", creation, constructor, context);
    }

    /// <summary>
    /// The factory call over the constructor's parameters, a hole for each one it has: <c>{0}</c> is its
    /// first parameter. A component the constructor does not take is zero, where the factory reads one
    /// after it.
    /// </summary>
    private static string Shape(IMethodSymbol constructor, bool offset)
    {
        var holes = constructor.Parameters.ToDictionary(parameter => parameter.Name, parameter => $"{{{parameter.Ordinal}}}");
        string? Hole(string name) => holes.TryGetValue(name, out var hole) ? hole : null;
        var last = offset ? Hole("offset") : Hole("kind");

        if (constructor.Parameters.Length == 0) return "minValue()";
        if (Hole("ticks") is { } ticks) return Call("fromTicks", ticks, last);
        if (Hole("dateTime") is { } dateTime) return Call("fromDateTime", dateTime, last);
        if (Hole("date") is { } date) return Call("fromDateAndTime", date, Hole("time"), last);

        // Every component up to the last one the constructor takes, or all eight before an offset,
        // which DateTimeOffset's `of` takes last; a kind follows the components, after a zero for each
        // component it skips.
        var taken = Array.FindLastIndex(Components, name => Hole(name) is not null);
        var count = offset || last is not null ? Components.Length : taken + 1;
        var parts = Components.Take(count).Select(name => Hole(name) ?? "0");
        return Call("of", [.. parts, last]);
    }

    /// <summary>A call over the arguments given, the absent ones at the end left out.</summary>
    private static string Call(string name, params string?[] arguments)
    {
        var written = arguments.ToList();
        while (written.Count > 0 && written[^1] is null) written.RemoveAt(written.Count - 1);
        return $"{name}({string.Join(", ", written)})";
    }
}
