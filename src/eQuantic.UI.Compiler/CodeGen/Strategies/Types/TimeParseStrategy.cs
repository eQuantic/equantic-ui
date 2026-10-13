using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Types;

/// <summary>
/// <c>Parse</c> and <c>TryParse</c> of a <c>DateTime</c>, a <c>DateTimeOffset</c>, a <c>DateOnly</c>, a
/// <c>TimeOnly</c> and a <c>TimeSpan</c>, read in the culture the call names, as .NET reads them (#718,
/// #806, #717). With no provider, with <c>CultureInfo.CurrentCulture</c> and with a null one, the twin
/// reads the page's culture, as .NET reads the current one; with <c>CultureInfo.InvariantCulture</c> it
/// reads the invariant one; any other provider fails the build (EQ2108), where it crossed as the name
/// <c>CultureInfo</c>, which no browser defines. Styles that are only white space are what Parse does
/// anyway and are left out; any other, and a span, fail the build (EQ1004). <c>TryParse</c> answers
/// true with the value read in its out argument, or false with the type's zero there, lowered inline as
/// <c>Guid.TryParse</c>'s is. Decided by the bound symbol, and by the spelling only where no model can be
/// asked, where only <c>Parse(text)</c> and <c>TryParse(text, out value)</c> are read.
/// </summary>
public class TimeParseStrategy : IExpressionIrStrategy
{
    /// <summary>What a reader of a date does, as EQ2108 names it for a culture the browser does not know;
    /// <c>Convert.ToDateTime</c> reads by the same rule.</summary>
    internal const string ReadingADate = "reading a date or a time";

    /// <summary>Each type's twin, by the name C# spells the type with.</summary>
    private static readonly Dictionary<string, string> Twins = new(StringComparer.Ordinal)
    {
        ["DateTime"] = Eq.DateTime,
        ["DateTimeOffset"] = Eq.DateTimeOffset,
        ["DateOnly"] = "$eq.time.dateOnly",
        ["TimeOnly"] = "$eq.time.timeOnly",
        ["TimeSpan"] = Eq.TimeSpan,
    };

    /// <summary>The styles a reader takes and does nothing with: <c>AllowWhiteSpaces</c> and its three
    /// parts, since Parse reads past white space whatever it is told.</summary>
    private const int WhiteSpaceStyles = 7;

    public bool CanConvert(SyntaxNode node, ConversionContext context) => Kind(node, context) is not null;

    /// <summary>
    /// The type's name where <paramref name="node"/> is a <c>Parse</c> or a <c>TryParse</c> of one of the
    /// five, or null: by the method C# bound, or, where no model can be asked, by the type's spelling.
    /// </summary>
    private static string? Kind(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Parse" or "TryParse" } access,
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

    /// <summary>The arguments of a call, each by the parameter it binds to.</summary>
    private sealed record Arguments(ArgumentSyntax Text, ArgumentSyntax? Provider, ArgumentSyntax? Styles, ArgumentSyntax? Result);

    /// <summary>
    /// Which argument is the text, the provider, the styles and the out result, by the parameter each
    /// binds to, by its name or its place; or, where no model can be asked, the text and the out result
    /// of <c>Parse(text)</c> and <c>TryParse(text, out value)</c>, and null for anything else. Null as well
    /// for the text of a span, which no twin reads.
    /// </summary>
    private static Arguments? ArgumentsOf(InvocationExpressionSyntax invocation, bool tries, ConversionContext context)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (context.SemanticHelper.GetSymbol(invocation) is not IMethodSymbol method)
        {
            var result = arguments.FirstOrDefault(argument => argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword));
            var others = arguments.Where(argument => argument != result).ToList();
            return others.Count == 1 && (tries ? result is not null && arguments.Count == 2 : result is null)
                ? new Arguments(others[0], null, null, result)
                : null;
        }

        ArgumentSyntax? text = null, provider = null, styles = null, outResult = null;
        for (var i = 0; i < arguments.Count; i++)
        {
            var named = arguments[i].NameColon?.Name.Identifier.ValueText;
            var parameter = named is null
                ? i < method.Parameters.Length ? method.Parameters[i] : null
                : method.Parameters.FirstOrDefault(p => p.Name == named);
            if (parameter is null) return null;
            if (parameter.RefKind == RefKind.Out) outResult = arguments[i];
            else if (parameter.Type.SpecialType == SpecialType.System_String) text = arguments[i];
            else if (parameter.Type.ToDisplayString() is "System.IFormatProvider" or "System.IFormatProvider?") provider = arguments[i];
            else if (parameter.Type.ToDisplayString() == "System.Globalization.DateTimeStyles") styles = arguments[i];
            else return null;
        }
        return text is null ? null : new Arguments(text, provider, styles, outResult);
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var kind = Kind(node, context)!;
        var name = ((MemberAccessExpressionSyntax)invocation.Expression).Name.Identifier.Text;
        var tries = name == "TryParse";
        if (ArgumentsOf(invocation, tries, context) is not { } call || tries != (call.Result is not null))
            return JsExpr.Opaque(context.Unhandled(invocation, context.SemanticHelper.GetSymbol(invocation) is IMethodSymbol
                ? $"{kind}.{name} with a span"
                : $"{kind}.{name} with a provider, styles or a span, which no model binds"));

        // The styles that are white space are what Parse does with none; any other changes what is read.
        if (call.Styles is not null
            && !(context.SemanticHelper.TryGetConstantValue(call.Styles.Expression, out var constant)
                && constant is int styles && (styles & ~WhiteSpaceStyles) == 0))
            return JsExpr.Opaque(context.Unhandled(invocation, $"{kind}.{name} with styles that are not white space"));

        var invariant = NamedCulture.ReadsInvariant(invocation, call.Provider?.Expression, context, ReadingADate);
        context.UsedHelpers.Add(Eq.Import);
        List<JsExpr> read = [context.Converter.ConvertIr(call.Text.Expression)];
        if (invariant) read.Add(JsExpr.Literal("true"));
        if (!tries) return JsExpr.Call(JsExpr.Identifier($"{Twins[kind]}.parse"), read);
        return JsExpr.Callish(OutArgument.TryAnswer(call.Result!, call.Text.SpanStart,
            JsExpr.Call(JsExpr.Identifier($"{Twins[kind]}.tryParse"), read),
            DefaultValue.TimeZero("System." + kind)!, context));
    }

    // Above the five types' own strategies (15), which claim every member of their type.
    public int Priority => 16;
}
