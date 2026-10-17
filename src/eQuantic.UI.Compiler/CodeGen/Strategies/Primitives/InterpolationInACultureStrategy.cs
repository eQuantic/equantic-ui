using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// An interpolation written in the culture a call names (#839): <c>FormattableString.Invariant($"…")</c>
/// and <c>string.Create(CultureInfo.InvariantCulture, $"…")</c> write every hole in the invariant culture,
/// and <c>FormattableString.CurrentCulture($"…")</c> and <c>string.Create</c> with the current culture or
/// a null provider in the page's, as .NET writes them. They went out as names nothing defines,
/// <c>FormattableString.invariant(…)</c> and <c>string.create(…)</c>. The interpolation is the one the
/// call is handed, written by <c>InterpolatedStringStrategy</c> and <c>ValueFlow</c> in the culture this
/// registers for it (<see cref="ConversionContext.InterpolationCultures"/>); another culture is EQ2108, as
/// it is for every call that reads one, and a <c>FormattableString</c> made elsewhere is EQ2004: the browser
/// holds the text it wrote, not its format and its arguments. <c>string.Create</c>'s other overloads, a
/// buffer and a span action, are left to the call fence, which refuses them.
/// </summary>
public class InterpolationInACultureStrategy : IExpressionIrStrategy
{
    /// <summary>What a refusal of another culture says the call was doing.</summary>
    private const string Writing = "writing an interpolation";

    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is InvocationExpressionSyntax invocation && Interpolated(invocation, context) is not null;

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var (method, argument, provider) = Interpolated(invocation, context)!.Value;
        if (Bare(argument.Expression) is not InterpolatedStringExpressionSyntax interpolated)
        {
            context.Report(argument.Expression, ConversionSeverity.Error, "EQ2004",
                $"'{method.ContainingType.ToDisplayString()}.{method.Name}' of a 'System.FormattableString' made elsewhere has "
                + "no JavaScript translation: the browser holds the text it wrote, in the page's culture, not its format and "
                + $"its arguments. Hand {method.Name} the interpolation itself.");
            return JsExpr.Literal("''");
        }

        bool invariant;
        if (provider is null) invariant = method.Name == "Invariant";
        else
        {
            var reported = context.Diagnostics.Count;
            invariant = NamedCulture.ReadsInvariant(invocation, provider.Expression, context, Writing);
            if (context.Diagnostics.Count > reported) return JsExpr.Literal("''");
        }
        context.InterpolationCultures[context.SemanticHelper.Original(interpolated)] = invariant;
        // string.Create's handler formats each hole as it is evaluated; a FormattableString holds its
        // arguments and formats them once every hole has run.
        return provider is null ? Captured(interpolated, context) : context.Converter.ConvertIr(interpolated);
    }

    /// <summary>
    /// A FormattableString's text, as .NET writes it: every hole evaluated first, in order, into a name of its
    /// own, and the text written from those values. Formatting each hole where it stands, a later hole's
    /// effect was missing from an earlier one's text: `{sb}|{sb.Append("b")}` wrote `a|ab` where .NET writes
    /// `ab|ab` (Copilot's review of #958). The names start with `$h`, which neither C# nor a template's own
    /// bindings (`$0`) can spell.
    /// </summary>
    private static JsExpr Captured(InterpolatedStringExpressionSyntax interpolated, ConversionContext context)
    {
        var holes = interpolated.Contents.OfType<InterpolationSyntax>().ToList();
        if (holes.Count == 0) return context.Converter.ConvertIr(interpolated);
        foreach (var hole in holes) context.CapturedHoles.Add(context.SemanticHelper.Original(hole.Expression));
        var values = holes.Select(hole => context.Converter.ConvertIr(hole.Expression)).ToList();
        var names = holes.Select((_, i) => $"$h{i}").ToList();
        var text = new StringBuilder("`");
        var at = 0;
        foreach (var content in interpolated.Contents)
        {
            if (content is InterpolatedStringTextSyntax part)
                text.Append(JsStringLiteral.TemplateText(InterpolatedStringStrategy.Text(part, interpolated)));
            else if (content is InterpolationSyntax hole)
                text.Append("${").Append(InterpolatedStringStrategy.Hole(hole, context, padded: true, captured: names[at++])).Append('}');
        }
        text.Append('`');
        return JsExpr.Call(JsExpr.Group(JsExpr.Arrow(string.Join(", ", names), JsExpr.Opaque(text.ToString()))), values);
    }

    /// <summary>
    /// The method <paramref name="invocation"/> calls, the argument that holds its interpolation and its
    /// provider's argument, where it is <c>FormattableString.Invariant</c>, <c>FormattableString.CurrentCulture</c>
    /// or <c>string.Create(IFormatProvider, ref DefaultInterpolatedStringHandler)</c>; null for any other call.
    /// Each argument is found by the parameter it binds to.
    /// </summary>
    private static (IMethodSymbol Method, ArgumentSyntax Argument, ArgumentSyntax? Provider)? Interpolated(
        InvocationExpressionSyntax invocation, ConversionContext context)
    {
        if (context.SemanticHelper.GetSymbol(invocation) is not IMethodSymbol { IsStatic: true } method) return null;
        var formattable = method is { Name: "Invariant" or "CurrentCulture", Parameters.Length: 1 }
            && method.ContainingType.IsNamed("System.FormattableString");
        var created = method is { Name: "Create", Parameters: [{ Type.Name: "IFormatProvider" }, { Type.Name: "DefaultInterpolatedStringHandler" }] }
            && method.ContainingType.SpecialType == SpecialType.System_String;
        if (!formattable && !created) return null;
        ArgumentSyntax? Bound(int ordinal) =>
            invocation.ArgumentList.Arguments.FirstOrDefault(argument => argument.NameColon?.Name.Identifier.ValueText == method.Parameters[ordinal].Name)
            ?? (ordinal < invocation.ArgumentList.Arguments.Count && invocation.ArgumentList.Arguments[ordinal].NameColon is null
                ? invocation.ArgumentList.Arguments[ordinal]
                : null);
        return formattable
            ? Bound(0) is { } text ? (method, text, null) : null
            : Bound(1) is { } handler && Bound(0) is { } provider ? (method, handler, provider) : null;
    }

    /// <summary>The expression parentheses name.</summary>
    private static ExpressionSyntax Bare(ExpressionSyntax expression) =>
        expression is ParenthesizedExpressionSyntax parenthesized ? Bare(parenthesized.Expression) : expression;

    public int Priority => 20;
}
