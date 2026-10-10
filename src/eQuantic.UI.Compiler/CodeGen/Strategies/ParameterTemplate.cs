using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A lowering written over a call's PARAMETERS — <c>{0}</c> its receiver where it has one, <c>{1}</c> its
/// first parameter, <c>{2}</c> the second — as IR over the arguments in the order they were WRITTEN,
/// which is the order C# evaluates them. Each parameter's hole is pointed at the argument that fills
/// it, a named one included: <c>list.CopyTo(arrayIndex: 1, array: a)</c> passes <c>a</c> first and
/// evaluates the index first, and the template writer keeps both.
/// </summary>
internal static class ParameterTemplate
{
    /// <summary>
    /// The template over <paramref name="invocation"/>'s arguments. A template names only the
    /// parameters the bound overload has and the call passes; <paramref name="convert"/> converts an
    /// argument where it needs more than its own conversion.
    /// </summary>
    internal static JsExpr Call(string template, JsExpr? receiver, InvocationExpressionSyntax invocation,
        IMethodSymbol? method, ConversionContext context, Func<ArgumentSyntax, JsExpr>? convert = null,
        (string Name, string Text)? insert = null) =>
        Call(template, receiver, invocation.ArgumentList.Arguments, method, context, convert, insert);

    /// <summary>
    /// The template over a constructor's arguments, as a call's: <c>{0}</c> is the constructor's first
    /// parameter, since a creation has no receiver.
    /// </summary>
    internal static JsExpr Construction(string template, BaseObjectCreationExpressionSyntax creation,
        IMethodSymbol constructor, ConversionContext context) =>
        Call(template, null, creation.ArgumentList?.Arguments ?? default, constructor, context, null, null);

    private static JsExpr Call(string template, JsExpr? receiver, SeparatedSyntaxList<ArgumentSyntax> arguments,
        IMethodSymbol? method, ConversionContext context, Func<ArgumentSyntax, JsExpr>? convert,
        (string Name, string Text)? insert)
    {
        var offset = receiver is null ? 0 : 1;
        var parts = new List<JsExpr>();
        if (receiver is not null) parts.Add(receiver);
        parts.AddRange(arguments.Select(argument => convert?.Invoke(argument) ?? context.Converter.ConvertIr(argument.Expression)));
        var bound = Bind(template, arguments, method, offset);
        // Text inserted after the parameters are bound: its holes already name written positions.
        if (insert is { } inserted) bound = bound.Replace(inserted.Name, inserted.Text);
        return JsExpr.Template(bound, parts);
    }

    /// <summary>The written position of the argument that fills each parameter, -1 for one not passed.</summary>
    internal static int[] WrittenFor(SeparatedSyntaxList<ArgumentSyntax> arguments, IMethodSymbol method)
    {
        var written = Enumerable.Repeat(-1, method.Parameters.Length).ToArray();
        for (var i = 0; i < arguments.Count; i++)
        {
            var named = arguments[i].NameColon?.Name.Identifier.ValueText;
            var slot = named is null ? i : method.Parameters.FirstOrDefault(parameter => parameter.Name == named)?.Ordinal ?? -1;
            if (slot >= 0 && slot < written.Length) written[slot] = i;
        }
        return written;
    }

    /// <summary>The argument that fills a parameter, named or in its place; null where the call passes none.</summary>
    internal static ArgumentSyntax? Filling(InvocationExpressionSyntax invocation, IMethodSymbol method, int ordinal)
    {
        var at = WrittenFor(invocation.ArgumentList.Arguments, method)[ordinal];
        return at < 0 ? null : invocation.ArgumentList.Arguments[at];
    }

    private static string Bind(string template, SeparatedSyntaxList<ArgumentSyntax> arguments, IMethodSymbol? method, int offset)
    {
        if (method is null || arguments.All(argument => argument.NameColon is null)) return template;
        var written = WrittenFor(arguments, method);
        return System.Text.RegularExpressions.Regex.Replace(template, @"\{(\d+)\}", hole =>
        {
            var index = int.Parse(hole.Groups[1].Value);
            if (index < offset) return hole.Value;
            var parameter = index - offset;
            return parameter < written.Length && written[parameter] >= 0
                ? "{" + (written[parameter] + offset) + "}"
                : hole.Value;
        });
    }
}
