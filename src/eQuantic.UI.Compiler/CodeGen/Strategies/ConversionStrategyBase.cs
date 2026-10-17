using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// Base class for conversion strategies, sharing behavior common to many of them instead of repeating
/// it as free-standing static helpers. Concrete strategies override <see cref="CanConvert"/> and
/// <see cref="Convert"/> (and optionally <see cref="Priority"/>) and inherit the protected helpers.
/// Strategies with no shared needs may still implement <see cref="IConversionStrategy"/> directly.
/// </summary>
public abstract class ConversionStrategyBase : IConversionStrategy
{
    public abstract bool CanConvert(SyntaxNode node, ConversionContext context);

    public abstract string Convert(SyntaxNode node, ConversionContext context);

    public virtual int Priority => 0;

    /// <summary>
    /// Converts an argument list to a comma-joined JS argument string (each argument transpiled via the
    /// shared converter). Returns the empty string for no/absent arguments.
    /// </summary>
    protected static string ConvertArgs(ArgumentListSyntax? argumentList, ConversionContext context) =>
        argumentList == null || argumentList.Arguments.Count == 0
            ? string.Empty
            : string.Join(", ", argumentList.Arguments.Select(a => context.Converter.ConvertExpression(a.Expression)));

    /// <summary>
    /// A call or a construction of <paramref name="method"/> with each argument in its PARAMETER's place,
    /// for a twin that takes them by position: <paramref name="callee"/> and its arguments, after the
    /// receiver where there is one (<paramref name="callee"/> is then the member, <c>.compare</c>). An
    /// optional parameter the call leaves out is <c>undefined</c>, up to the last one it fills. Passed as
    /// written, a named argument landed in another parameter's place: <c>TimeSpan.FromHours(1, seconds: 5)</c>
    /// put the 5 in the minutes, and <c>DateTime.Compare(t2: later, t1: earlier)</c> compared the two the
    /// other way round (Copilot's review of #958). The receiver and the arguments stay in the order they
    /// were written, which is the order C# evaluates them, and the template's writer keeps it where the
    /// places do not follow it. Written as they come where an argument names no parameter of the method.
    /// </summary>
    private protected static string ByParameter(string callee, ArgumentListSyntax? argumentList, IMethodSymbol? method,
        ConversionContext context, ExpressionSyntax? receiver = null)
    {
        var arguments = argumentList?.Arguments ?? default;
        var offset = receiver is null ? 0 : 1;
        var places = new string?[method?.Parameters.Length ?? 0];
        for (var i = 0; i < arguments.Count; i++)
        {
            var named = arguments[i].NameColon?.Name.Identifier.ValueText;
            var ordinal = method is null ? -1
                : named is null ? i
                : method.Parameters.FirstOrDefault(parameter => parameter.Name == named)?.Ordinal ?? -1;
            if (ordinal < 0 || ordinal >= places.Length || method!.Parameters[ordinal].IsParams)
                return receiver is null
                    ? $"{callee}({ConvertArgs(argumentList, context)})"
                    : $"{JsExprWriter.WriteIn(context.Converter.ConvertIr(receiver), JsPrecedence.Call)}{callee}({ConvertArgs(argumentList, context)})";
            places[ordinal] = "{" + (i + offset) + "}";
        }
        var filled = places.Take(System.Array.FindLastIndex(places, place => place is not null) + 1)
            .Select(place => place ?? "undefined");
        var parts = new List<JsExpr>();
        if (receiver is not null) parts.Add(context.Converter.ConvertIr(receiver));
        parts.AddRange(arguments.Select(argument => context.Converter.ConvertIr(argument.Expression)));
        return JsExprWriter.Write(JsExpr.Template($"{(receiver is null ? "" : "{0}")}{callee}({string.Join(", ", filled)})", parts));
    }
}
