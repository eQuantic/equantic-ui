using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

/// <summary>
/// A range used to INDEX — <c>line[start..end]</c>, <c>text[..n]</c>, <c>name[^3..]</c>. In C# that
/// is a slice; in JavaScript it is <c>.slice(from, to)</c>, whose negative arguments already carry
/// the meaning <c>^</c> has. So the ordinary shapes cost nothing at runtime.
/// <para>
/// What used to happen is why this exists: the range converted to a <c>{ start, end }</c> object and
/// the element access wrapped it in brackets, producing <c>text[{ start: 0, end: 4 }]</c> — which is
/// <c>undefined</c> at runtime, in every case, with no diagnostic. A tokenizer written in C# and
/// realized on the web drew nothing and said nothing about why.
/// </para>
/// <para>
/// One shape JS cannot say directly: a from-end endpoint that may be ZERO. <c>slice(0, -0)</c> is
/// <c>slice(0, 0)</c> — empty — where C#'s <c>[..^0]</c> means the whole value. Anything but a
/// positive literal after <c>^</c> therefore goes through <c>$eq.slice</c>, which resolves both
/// endpoints against the length exactly as <c>Index.GetOffset</c> does.
/// </para>
/// <para>
/// A range over a type eqc writes goes through the member the bound tree names (#585). Its
/// <c>Slice(int start, int length)</c> takes a LENGTH where JavaScript's <c>slice</c> takes an end, and
/// the twin's <c>slice</c> is that member, so <c>x[1..3]</c> called <c>Slice(1, 3)</c>, three elements
/// where .NET answers two: it is called as C# lowers the range, its receiver once, then the endpoints in
/// their order, then the <c>Length</c> or <c>Count</c> the bound tree names, read only where an endpoint
/// counts from the end or the end is left open, then <c>Slice(start, end - start)</c>. An indexer that
/// takes the <c>Range</c> itself would be handed a System.Range value, which has no translation
/// (<see cref="RangeExpressionStrategy"/>), and is refused where it was a call of a <c>slice</c> the twin
/// does not have.
/// </para>
/// </summary>
public class RangeIndexerStrategy : IExpressionIrStrategy
{
    // A dictionary keyed by Range is not a slice: `d[1..2]` looks the key up (DictionaryEntry), and
    // a Range key is a Range VALUE, which RangeExpressionStrategy fences (EQ2004), so the build says
    // so at the key where this emitted `.slice(1, 2)` on a map and reported nothing.
    public bool CanConvert(SyntaxNode node, ConversionContext context) =>
        node is ElementAccessExpressionSyntax { ArgumentList.Arguments: [{ Expression: RangeExpressionSyntax }] } access
        && DictionaryEntry.Of(access, context) is null;

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var access = (ElementAccessExpressionSyntax)node;
        var range = (RangeExpressionSyntax)access.ArgumentList.Arguments[0].Expression;

        switch (context.SemanticHelper.GetOperation(access))
        {
            case IImplicitIndexerReferenceOperation { IndexerSymbol: IMethodSymbol slice, LengthSymbol: IPropertySymbol length }
                when ObjectCreationStrategy.TwinIsWritten(slice.ContainingType):
                return Sliced(access, range, slice, length, context);
            case IPropertyReferenceOperation { Property: { IsIndexer: true, Parameters: [{ Type: var key }] } indexer }
                when key.ToDisplayString() == "System.Range":
                RangeExpressionStrategy.RefuseAsAKey(range, indexer, context);
                return JsExpr.Opaque(access.ToString());
        }

        var receiver = context.Converter.ConvertIr(access.Expression);
        var start = Endpoint(range.LeftOperand, context, isStart: true);
        var end = Endpoint(range.RightOperand, context, isStart: false);

        if (start.Direct is { } from && end.Direct is { } to)
            return JsExpr.Call(JsExpr.Member(receiver, "slice"), from, to);
        if (start.Direct is { } only && end.Omitted)
            return JsExpr.Call(JsExpr.Member(receiver, "slice"), only);

