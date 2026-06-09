using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
}
