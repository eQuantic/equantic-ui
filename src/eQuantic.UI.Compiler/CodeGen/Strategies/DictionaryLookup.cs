using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// The LOOKUPS a dictionary answers beyond yes or no — <c>TryGetValue</c>, <c>GetValueOrDefault</c> and
/// <c>Remove(key, out value)</c> — over the two questions the runtime's dictionary classes answer:
/// whether the key is there (<c>has</c>) and what it holds (<c>get</c>).
/// <para>
/// Written once, for what every copy of them had got wrong:
/// </para>
/// <list type="bullet">
/// <item>The receiver and the key are named twice — in the question and in the read — so a call
/// among them ran twice: <c>d.TryGetValue(Next(), out var v)</c> called <c>Next()</c> once per
/// mention. The templates here just name the parts, numbered in the order C# evaluates them, and
/// the writer binds what must be bound (<see cref="JsExprWriter"/>).</item>
/// <item>A miss answers <c>default(TValue)</c>. TryGetValue's out held undefined, so the counting
/// idiom <c>d.TryGetValue(k, out var n); d[k] = n + 1;</c> stored NaN, and a reused variable kept
/// its old value; GetValueOrDefault answered null for an int.</item>
/// <item>An explicit default is evaluated once whether or not it is needed, as C# evaluates every
/// argument before the call — <c>??</c> skipped it on a hit.</item>
/// <item>The out argument is written as <see cref="OutArgument"/> writes it: a discard receives
/// nothing, and a field is written through its object.</item>
/// <item>An extension called in its static form — <c>CollectionExtensions.GetValueOrDefault(d, key)</c>
/// — reads the dictionary its first argument passes, where the type's name was taken for it.</item>
/// </list>
/// </summary>
internal static class DictionaryLookup
{
    // Both over {r}, the dictionary, and {k}, the key.
    private const string Has = "{r}.has({k})";
    private const string Get = "{r}.get({k})";

    /// <summary>
    /// The dictionary a lookup reads: the call's receiver, or — for an extension called in its
    /// static form, <c>CollectionExtensions.GetValueOrDefault(d, key)</c> — the argument its first
    /// parameter takes. Whether a call is a dictionary's lookup is decided by THIS expression's type.
    /// </summary>
    public static ExpressionSyntax? DictionaryOf(InvocationExpressionSyntax invocation, IMethodSymbol? method) =>
        IsStaticForm(method)
            ? Filling(invocation.ArgumentList.Arguments, method, 0)?.Expression
            : (invocation.Expression as MemberAccessExpressionSyntax)?.Expression;

    /// <summary><c>dictionary.TryGetValue(key, out value)</c>.</summary>
    public static JsExpr TryGetValue(InvocationExpressionSyntax invocation, ConversionContext context) =>
        IntoOut(invocation, "Dictionary.TryGetValue", Has, "true", context);

    /// <summary><c>dictionary.Remove(key, out value)</c>: the value moves into the out and the key's
    /// slot is freed, and a miss writes the default and answers false.</summary>
    public static JsExpr Remove(InvocationExpressionSyntax invocation, ConversionContext context) =>
        IntoOut(invocation, "Dictionary.Remove", "{r}.delete({k})", "{r}.delete({k})", context);

    /// <summary>A lookup that writes what it found into an out: <paramref name="discarded"/> is the
    /// whole call when the out is a discard, and <paramref name="hit"/> what a hit answers once the
    /// value is written.</summary>
    private static JsExpr IntoOut(InvocationExpressionSyntax invocation, string strategy, string discarded, string hit,
        ConversionContext context)
    {
        var method = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        var arguments = invocation.ArgumentList.Arguments;
        var key = IsStaticForm(method) ? 1 : 0;
        if (Dictionary(invocation, method, context) is not { } parts
            || Filling(arguments, method, key) is not { } keyArgument
            || Filling(arguments, method, key + 1) is not { } valueArgument)
        {
            return context.Unhandled(invocation, strategy);
        }

        parts.Add(arguments.IndexOf(keyArgument), "k", context.Converter.ConvertIr(keyArgument.Expression));
        // A discard receives nothing, so nothing is read for it: the question is the answer.
        if (OutArgument.IsDiscard(valueArgument, context))
            return parts.Template(discarded, context);

        // The parts the out reads (an element's array and index) are evaluated where it was written.
        var at = arguments.IndexOf(valueArgument);
        var place = OutArgument.Place(valueArgument, context, part => parts.Add(at, $"t{parts.Count}", part));
        var fallback = DefaultValue.Of(method?.Parameters.ElementAtOrDefault(key + 1)?.Type, context);
        return parts.Template(
            $"({Has} ? (({place} = {Get}), {hit}) : (({place} = {fallback}), false))", context);
    }

