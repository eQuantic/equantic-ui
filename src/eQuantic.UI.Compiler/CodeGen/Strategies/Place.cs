using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using eQuantic.UI.Compiler.CodeGen.Extensions;
using eQuantic.UI.Compiler.CodeGen.Ir;
using eQuantic.UI.Compiler.CodeGen.Strategies.Expressions;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A PLACE JavaScript cannot assign, because it is read and written through calls: a dictionary's
/// entry, read by <c>$eq.mapGet</c> and written by <c>$eq.mapSet</c> (<see cref="DictionaryEntry"/>),
/// and an entry of an indexer a twin carries, read by its <c>item</c> and written by its
/// <c>setItem</c> (<see cref="Indexer"/>). It is resolved here, once, and every writer takes it from
/// here: the assignment, the compound, the step, the coalescing assignment, a bool's compound, the
/// null-conditional assignment, the object initializer's entry and the deconstruction. Each of them
/// spelled the two kinds beside each other, so a rule of either had a writer for each to be taught in.
/// <para>
/// Its parts are what C# evaluates, in the order it evaluates them: the receiver, then the keys. A
/// writer builds a template over them (<see cref="JsTemplate"/>), whose writer binds a part once
/// where it is used twice.
/// </para>
/// </summary>
internal sealed class Place
{
    private readonly bool _entry;
    private readonly IReadOnlyList<JsExpr> _parts;
    private readonly int _evaluated;
    private readonly IReadOnlyList<string> _keys;
    private readonly ConversionContext _context;

    /// <param name="entry">A dictionary's entry, or else an indexer's.</param>
    /// <param name="parts">The receiver, the keys C# evaluates, then the constants the call passes.</param>
    /// <param name="evaluated">How many of the parts C# evaluates; the rest are constants.</param>
    /// <param name="keys">The keys the call passes, in its parameters' order, as template text over the
    /// parts' holes.</param>
    /// <param name="context">The conversion the writers build in.</param>
    private Place(bool entry, IReadOnlyList<JsExpr> parts, int evaluated, IReadOnlyList<string> keys,
        ConversionContext context)
    {
        _entry = entry;
        _parts = parts;
        _evaluated = evaluated;
        _keys = keys;
        _context = context;
    }

    /// <summary>The place <paramref name="target"/> names, through its parentheses: a dictionary's
    /// entry or an entry of an indexer a twin carries; null for any other target, whose writers keep
    /// JavaScript's own assignment.</summary>
    public static Place? Of(ExpressionSyntax target, ConversionContext context)
    {
        var node = target;
        while (node is ParenthesizedExpressionSyntax parenthesized) node = parenthesized.Expression;
        if (node is not ElementAccessExpressionSyntax access) return null;
        if (DictionaryEntry.Of(access, context) is not null)
            return Entry(context.Converter.ConvertIr(access.Expression), access.ArgumentList.Arguments[0].Expression, context);
        return Indexer.LoweredAt(access, context) is null
            ? null
            : Indexed(access.ArgumentList, context.Converter.ConvertIr(access.Expression), context);
    }

    /// <summary>The place an element named without its receiver is, the writer holding the
    /// <paramref name="receiver"/>: a null-conditional's binding (<c>a?[k] = v</c>, over its guarded
    /// value) and an object initializer's entry (<c>[k] = v</c>, over the object it applies to).</summary>
    public static Place? Of(ExpressionSyntax target, JsExpr receiver, ConversionContext context)
    {
        if (ArgumentsOf(target) is not { } arguments || target is ElementAccessExpressionSyntax) return null;
        if (context.SemanticHelper.GetSymbol(target) is IPropertySymbol { IsIndexer: true } indexer
            && indexer.ContainingType.IsDictionary() && arguments.Arguments.Count == 1)
            return Entry(receiver, arguments.Arguments[0].Expression, context);
        return Indexer.LoweredAt(target, context) is null ? null : Indexed(arguments, receiver, context);
    }

