using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// <c>TryParse(string, out T)</c> of a <c>DateTime</c>, a <c>DateTimeOffset</c>, a <c>DateOnly</c>, a
/// <c>TimeOnly</c> and a <c>TimeSpan</c> (#718): true with the value read in the out argument, or false
/// with the type's zero there, as .NET leaves it. The twin's <c>tryParse</c> reads the text as its
/// <c>Parse</c> does and answers undefined where Parse throws, and the answer is lowered inline, as
/// <c>Guid.TryParse</c>'s is. Each type's own strategy handed the out argument to a <c>tryParse</c> as
/// a second argument: four of the five twins had none, a TypeError on the page, and the fifth answered
/// the date or null. Every other overload (a provider, styles, a span) fails the build (EQ1004): handed
/// to the twin, it would read the text and never write its out argument. Translating them is #717's.
/// Decided by the bound symbol, and by the spelling only where no model can be asked.
/// </summary>
public class TimeTryParseStrategy : IExpressionIrStrategy
{
    /// <summary>Each type's twin, by the name C# spells the type with.</summary>
    private static readonly Dictionary<string, string> Twins = new(StringComparer.Ordinal)
    {
        ["DateTime"] = Eq.DateTime,
        ["DateTimeOffset"] = Eq.DateTimeOffset,
        ["DateOnly"] = "$eq.time.dateOnly",
        ["TimeOnly"] = "$eq.time.timeOnly",
        ["TimeSpan"] = Eq.TimeSpan,
    };

    public bool CanConvert(SyntaxNode node, ConversionContext context) => Kind(node, context) is not null;

    /// <summary>
    /// The type's name where <paramref name="node"/> is a <c>TryParse</c> of one of the five, or null: by
    /// the method C# bound, or, where no model can be asked, by the type's spelling.
    /// </summary>
    private static string? Kind(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "TryParse" } access,
            } invocation)
            return null;
        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method)
            return method is { IsStatic: true, ContainingType: { } type }
                && Twins.ContainsKey(type.Name) && type.ToDisplayString() == "System." + type.Name
                ? type.Name
                : null;
        var spelled = access.Expression.ToString();
        if (spelled.StartsWith("System.", StringComparison.Ordinal)) spelled = spelled["System.".Length..];
        return context.CanGuess(invocation) && Twins.ContainsKey(spelled) ? spelled : null;
    }

    /// <summary>
    /// Whether the call is <c>TryParse(string, out T)</c>: by the overload C# bound, or, where no model
    /// can be asked, by two arguments, one of them <c>out</c>, which no other overload of the five takes
    /// but the span's, and a span is not spelled with no model to say so.
    /// </summary>
    private static bool ReadsText(InvocationExpressionSyntax invocation, ConversionContext context)
    {
        if (context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol method)
            return method is
            {
                ContainingType: { } type,
                Parameters: [{ Type.SpecialType: SpecialType.System_String }, { RefKind: RefKind.Out } result],
            } && SymbolEqualityComparer.Default.Equals(result.Type, type);
        var arguments = invocation.ArgumentList.Arguments;
        return arguments.Count == 2
            && arguments.Count(argument => argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)) == 1;
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var kind = Kind(node, context)!;
        if (!ReadsText(invocation, context))
            return JsExpr.Opaque(context.Unhandled(invocation, $"{kind}.TryParse with a provider, styles or a span"));
        var arguments = invocation.ArgumentList.Arguments;
        var result = arguments.First(argument => argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword));
        var input = arguments.First(argument => argument != result);
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Callish(OutArgument.TryAnswer(result, input.SpanStart,
            JsExpr.Call(JsExpr.Identifier($"{Twins[kind]}.tryParse"), context.Converter.ConvertIr(input.Expression)),
            DefaultValue.TimeZero("System." + kind)!, context));
    }

    // Above the five types' own strategies (15), which claim every member of their type.
    public int Priority => 16;
}
