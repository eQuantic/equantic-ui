using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// <c>Parse</c> and <c>TryParse</c> on every numeric type: the eight integers, <c>float</c>,
/// <c>double</c> and <c>decimal</c> (#358, #376). The runtime reads the text with .NET's grammar,
/// under the NumberStyles the call names or the type's own, holds the digits as the type does, and
/// throws what .NET throws with its words. <c>parseInt</c> and <c>parseFloat</c> did none of it:
/// they read the longest prefix that looked like a number, so "12abc" was 12 and "1,5" was 1, an int
/// kept digits past its range, a long became a number where every other long is a BigInt, and a
/// failed TryParse left NaN in its <c>out</c>.
/// </summary>
public class NumberMethodStrategy : IExpressionIrStrategy
{
    /// <summary>How a numeric type is read on this side: the runtime's Parse and TryParse, the tag
    /// that names the type to them (none for a decimal, which has readers of its own), and the zero
    /// a failed TryParse leaves in its <c>out</c>.</summary>
    private sealed record Reader(string Parse, string TryParse, string? Tag, string Zero);

    private static readonly Dictionary<SpecialType, Reader> Readers = new()
    {
        [SpecialType.System_Decimal] = new(Eq.DecParse, Eq.DecTryParse, null, $"{Eq.Dec}(0)"),
        [SpecialType.System_Double] = new(Eq.RealParse, Eq.RealTryParse, "double", "0"),
        [SpecialType.System_Single] = new(Eq.RealParse, Eq.RealTryParse, "single", "0"),
        [SpecialType.System_Byte] = Integer("byte"),
        [SpecialType.System_SByte] = Integer("sbyte"),
        [SpecialType.System_Int16] = Integer("short"),
        [SpecialType.System_UInt16] = Integer("ushort"),
        [SpecialType.System_Int32] = Integer("int"),
        [SpecialType.System_UInt32] = Integer("uint"),
        [SpecialType.System_Int64] = new(Eq.IntParse, Eq.IntTryParse, "long", "0n"),
        [SpecialType.System_UInt64] = new(Eq.IntParse, Eq.IntTryParse, "ulong", "0n"),
    };

    private static Reader Integer(string tag) => new(Eq.IntParse, Eq.IntTryParse, tag, "0");

    /// <summary>The names a receiver may be written with, for a call no model bound.</summary>
    private static readonly Dictionary<string, SpecialType> Names = new()
    {
        ["decimal"] = SpecialType.System_Decimal, ["Decimal"] = SpecialType.System_Decimal,
        ["double"] = SpecialType.System_Double, ["Double"] = SpecialType.System_Double,
        ["float"] = SpecialType.System_Single, ["Single"] = SpecialType.System_Single,
        ["byte"] = SpecialType.System_Byte, ["Byte"] = SpecialType.System_Byte,
        ["sbyte"] = SpecialType.System_SByte, ["SByte"] = SpecialType.System_SByte,
        ["short"] = SpecialType.System_Int16, ["Int16"] = SpecialType.System_Int16,
        ["ushort"] = SpecialType.System_UInt16, ["UInt16"] = SpecialType.System_UInt16,
        ["int"] = SpecialType.System_Int32, ["Int32"] = SpecialType.System_Int32,
        ["uint"] = SpecialType.System_UInt32, ["UInt32"] = SpecialType.System_UInt32,
        ["long"] = SpecialType.System_Int64, ["Int64"] = SpecialType.System_Int64,
        ["ulong"] = SpecialType.System_UInt64, ["UInt64"] = SpecialType.System_UInt64,
    };

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        var name = memberAccess.Name.Identifier.Text;
        if (name is not ("Parse" or "TryParse")) return false;