    /// <summary>The parts C# evaluates, in its order: the receiver, then the keys. A lowering that
    /// evaluates them itself (a deconstruction's captures, an initializer's arguments) writes the place
    /// over what it bound them to (<see cref="Over"/>).</summary>
    public IReadOnlyList<JsExpr> Evaluated => _parts.Take(_evaluated).ToList();

    /// <summary>The same place over other parts for the ones C# evaluates, the constants kept.</summary>
    public Place Over(IReadOnlyList<JsExpr> evaluated) =>
        new(_entry, [.. evaluated, .. _parts.Skip(_evaluated)], _evaluated, _keys, _context);

    /// <summary>The read.</summary>
    public JsExpr Read() => Template(ReadText(_keys), _parts);

    /// <summary>The write, which answers <paramref name="value"/>, as C#'s assignment does.</summary>
    public JsExpr Write(JsExpr value) => Template(WriteText(_keys, Hole(_parts.Count)), [.. _parts, value]);

    /// <summary>
    /// A read-modify-write (a compound, a step, a bool's compound): the current value read, the next
    /// one written, the receiver and each key evaluated once (<see cref="ReadModifyWrite"/>).
    /// <paramref name="answerOld"/> answers the value before the write.
    /// </summary>
    public JsExpr Modify(IReadOnlyList<JsExpr> operands, Func<JsExpr, IReadOnlyList<JsExpr>, JsExpr> next, bool answerOld)
    {
        var (text, parts) = ReadModifyWrite.Spelled([.. _parts], value => WriteText(_keys, value), ReadText(_keys),
            operands, next, answerOld, _context);
        return Template(text, parts);
    }

    /// <summary>
    /// <c>??=</c>: the write, of <paramref name="value"/>, only where the read is null, as C# calls the
    /// setter only then. The value is evaluated only when it is written.
    /// </summary>
    public JsExpr Coalesce(JsExpr value) =>
        Template($"({ReadText(_keys)} ?? {WriteText(_keys, Hole(_parts.Count))})", [.. _parts, value]);

    private static string Hole(int index) => "{" + index + "}";

    private string ReadText(IReadOnlyList<string> keys) =>
        _entry ? DictionaryEntry.Read("{0}", keys[0]) : $"{{0}}.{Indexer.Get}({string.Join(", ", keys)})";

    private string WriteText(IReadOnlyList<string> keys, string value) =>
        _entry ? DictionaryEntry.Write("{0}", keys[0], value) : $"{{0}}.{Indexer.Set}({string.Join(", ", keys.Append(value))})";

    private JsExpr Template(string text, IReadOnlyList<JsExpr> parts) =>
        JsExpr.Template(text, parts, _context.TypeAnnotations);

    private static Place Entry(JsExpr receiver, ExpressionSyntax key, ConversionContext context)
    {
        context.UsedHelpers.Add(Eq.Import);
        return new Place(entry: true, [receiver, context.Converter.ConvertIr(key)], evaluated: 2, ["{1}"], context);
    }

    /// <summary>The argument list of an element named by its keys, written with its receiver or without.</summary>
    private static BracketedArgumentListSyntax? ArgumentsOf(SyntaxNode node) => node switch
    {
        ElementAccessExpressionSyntax access => access.ArgumentList,
        ElementBindingExpressionSyntax binding => binding.ArgumentList,
        ImplicitElementAccessSyntax implicitAccess => implicitAccess.ArgumentList,
        _ => null,
    };

    /// <summary>An entry of an indexer a twin carries: the receiver, then each key.</summary>
    private static Place Indexed(BracketedArgumentListSyntax arguments, JsExpr receiver, ConversionContext context)
    {
        var parts = new List<JsExpr> { receiver };
        var keys = new List<string>();
        foreach (var argument in arguments.Arguments)
        {
            keys.Add(Hole(parts.Count));
            parts.Add(context.Converter.ConvertIr(argument.Expression));
        }
        return new Place(entry: false, parts, parts.Count, keys, context);
    }
}