        // The endpoint could be zero-from-the-end, where a negative index would mean the opposite.
        context.UsedHelpers.Add(Eq.Import);
        return JsExpr.Call(JsExpr.Identifier("$eq.slice"), receiver, start.Value, JsExpr.Literal(start.FromEnd ? "true" : "false"),
            end.Value, JsExpr.Literal(end.FromEnd ? "true" : "false"));
    }

    /// <summary>
    /// <c>x[a..b]</c> over a twin with a <c>Slice(start, length)</c>, as C# lowers it: the receiver, then
    /// each endpoint written, then the count only where one is needed, then the slice. Where no endpoint
    /// needs the count, a template over the parts: <c>x.slice(a, b - a)</c>, the start bound once. Where
    /// one does, a function handed the receiver and the endpoints in that order, which reads the count
    /// once, after them, as C# reads it.
    /// </summary>
    private static JsExpr Sliced(ElementAccessExpressionSyntax access, RangeExpressionSyntax range, IMethodSymbol slice,
        IPropertySymbol length, ConversionContext context)
    {
        var receiver = context.Converter.ConvertIr(access.Expression);
        var member = slice.Name.ToCamelCase();
        var (start, startFromEnd) = Operand(range.LeftOperand, context);
        var (end, endFromEnd) = Operand(range.RightOperand, context);

        if (!startFromEnd && !endFromEnd && end is not null)
        {
            return start is null
                ? JsExpr.Template($"{{0}}.{member}(0, {{1}})", [receiver, end], context.TypeAnnotations)
                : JsExpr.Template($"{{0}}.{member}({{1}}, {{2}} - {{1}})", [receiver, start, end], context.TypeAnnotations);
        }

        // `this` and `super` are read where they stand, an arrow taking them from its function: `super`
        // is no value an arrow could be handed.
        var fixedReceiver = receiver is JsIdentifier { Name: "this" or "super" };
        var self = fixedReceiver ? receiver : JsExpr.Identifier("$r");
        var count = JsExpr.Identifier("$n");
        var from = start is null ? JsExpr.Literal("0") : startFromEnd ? JsExpr.Binary(count, "-", JsExpr.Identifier("$s")) : JsExpr.Identifier("$s");
        var to = end is null ? count : endFromEnd ? JsExpr.Binary(count, "-", JsExpr.Identifier("$e")) : JsExpr.Identifier("$e");
        var annotated = context.TypeAnnotations;
        List<string> parameters = fixedReceiver ? [] : [annotated ? "$r: any" : "$r"];
        List<JsExpr> arguments = fixedReceiver ? [] : [receiver];
        if (start is not null)
        {
            parameters.Add(annotated ? "$s: number" : "$s");
            arguments.Add(start);
        }
        if (end is not null)
        {
            parameters.Add(annotated ? "$e: number" : "$e");
            arguments.Add(end);
        }
        var body = JsStatement.Block([
            JsStatement.Const("$n", JsExpr.Member(self, length.Name.ToCamelCase())),
            JsStatement.Return(JsExpr.Call(JsExpr.Member(self, member), from, JsExpr.Binary(to, "-", from))),
        ]);
        return JsExpr.Call(JsExpr.ArrowBlock(string.Join(", ", parameters), body, context.Layout, context.Depth), arguments);
    }

    /// <summary>An endpoint of a range as C# evaluates it: its value, without the <c>^</c>, and whether it
    /// counts from the end; null for one left out.</summary>
    private static (JsExpr? Value, bool FromEnd) Operand(ExpressionSyntax? operand, ConversionContext context) => operand switch
    {
        null => (null, false),
        PrefixUnaryExpressionSyntax hat when hat.IsKind(SyntaxKind.IndexExpression) => (context.Converter.ConvertIr(hat.Operand), true),
        _ => (context.Converter.ConvertIr(operand), false),
    };

    /// <summary>One end of the range: what it is, and whether `.slice` can take it as it stands.</summary>
    private readonly record struct RangeEnd(JsExpr Value, bool FromEnd, JsExpr? Direct, bool Omitted);

    private static RangeEnd Endpoint(ExpressionSyntax? operand, ConversionContext context, bool isStart)
    {
        // Omitted: `..end` starts at 0, `start..` runs to the end.
        if (operand is null)
            return isStart
                ? new RangeEnd(JsExpr.Literal("0"), false, JsExpr.Literal("0"), false)
                : new RangeEnd(JsExpr.Literal("null"), false, null, true);

        if (operand is not PrefixUnaryExpressionSyntax hat || !hat.IsKind(SyntaxKind.IndexExpression))
        {
            var value = context.Converter.ConvertIr(operand);
            return new RangeEnd(value, false, value, false);
        }

        var offset = context.Converter.ConvertIr(hat.Operand);
        // `^3` is `-3` to slice — but only once we know it is not `^0`, which slice reads as 0.
        var direct = hat.Operand is LiteralExpressionSyntax { Token.Value: int and > 0 }
            ? JsExpr.Literal($"-{JsExprWriter.Write(offset)}")
            : null;
        return new RangeEnd(offset, true, direct, false);
    }

    /// <summary>Above ElementAccessStrategy (1) and IndexFromEndStrategy (20), which claim the same
    /// syntax node for the non-range shapes.</summary>
    public int Priority => 25;
}