    /// <summary><c>dictionary.GetValueOrDefault(key)</c> and <c>dictionary.GetValueOrDefault(key, defaultValue)</c>.</summary>
    public static JsExpr GetValueOrDefault(InvocationExpressionSyntax invocation, ConversionContext context)
    {
        var method = context.SemanticHelper.GetSymbol(invocation) as IMethodSymbol;
        var arguments = invocation.ArgumentList.Arguments;
        var key = IsStaticForm(method) ? 1 : 0;
        if (Dictionary(invocation, method, context) is not { } parts
            || Filling(arguments, method, key) is not { } keyArgument)
        {
            return context.Unhandled(invocation, "Dictionary.GetValueOrDefault");
        }

        parts.Add(arguments.IndexOf(keyArgument), "k", context.Converter.ConvertIr(keyArgument.Expression));
        if (Filling(arguments, method, key + 1) is not { } defaultArgument)
        {
            return parts.Template(
                $"({Has} ? {Get} : {DefaultValue.Of(method?.ReturnType, context)})", context);
        }

        var value = context.Converter.ConvertIr(defaultArgument.Expression);
        parts.Add(arguments.IndexOf(defaultArgument), "d", value);
        // A default nobody can observe being read may be read only where it is needed. Any other is
        // evaluated whether or not it is needed, once, in its place among the arguments: every part
        // is then an argument of one arrow, where a hole in the miss branch would run only there.
        return JsExprWriter.IsInlinable(value)
            ? parts.Template($"({Has} ? {Get} : {{d}})", context)
            : parts.Arrow($"({Has} ? {Get} : {{d}})", context);
    }

    /// <summary>The parts, started with the dictionary: first when it is the call's receiver, in
    /// its place among the arguments when the call is an extension's static form.</summary>
    private static Parts? Dictionary(InvocationExpressionSyntax invocation, IMethodSymbol? method, ConversionContext context)
    {
        if (DictionaryOf(invocation, method) is not { } dictionary) return null;
        var parts = new Parts();
        var position = IsStaticForm(method)
            ? invocation.ArgumentList.Arguments.IndexOf(argument => argument.Expression == dictionary)
            : -1;
        parts.Add(position, "r", context.Converter.ConvertIr(dictionary));
        return parts;
    }

    /// <summary>An extension method called through its type, the dictionary passed as an argument.</summary>
    private static bool IsStaticForm(IMethodSymbol? method) =>
        method is { IsExtensionMethod: true, MethodKind: MethodKind.Ordinary };

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

    /// <summary>
    /// The parts of one lookup, in the order C# evaluates them — the receiver of an instance call
    /// first, then every argument where it was written, named ones included — each written into a
    /// template as <c>{name}</c>. The holes are numbered by that order, which is the order the
    /// writer binds in, so the templates only say what they compute.
    /// </summary>
    private sealed class Parts
    {
        private readonly List<(int Position, int Added, string Hole, JsExpr Part)> _parts = [];

        public int Count => _parts.Count;

        /// <summary>Adds <paramref name="part"/>, evaluated at <paramref name="position"/> (−1 for an
        /// instance call's receiver, else the index of its argument), and answers its hole.</summary>
        public string Add(int position, string name, JsExpr part)
        {
            var hole = "{" + name + "}";
            _parts.Add((position, _parts.Count, hole, part));
            return hole;
        }

        public JsExpr Template(string text, ConversionContext context)
        {
            var ordered = Ordered();
            for (var index = 0; index < ordered.Count; index++)
                text = text.Replace(ordered[index].Hole, "{" + index + "}");
            return JsExpr.Template(text, [.. ordered.Select(part => part.Part)], context.TypeAnnotations);
        }

        /// <summary>Every part evaluated ONCE, as an argument of one arrow, in the order C# evaluates
        /// them, and <paramref name="body"/> reading each through its parameter — for a part that is
        /// evaluated whether or not the body reads it.</summary>
        public JsExpr Arrow(string body, ConversionContext context)
        {
            var ordered = Ordered();
            string Parameter(string hole) => "$" + hole[1..^1];
            foreach (var part in ordered) body = body.Replace(part.Hole, Parameter(part.Hole));
            var parameters = string.Join(", ", ordered.Select(part =>
                Parameter(part.Hole) + (context.TypeAnnotations ? ": any" : "")));
            var holes = string.Join(", ", ordered.Select((_, index) => "{" + index + "}"));
            return JsExpr.Template($"(({parameters}) => {body})({holes})",
                [.. ordered.Select(part => part.Part)], context.TypeAnnotations);
        }

        private List<(int Position, int Added, string Hole, JsExpr Part)> Ordered() =>
            [.. _parts.OrderBy(part => part.Position).ThenBy(part => part.Added)];
    }
}
