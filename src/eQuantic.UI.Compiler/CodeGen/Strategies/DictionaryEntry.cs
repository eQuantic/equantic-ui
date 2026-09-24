using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// How a dictionary ENTRY — <c>d[k]</c> — is read and written, per the representation its
/// dictionary lowers to. A plain object reads through <c>$eq.dictGet</c> and writes by index; a
/// runtime map (a <c>SortedDictionary</c>, a <c>SortedList</c>, a <c>Dictionary</c> keyed by a value)
/// reads through <c>$eq.mapGet</c> and writes through its <c>set</c>, by <c>$eq.mapSet</c>. Both
/// reads throw for a key that is not there, as .NET's indexer does, and both writes answer the value
/// written, as C#'s assignment does.
/// <para>
/// Everything else about an entry is the same for both, and is written once, by whoever computes it:
/// the value a compound takes follows its type's rule (<see cref="ReadModifyWrite"/>), a bool's
/// logical compound stays logical, and <c>??=</c> reads before it coalesces. The runtime map had a
/// text template of its own that ran ahead of all of them, and got every one wrong.
/// </para>
/// </summary>
internal sealed class DictionaryEntry
{
    /// <summary>A dictionary lowered to a plain object.</summary>
    public static readonly DictionaryEntry PlainObject = new($"{Eq.DictGet}({{r}}, {{k}})", "{r}[{k}] = {v}");

    /// <summary>A dictionary lowered to a runtime map.</summary>
    public static readonly DictionaryEntry RuntimeMap = new($"{Eq.MapGet}({{r}}, {{k}})", $"{Eq.MapSet}({{r}}, {{k}}, {{v}})");

    // Over {r}, the dictionary, {k}, the key, and {v}, the value written.
    private static readonly Regex Token = new(@"\{([rkv])\}", RegexOptions.Compiled);
    private readonly string _read;
    private readonly string _write;

    private DictionaryEntry(string read, string write)
    {
        _read = read;
        _write = write;
    }

    /// <summary>The entry <paramref name="target"/> names, and how its dictionary reads and writes
    /// one; null when the target is not a dictionary's entry.</summary>
    public static (ElementAccessExpressionSyntax Access, DictionaryEntry Entry)? Of(ExpressionSyntax target, ConversionContext context)
    {
        if (target is not ElementAccessExpressionSyntax { ArgumentList.Arguments.Count: 1 } access) return null;
        var type = context.SemanticHelper.GetType(access.Expression);
        // The same two questions the runtime-map strategies ask of the dictionary's type.
        if (type.IsSortedDictionary() || type.IsValueKeyedDictionary()) return (access, RuntimeMap);
        return type.IsDictionaryLike(out _) ? (access, PlainObject) : null;
    }

    /// <summary>The read, which throws for a key that is not there.</summary>
    public string Read(string dictionary, string key) => Fill(_read, dictionary, key, "");

    /// <summary>The write, which answers <paramref name="value"/>. Its text is an assignment on a
    /// plain object and a call on a runtime map, so a template that places it among other operators
    /// wraps it in parentheses of its own.</summary>
    public string Write(string dictionary, string key, string value) => Fill(_write, dictionary, key, value);

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
