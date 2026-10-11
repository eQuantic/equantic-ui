using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies.Linq;

/// <summary>
/// The arguments of a LINQ call handed to a runtime twin (<c>$eq.linq.*</c>) by the parameter each one
/// binds to. The twin takes its parameters in .NET's order, a missing one as <c>undefined</c>, and the
/// call's arguments run in the order they are WRITTEN, whatever parameter each fills: a named argument
/// written out of order fills its own place and still runs where it stands, as C# evaluates it. The
/// receiver, the extension's <c>this</c>, is not among them; the operator reads it through
/// <see cref="LinqSource"/>.
/// </summary>
internal static class LinqArguments
{
    /// <summary>
    /// The parameter each written argument binds to, by position among the written arguments, from the
    /// bound tree, as <see cref="GroupByStrategy"/> reads its roles: a call a null-conditional rebuilt,
    /// whose arguments are copies, is matched through its original. Null where the tree binds nothing.
    /// </summary>
    internal static IReadOnlyList<string>? Parameters(InvocationExpressionSyntax invocation, ConversionContext context)
    {
        if (context.SemanticHelper.GetOperation(invocation) is not IInvocationOperation call) return null;
        var args = invocation.ArgumentList.Arguments;
        var written = (context.SemanticHelper.Original(invocation) as InvocationExpressionSyntax)?.ArgumentList.Arguments ?? args;
        var names = new string?[args.Count];
        foreach (var argument in call.Arguments)
        {
            if (argument is { Syntax: ArgumentSyntax syntax, Parameter.Name: var name }
                && written.IndexOf(syntax) is var at && at >= 0 && at < names.Length)
                names[at] = name;
        }
        return names.All(name => name is not null) ? Array.ConvertAll(names, name => name!) : null;
    }

    /// <summary>
    /// <c>helper(source, p1, p2, …)</c> over <paramref name="slots"/>, the twin's parameters after the
    /// source in their order: each filled by the argument written for it (<paramref name="convert"/>
    /// writes it), and <c>undefined</c> where the call wrote none, then <paramref name="settled"/>, the
    /// literals the compiler settled from the bound overload (how the values add, the equality they
    /// compare by). An argument for <paramref name="dropped"/> is not written: a comparer the fence let
    /// through asks for what the twin already does, and reading one has no effect to keep. Null where an
    /// argument binds to no slot, or the tree binds nothing.
    /// </summary>
    internal static JsExpr? Call(string helper, JsExpr source, IReadOnlyList<string> slots,
        InvocationExpressionSyntax invocation, Func<ArgumentSyntax, JsExpr> convert, ConversionContext context,
        IReadOnlyList<string>? settled = null, string? dropped = null)
    {
        if (Parameters(invocation, context) is not { } names
            || names.Any(name => name != dropped && !slots.Contains(name))) return null;
        var args = invocation.ArgumentList.Arguments;
        var parts = new List<JsExpr> { source };
        var holes = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var at = 0; at < args.Count; at++)
        {
            if (names[at] == dropped) continue;
            holes[names[at]] = parts.Count;
            parts.Add(convert(args[at]));
        }

        context.UsedHelpers.Add(Eq.Import);
        var filled = slots.Select(slot => holes.TryGetValue(slot, out var hole) ? $"{{{hole}}}" : "undefined")
            .Concat(settled ?? []);
        return JsExpr.Template($"{helper}({{0}}, {string.Join(", ", filled)})", parts);
    }
}
