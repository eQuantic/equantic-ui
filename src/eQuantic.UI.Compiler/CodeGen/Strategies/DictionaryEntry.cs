using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// How a dictionary ENTRY — <c>d[k]</c> — is read and written. Every dictionary is a runtime class on
/// this side (the runtime's <c>Dictionary</c>, or the <c>SortedMap</c> of a sorted one): the read goes
/// through <c>$eq.mapGet</c>, which throws for a key that is not there as .NET's indexer does, and the
/// write through <c>$eq.mapSet</c>, which answers the value written as C#'s assignment does where the
/// class's own <c>set</c> answers the dictionary.
/// <para>
/// Everything else about an entry is written once, by whoever computes it: the value a compound takes
/// follows its type's rule (<see cref="ReadModifyWrite"/>), a bool's logical compound stays logical,
/// and <c>??=</c> reads before it coalesces.
/// </para>
/// </summary>
internal static class DictionaryEntry
{
    private static readonly string ReadPattern = $"{Eq.MapGet}({{r}}, {{k}})";
    private static readonly string WritePattern = $"{Eq.MapSet}({{r}}, {{k}}, {{v}})";

    // Over {r}, the dictionary, {k}, the key, and {v}, the value written.
    private static readonly Regex Token = new(@"\{([rkv])\}", RegexOptions.Compiled);

    /// <summary>The entry <paramref name="target"/> names, or null when it is not a dictionary's entry.</summary>
    public static ElementAccessExpressionSyntax? Of(ExpressionSyntax target, ConversionContext context) =>
        target is ElementAccessExpressionSyntax { ArgumentList.Arguments.Count: 1 } access
        && context.SemanticHelper.GetType(access.Expression).IsDictionary()
            ? access
            : null;

    /// <summary>The read, which throws for a key that is not there, as text for a template.</summary>
    public static string Read(string dictionary, string key) => Fill(ReadPattern, dictionary, key, "");

    /// <summary>The write, which answers <paramref name="value"/>, as text for a template.</summary>
    public static string Write(string dictionary, string key, string value) => Fill(WritePattern, dictionary, key, value);

    /// <summary>The read, as a node.</summary>
    public static JsExpr Read(JsExpr dictionary, JsExpr key) => JsExpr.Call(JsExpr.Identifier(Eq.MapGet), dictionary, key);

    /// <summary>The write, as a node.</summary>
    public static JsExpr Write(JsExpr dictionary, JsExpr key, JsExpr value) =>
        JsExpr.Call(JsExpr.Identifier(Eq.MapSet), dictionary, key, value);

    /// <summary>The pattern with its tokens filled in ONE pass, so no text put in is scanned for a
    /// token again: replaced one token at a time, a receiver written <c>GetMap("{k}")</c> had its
    /// string literal rewritten by the key, and a key <c>"{v}"</c> by the value.</summary>
    private static string Fill(string pattern, string dictionary, string key, string value) =>
        Token.Replace(pattern, token => token.Groups[1].Value switch
        {
            "r" => dictionary,
            "k" => key,
            _ => value,
        });
}