        return context.ReceiverIsType(memberAccess.Expression,
            named => Readers.ContainsKey(named.SpecialType),
            [.. Names.Keys, .. Names.Keys.Select(key => $"System.{key}")]);
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var name = memberAccess.Name.Identifier.Text;
        var method = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        var type = method?.ContainingType.SpecialType
            ?? Names.GetValueOrDefault(memberAccess.Expression.ToString().Replace("System.", ""));
        return Readers.TryGetValue(type, out var reader)
            ? Text(invocation, name, method, reader, context)
            : JsExpr.Opaque(context.Unhandled(invocation, "numeric Parse/TryParse"));
    }

    /// <summary>
    /// The call, read by the runtime. The style reaches it as the number a NumberStyles holds; the
    /// provider does not, since the twin reads the invariant culture (EQ2110), and the culture it
    /// names decides whether that is faithful (see <see cref="ParseCulture"/>). The bound method says
    /// which argument fills which parameter, so a named one lands in its own slot, and the template
    /// keeps them in the order C# evaluates them. A TryParse that fails leaves 0 in its <c>out</c>,
    /// as .NET does.
    /// </summary>
    private static JsExpr Text(InvocationExpressionSyntax invocation, string name, IMethodSymbol? method,
        Reader reader, ConversionContext context)
    {
        var arguments = invocation.ArgumentList.Arguments;
        int? text = null, style = null, result = null, provider = null;
        if (method is not null)
        {
            // The text is a string or a span of chars; a span of UTF-8 bytes has no twin to read.
            if (method.Parameters is not [{ Type: var first }, ..]
                || !(first.SpecialType == SpecialType.System_String || IsSpanOfChar(first)))
            {
                return JsExpr.Opaque(context.Unhandled(invocation, "numeric Parse/TryParse over UTF-8 bytes"));
            }
            for (var i = 0; i < arguments.Count; i++)
            {
                var named = arguments[i].NameColon?.Name.Identifier.ValueText;
                var parameter = named is null
                    ? (i < method.Parameters.Length ? method.Parameters[i] : null)
                    : method.Parameters.FirstOrDefault(p => p.Name == named);
                if (parameter is null) return JsExpr.Opaque(context.Unhandled(invocation, "numeric Parse/TryParse"));
                if (parameter.RefKind == RefKind.Out) result = i;
                else if (parameter.Ordinal == 0) text = i;
                else if (parameter.Type is { Name: "NumberStyles", ContainingNamespace: var home }
                         && home.ToDisplayString() == "System.Globalization") style = i;
                else provider = i;
            }
        }
        else if (arguments.Count == (name == "TryParse" ? 2 : 1))
        {
            // No model bound the call: the text comes first and TryParse's out last.
            text = 0;
            if (name == "TryParse") result = 1;
        }
        else
        {
            // Anything between the text and the out is a style or a provider, and only the bound
            // overload can tell which: a guess would parse under a style nobody wrote.
            return JsExpr.Opaque(context.Unhandled(invocation, "numeric Parse/TryParse"));
        }
        if (text is not { } at || (name == "TryParse") != result.HasValue)
            return JsExpr.Opaque(context.Unhandled(invocation, "numeric Parse/TryParse"));
        ParseCulture.Check(invocation, provider is { } culture ? arguments[culture].Expression : null, context);

        context.UsedHelpers.Add(Eq.Import);
        string Call(string function, string textHole, string? styleHole) =>
            $"{function}({textHole}{(reader.Tag is { } tag ? $", '{tag}'" : "")}{(styleHole is null ? "" : $", {styleHole}")})";

        // The parts in the order they were written; each hole names its part by that order.
        var read = new[] { text, style }.OfType<int>().Order().ToList();
        var parts = read.Select(i => context.Converter.ConvertIr(arguments[i].Expression)).ToArray();
        string Hole(int argument) => "{" + read.IndexOf(argument) + "}";

        if (name == "Parse")
        {
            return JsExpr.Template(Call(reader.Parse, Hole(at), style is { } s ? Hole(s) : null),
                parts, context.TypeAnnotations);
        }

        var parsed = Call(reader.TryParse, Hole(at), style is { } styleArgument ? Hole(styleArgument) : null);
        var outArgument = arguments[result!.Value];
        if (OutArgument.IsDiscard(outArgument, context))
            return JsExpr.Template($"({parsed} !== undefined)", parts, context.TypeAnnotations);
        var target = OutArgument.Target(outArgument, context);
        if (OutArgument.IsBareName(target))
            return JsExpr.Template(
                $"(({target} = {parsed}) !== undefined || (({target} = {reader.Zero}), false))",
                parts, context.TypeAnnotations);
        // A target with an effect of its own (`out values[index++]`) is written by ONE branch, once
        // the parsed value is bound, so its effect runs once, as C#'s out does: named in both halves
        // of an `||`, a failed parse stepped `index` twice and left the zero in the next element.
        // And its receiver and key are evaluated where the argument was WRITTEN, as C# evaluates
        // every argument: a named argument can put the out first (`TryParse(result: out
        // values[index++], s: S())`), and then `index` steps before S() runs.
        var targetParts = new List<JsExpr>();
        var place = OutArgument.Place(outArgument, context, part =>
        {
            targetParts.Add(part);
            return $"{{t{targetParts.Count - 1}}}";
        });
        var ordered = read.Select(i => (Argument: i, Sub: 0, Part: context.Converter.ConvertIr(arguments[i].Expression), Key: $"a{i}"))
            .Concat(targetParts.Select((part, sub) => (Argument: result.Value, Sub: sub, Part: part, Key: $"t{sub}")))
            .OrderBy(entry => entry.Argument).ThenBy(entry => entry.Sub).ToList();
        string Numbered(string written) => ordered.Select((entry, position) => (entry.Key, position))
            .Aggregate(written, (current, hole) => current.Replace("{" + hole.Key + "}", "{" + hole.position + "}"));
        var call = Call(reader.TryParse, $"{{a{at}}}", style is { } keyed ? $"{{a{keyed}}}" : null);
        var value = context.TypeAnnotations ? "($r: any)" : "($r)";
        return JsExpr.Template(
            Numbered($"({value} => ($r !== undefined ? (({place} = $r), true) : (({place} = {reader.Zero}), false)))({call})"),
            [.. ordered.Select(entry => entry.Part)], context.TypeAnnotations);
    }

    private static bool IsSpanOfChar(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "ReadOnlySpan", TypeArguments: [{ SpecialType: SpecialType.System_Char }] };

    public int Priority => 10;
}
