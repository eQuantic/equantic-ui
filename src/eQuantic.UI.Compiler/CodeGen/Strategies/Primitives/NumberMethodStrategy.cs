using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Primitives;

/// <summary>
/// Strategy for Number methods (int.Parse, double.TryParse, etc).
/// Handles:
/// - int.Parse(s) -> parseInt(s)
/// - double.Parse(s) -> parseFloat(s)
/// - int.TryParse(s, out var x) -> x = parseInt(s); return !isNaN(x)
/// - decimal.Parse and decimal.TryParse -> the runtime's reader, which answers a Decimal
/// </summary>
public class NumberMethodStrategy : IExpressionIrStrategy
{
    private static readonly HashSet<string> Types = new() { "int", "Int32", "double", "Double", "float", "Single", "decimal", "Decimal", "long", "Int64" };

    public bool CanConvert(SyntaxNode node, ConversionContext context)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return false;

        var name = memberAccess.Name.Identifier.Text;
        if (name is not ("Parse" or "TryParse")) return false;

        return context.ReceiverIsType(memberAccess.Expression,
            named => named.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64
                or SpecialType.System_Double or SpecialType.System_Single or SpecialType.System_Decimal,
            [.. Types]);
    }

    public JsExpr ConvertIr(SyntaxNode node, ConversionContext context)
    {
        var invocation = (InvocationExpressionSyntax)node;
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var type = memberAccess.Expression.ToString();
        var name = memberAccess.Name.Identifier.Text;
        var args = invocation.ArgumentList.Arguments;
        var method = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;

        if (method is { ContainingType.SpecialType: SpecialType.System_Decimal }
            || (method is null && type is "decimal" or "Decimal" or "System.Decimal"))
        {
            return DecimalText(invocation, name, method, context);
        }

        string parsMethod = (type == "int" || type == "Int32" || type == "long" || type == "Int64")
            ? "parseInt"
            : "parseFloat";
        // A float parsed from text is a single, like every other float this side produces
        // (SinglePrecision): `parseFloat("0.1")` is the double 0.1, and .NET's float.Parse is not.
        var parsesSingle = method is { ContainingType.SpecialType: SpecialType.System_Single }
            || type is "float" or "Single" or "System.Single";
        string Parsed(string input) => parsesSingle ? $"Math.fround({parsMethod}({input}))" : $"{parsMethod}({input})";

        if (name == "Parse")
        {
            var input = context.Converter.ConvertExpression(args[0].Expression);
            return JsExpr.Callish(Parsed(input));
        }

        if (name == "TryParse")
        {
            // int.TryParse(s, out var result)
            // Transform to IIFE: (() => { result = parseInt(s); return !isNaN(result); })()
            // BUT: This updates a local variable 'result'.
            // If the argument is `out var result` (DeclarationExpression), we need to handle scope.
            // If it's `out result` (IdentifierName), we assign to it.

            if (args.Count < 2) return JsExpr.Literal("false");

            var input = context.Converter.ConvertExpression(args[0].Expression);
            // The `out` is the LAST argument, not the second. `TryParse(s, out var n)` and
            // `TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)` are the same
            // method with the same answer, and taking args[1] on the four-argument overload wrote
            // the NUMBER STYLE where the variable belonged: `(511 = parseFloat(value), !isNaN(511))`,
            // which is not even syntax. The styles and the provider have no JS equivalent worth
            // emitting — `parseFloat` is already invariant and permissive — so they are dropped,
            // deliberately, and the value they carried is the one this comment owes you.
            var outArg = args[^1];

            // A discard receives nothing (OutArgument).
            if (OutArgument.IsDiscard(outArg, context)) return JsExpr.Callish($"(!isNaN({Parsed(input)}))");

            var varName = OutArgument.Target(outArg, context);

            // Note: In strict JS logic, assignment relies on variable being available.
            // If it's `out var x`, `x` is hoisted in C# scope. In JS `var` is hoisted too, but let isn't.
            // We'll trust LocalDeclarationStrategy or standard var usage handled elsewhere if verified.
            // For now, simpler: assume variable exists or is created.

            if (OutArgument.IsBareName(varName)) return JsExpr.Callish($"({varName} = {Parsed(input)}, !isNaN({varName}))");
            // A target with an effect of its own (`out values[index++]`) is named once: the parsed
            // value is bound, written, and checked through the binding.
            return JsExpr.Template($"(({varName} = {{0}}), !isNaN({{0}}))", [JsExpr.Callish(Parsed(input))],
                context.TypeAnnotations);
        }

        return JsExpr.Opaque(context.Unhandled(node, "numeric Parse/TryParse"));
    }

    /// <summary>
    /// <c>decimal.Parse</c> and <c>decimal.TryParse</c> (#358). The runtime reads the text with
    /// .NET's grammar, under the NumberStyles the call names, and rounds as .NET's parser rounds,
    /// so the value is a Decimal: <c>parseFloat</c> made a double, which lost <c>0.1</c> on the way
    /// in and had none of the methods decimal arithmetic calls next. The style reaches the runtime
    /// as the number a NumberStyles holds; the provider does not, since the twin reads the
    /// invariant culture (EQ2110). The bound method says which argument fills which parameter, so a
    /// named one lands in its own slot, and the template keeps them in the order C# evaluates them.
    /// A TryParse that fails leaves 0 in its <c>out</c>, as .NET does.
    /// </summary>
    private static JsExpr DecimalText(InvocationExpressionSyntax invocation, string name, IMethodSymbol? method,
        ConversionContext context)
    {
        var arguments = invocation.ArgumentList.Arguments;
        int? text = null, style = null, result = null, provider = null;
        if (method is not null)
        {
            // The text is a string or a span of chars; a span of UTF-8 bytes has no twin to read.
            if (method.Parameters is not [{ Type: var first }, ..]
                || !(first.SpecialType == SpecialType.System_String || IsSpanOfChar(first)))
            {
                return JsExpr.Opaque(context.Unhandled(invocation, "decimal Parse/TryParse over UTF-8 bytes"));
            }
            for (var i = 0; i < arguments.Count; i++)
            {
                var named = arguments[i].NameColon?.Name.Identifier.ValueText;
                var parameter = named is null
                    ? (i < method.Parameters.Length ? method.Parameters[i] : null)
                    : method.Parameters.FirstOrDefault(p => p.Name == named);
                if (parameter is null) return JsExpr.Opaque(context.Unhandled(invocation, "decimal Parse/TryParse"));
                if (parameter.RefKind == RefKind.Out) result = i;
                else if (parameter.Ordinal == 0) text = i;
                else if (parameter.Type is { Name: "NumberStyles", ContainingNamespace: var home }
                         && home.ToDisplayString() == "System.Globalization") style = i;
                // The provider: the browser reads the invariant culture, so it is left out, and the
                // culture it names decides whether that is faithful (see ParseCulture).
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
            return JsExpr.Opaque(context.Unhandled(invocation, "decimal Parse/TryParse"));
        }
        if (text is not { } at || (name == "TryParse") != result.HasValue)
            return JsExpr.Opaque(context.Unhandled(invocation, "decimal Parse/TryParse"));
        ParseCulture.Check(invocation, provider is { } culture ? arguments[culture].Expression : null, context);

        context.UsedHelpers.Add(Eq.Import);
        // The parts in the order they were written; each hole names its part by that order.
        var read = new[] { text, style }.OfType<int>().Order().ToList();
        var parts = read.Select(i => context.Converter.ConvertIr(arguments[i].Expression)).ToArray();
        string Hole(int argument) => "{" + read.IndexOf(argument) + "}";
        var callArguments = style is { } styles ? $"{Hole(at)}, {Hole(styles)}" : Hole(at);

        if (name == "Parse")
            return JsExpr.Template($"{Eq.DecParse}({callArguments})", parts, context.TypeAnnotations);

        var parsed = $"{Eq.DecTryParse}({callArguments})";
        var outArgument = arguments[result!.Value];
        if (OutArgument.IsDiscard(outArgument, context))
            return JsExpr.Template($"({parsed} !== undefined)", parts, context.TypeAnnotations);
        var target = OutArgument.Target(outArgument, context);
        if (OutArgument.IsBareName(target))
            return JsExpr.Template(
                $"(({target} = {parsed}) !== undefined || (({target} = {Eq.Dec}(0)), false))",
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
        string Numbered(string text) => ordered.Select((entry, position) => (entry.Key, position))
            .Aggregate(text, (written, hole) => written.Replace("{" + hole.Key + "}", "{" + hole.position + "}"));
        var call = style is { } read2 ? $"{Eq.DecTryParse}({{a{at}}}, {{a{read2}}})" : $"{Eq.DecTryParse}({{a{at}}})";
        var value = context.TypeAnnotations ? "($r: any)" : "($r)";
        return JsExpr.Template(
            Numbered($"({value} => ($r !== undefined ? (({place} = $r), true) : (({place} = {Eq.Dec}(0)), false)))({call})"),
            [.. ordered.Select(entry => entry.Part)], context.TypeAnnotations);
    }

    private static bool IsSpanOfChar(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "ReadOnlySpan", TypeArguments: [{ SpecialType: SpecialType.System_Char }] };

    public int Priority => 10;
}
