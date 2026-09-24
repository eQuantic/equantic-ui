using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// The two LOOKUPS a dictionary answers beyond yes or no — <c>TryGetValue</c> and
/// <c>GetValueOrDefault</c> — written once, over the two questions each representation asks in
/// its own way: whether the key is there, and what it holds. A plain object asks for its OWN key,
/// for the reason ContainsKey gives (a prototype member such as <c>"toString"</c> is a miss), and
/// indexes; a runtime map (<c>$eq.collections.valueMap</c>, <c>sortedDictionary</c>…) asks
/// <c>has</c> and <c>get</c>.
/// <para>
/// Each lowering kept its own copy of both before this, and the copies were wrong the same way:
/// </para>
/// <list type="bullet">
/// <item>The receiver and the key are named twice — in the question and in the read — so a call
/// among them ran twice: <c>d.TryGetValue(Next(), out var v)</c> called <c>Next()</c> once per
/// mention. The templates here just name them, and the writer binds a part it uses twice
/// (<see cref="JsExprWriter"/>), in the order C# evaluates the arguments — as written, named ones
/// included.</item>
/// <item>A miss answers <c>default(TValue)</c>. TryGetValue's out held undefined, so the counting
/// idiom <c>d.TryGetValue(k, out var n); d[k] = n + 1;</c> stored NaN, and a reused variable kept
/// its old value; GetValueOrDefault answered null for an int.</item>
/// <item>An explicit default is evaluated once whether or not it is needed, as C# evaluates every
/// argument before the call — <c>??</c> skipped it on a hit.</item>
/// <item>The out argument is written as <see cref="OutArgument"/> writes it: a discard receives
/// nothing, and a field is written through its object.</item>
/// </list>
/// </summary>
internal sealed class DictionaryLookup
{
    /// <summary>A dictionary lowered to a plain object.</summary>
    public static readonly DictionaryLookup PlainObject =
        new("Object.prototype.hasOwnProperty.call({r}, {k})", "{r}[{k}]");

    /// <summary>A dictionary lowered to a runtime map.</summary>
    public static readonly DictionaryLookup RuntimeMap = new("{r}.has({k})", "{r}.get({k})");

    // Both over {r}, the receiver, and {k}, the key.
    private readonly string _has;
    private readonly string _read;

    private DictionaryLookup(string has, string read)
    {
        _has = has;
        _read = read;
    }

    /// <summary><c>receiver.TryGetValue(key, out value)</c>.</summary>
    public JsExpr TryGetValue(ExpressionSyntax receiver, InvocationExpressionSyntax invocation, ConversionContext context)
    {
        var method = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        var arguments = invocation.ArgumentList.Arguments;
        if (Filling(arguments, method, 0) is not { } keyArgument || Filling(arguments, method, 1) is not { } valueArgument)
            return context.Unhandled(invocation, "Dictionary.TryGetValue");

        var parts = new List<JsExpr> { context.Converter.ConvertIr(receiver) };
        string Hole(JsExpr part)
        {
            parts.Add(part);
            return $"{{{parts.Count - 1}}}";
        }

        var key = context.Converter.ConvertIr(keyArgument.Expression);
        // A discard receives nothing, so nothing is read for it: the question is the answer.
        if (OutArgument.IsDiscard(valueArgument, context))
            return Template(Has("{0}", Hole(key)), parts, context);

        // The key and the parts the out reads (an element's array and index), in the order written.
        var keyFirst = arguments.IndexOf(keyArgument) < arguments.IndexOf(valueArgument);
        var k = keyFirst ? Hole(key) : "";
        var place = OutArgument.Place(valueArgument, context, Hole);
        if (!keyFirst) k = Hole(key);

        var fallback = DefaultValue.Of(method is { Parameters.Length: > 1 } ? method.Parameters[1].Type : null, context);
        return Template(
            $"({Has("{0}", k)} ? (({place} = {Read("{0}", k)}), true) : (({place} = {fallback}), false))",
            parts, context);
    }

    /// <summary><c>receiver.GetValueOrDefault(key)</c> and <c>receiver.GetValueOrDefault(key, defaultValue)</c>.</summary>
    public JsExpr GetValueOrDefault(ExpressionSyntax receiver, InvocationExpressionSyntax invocation, ConversionContext context)
    {
        var method = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        var arguments = invocation.ArgumentList.Arguments;
        if (Filling(arguments, method, 0) is not { } keyArgument)
            return context.Unhandled(invocation, "Dictionary.GetValueOrDefault");
        var defaultArgument = arguments.Count > 1 ? Filling(arguments, method, 1) : null;

        var parts = new List<JsExpr> { context.Converter.ConvertIr(receiver) };
        string Hole(JsExpr part)
        {
            parts.Add(part);
            return $"{{{parts.Count - 1}}}";
        }

        var key = context.Converter.ConvertIr(keyArgument.Expression);
        if (defaultArgument is null)
        {
            var k = Hole(key);
            return Template(
                $"({Has("{0}", k)} ? {Read("{0}", k)} : {DefaultValue.Of(method?.ReturnType, context)})",
                parts, context);
        }

        var value = context.Converter.ConvertIr(defaultArgument.Expression);
        if (JsExprWriter.IsInlinable(value))
        {
            // A default nobody can observe being read may be read only where it is needed.
            var k = Hole(key);
            return Template($"({Has("{0}", k)} ? {Read("{0}", k)} : {Hole(value)})", parts, context);
        }

        // Any other default is evaluated whether or not it is needed, once, where it was written
        // relative to the key: an arrow takes both, in that order, and the writer still binds the
        // receiver it names twice — which C# evaluated first.
        var keyFirst = arguments.IndexOf(keyArgument) < arguments.IndexOf(defaultArgument);
        var first = Hole(keyFirst ? key : value);
        var second = Hole(keyFirst ? value : key);
        var parameters = string.Join(", ", (keyFirst ? new[] { "$k", "$d" } : new[] { "$d", "$k" })
            .Select(name => context.TypeAnnotations ? $"{name}: any" : name));
        return Template(
            $"(({parameters}) => ({Has("{0}", "$k")} ? {Read("{0}", "$k")} : $d))({first}, {second})",
            parts, context);
    }

    private string Has(string receiver, string key) => _has.Replace("{r}", receiver).Replace("{k}", key);

    private string Read(string receiver, string key) => _read.Replace("{r}", receiver).Replace("{k}", key);

    private static JsExpr Template(string text, List<JsExpr> parts, ConversionContext context) =>
        JsExpr.Template(text, parts, context.TypeAnnotations);

    /// <summary>The argument that fills parameter <paramref name="ordinal"/>: the one NAMED for it —
    /// C# lets a call name its arguments in any order, <c>TryGetValue(value: out var v, key: k)</c> —
    /// or the one in its position.</summary>
    private static ArgumentSyntax? Filling(SeparatedSyntaxList<ArgumentSyntax> arguments, IMethodSymbol? method, int ordinal)
    {
        if (method is not null && ordinal < method.Parameters.Length
            && arguments.FirstOrDefault(argument =>
                argument.NameColon?.Name.Identifier.ValueText == method.Parameters[ordinal].Name) is { } named)
        {
            return named;
        }
        return ordinal < arguments.Count && (method is null || arguments[ordinal].NameColon is null)
            ? arguments[ordinal]
            : null;
    }
}
