using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
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
/// Its parts are what C# evaluates, in the order it evaluates them: the receiver, then the keys as they
/// are written, and after them the constants the call passes without evaluating anything. A writer
/// builds a template over them (<see cref="JsTemplate"/>), whose writer binds a part once where it is
/// used twice and keeps C#'s order where a key is passed out of it.
/// </para>
/// </summary>
internal sealed class Place
{
    private readonly bool _entry;
    private readonly IReadOnlyList<JsExpr> _parts;
    private readonly int _evaluated;
    private readonly IReadOnlyList<string> _keys;
    private readonly bool _computed;
    private readonly ConversionContext _context;

    /// <param name="entry">A dictionary's entry, or else an indexer's.</param>
    /// <param name="parts">The receiver, the keys C# evaluates, then the constants the call passes.</param>
    /// <param name="evaluated">How many of the parts C# evaluates; the rest are constants.</param>
    /// <param name="keys">The keys the call passes, in its parameters' order, as template text over the
    /// parts' holes.</param>
    /// <param name="computed">Whether the keys are computed from the parts (a from-the-end key's
    /// position), once, where C# computes them.</param>
    /// <param name="context">The conversion the writers build in.</param>
    private Place(bool entry, IReadOnlyList<JsExpr> parts, int evaluated, IReadOnlyList<string> keys, bool computed,
        ConversionContext context)
    {
        _entry = entry;
        _parts = parts;
        _evaluated = evaluated;
        _keys = keys;
        _computed = computed;
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
            : Indexed(access, access.ArgumentList, context.Converter.ConvertIr(access.Expression), context);
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
        return Indexer.LoweredAt(target, context) is null ? null : Indexed(target, arguments, receiver, context);
    }

    /// <summary>
    /// What a lowering that evaluates the place itself (a deconstruction's captures, an initializer's
    /// arguments) evaluates, in C#'s order, and writes the place over (<see cref="Over"/>): the receiver,
    /// then each key C# evaluates. Where the keys are computed from them, it is ONE value, the receiver
    /// and the keys as C# computes them, before anything after the place runs: <c>[ring, ring.count -
    /// 1]</c>, a template the writer binds the receiver of where it is read twice.
    /// </summary>
    public IReadOnlyList<JsExpr> Evaluated => _computed
        ? [Template($"[{string.Join(", ", _keys.Prepend("{0}"))}]", _parts)]
        : _parts.Take(_evaluated).ToList();

    /// <summary>The same place over what its <see cref="Evaluated"/> parts were bound to, the constants
    /// kept: where the keys were computed, the receiver and the keys are read back from the one value.</summary>
    public Place Over(IReadOnlyList<JsExpr> evaluated)
    {
        if (!_computed) return new(_entry, [.. evaluated, .. _parts.Skip(_evaluated)], _evaluated, _keys, false, _context);
        List<JsExpr> held = [.. Enumerable.Range(0, _keys.Count + 1).Select(i => JsExpr.Index(evaluated[0], JsExpr.Literal(i.ToString())))];
        return new(_entry, held, held.Count, [.. _keys.Select((_, i) => Hole(i + 1))], false, _context);
    }

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
        return new Place(entry: true, [receiver, context.Converter.ConvertIr(key)], evaluated: 2, ["{1}"], computed: false,
            context);
    }

    /// <summary>The argument list of an element named by its keys, written with its receiver or without.</summary>
    private static BracketedArgumentListSyntax? ArgumentsOf(SyntaxNode node) => node switch
    {
        ElementAccessExpressionSyntax access => access.ArgumentList,
        ElementBindingExpressionSyntax binding => binding.ArgumentList,
        ImplicitElementAccessSyntax implicitAccess => implicitAccess.ArgumentList,
        _ => null,
    };

    /// <summary>
    /// An entry of an indexer a twin carries, its keys as the bound tree passes them (its
    /// <see cref="IArgumentOperation"/>s, in the order C# evaluates them): each key in its parameter's
    /// place, the elements of a params key in the array C# packs them into, and a key the call leaves
    /// out as the default the bound tree passes for it, after the keys C# evaluates, since nothing
    /// evaluates a constant. Every call passes every key, so the twin's <c>item</c> and <c>setItem</c>
    /// take them bare. Taken as written, <c>grid[1] = 5</c> over <c>this[int row, int col = 0]</c> put 5
    /// in <c>col</c> and left the value undefined, and <c>grid[col: 1, row: 2]</c> read row 1.
    /// </summary>
    private static Place? Indexed(ExpressionSyntax access, BracketedArgumentListSyntax arguments, JsExpr receiver,
        ConversionContext context)
    {
        var operation = context.SemanticHelper.GetOperation(access);
        if (operation is IImplicitIndexerReferenceOperation fromTheEnd)
            return FromTheEnd(access, arguments, receiver, fromTheEnd, context);
        if (operation is not IPropertyReferenceOperation { Property: var indexer } reference) return null;
        var written = new Written(access, arguments, context);
        var parts = new List<JsExpr> { receiver };
        var keys = new string?[indexer.Parameters.Length];
        var omitted = new List<IArgumentOperation>();
        foreach (var argument in reference.Arguments)
        {
            if (argument.Parameter is not { } parameter || parameter.Ordinal >= keys.Length) return null;
            if (argument.ArgumentKind == ArgumentKind.DefaultValue)
            {
                omitted.Add(argument);
                continue;
            }
            keys[parameter.Ordinal] = Hole(parts.Count);
            parts.Add(argument.ArgumentKind == ArgumentKind.ParamArray
                ? JsExpr.Array([.. ((argument.Value as IArrayCreationOperation)?.Initializer?.ElementValues ?? [])
                    .Select(element => written.Convert(element.Syntax))])
                : written.Convert(argument.Syntax));
        }
        var evaluated = parts.Count;
        foreach (var argument in omitted)
        {
            keys[argument.Parameter!.Ordinal] = Hole(parts.Count);
            parts.Add(Default(argument, context));
        }
        return keys.Any(key => key is null) ? null : Made(entry: false, parts, evaluated, keys!, computed: false, access, context);
    }

    /// <summary>
    /// A from-the-end key over a type that counts its elements, which C# binds to its <c>this[int]</c>:
    /// <c>ring[^1]</c> is <c>ring[ring.Count - 1]</c>, through the count the bound tree names (a
    /// <c>Count</c> or a <c>Length</c>). The position is computed from the receiver, after the offset,
    /// as C# reads them: receiver, offset, count, and only then a value. It went to the array lowering,
    /// which read <c>ring.length</c>, and its write passed the bare index to <c>setItem</c>. A writer
    /// that reads and writes the entry computes the position for each, before it evaluates a value,
    /// reading the count twice where C# reads it once, which only a count with an effect can tell.
    /// An index that is a System.Index value rather than <c>^n</c> has no translation and is refused.
    /// </summary>
    private static Place? FromTheEnd(ExpressionSyntax access, BracketedArgumentListSyntax arguments, JsExpr receiver,
        IImplicitIndexerReferenceOperation index, ConversionContext context)
    {
        if (index.Argument is not IUnaryOperation { OperatorKind: UnaryOperatorKind.Hat }
            || arguments.Arguments is not [{ Expression: PrefixUnaryExpressionSyntax hat }]
            || index.LengthSymbol is not IPropertySymbol count)
        {
            context.Unhandled(access, "index-from-end");
            return null;
        }
        var offset = context.Converter.ConvertIr(hat.Operand);
        var counted = $"{{0}}.{count.Name.ToCamelCase()}";
        // A literal offset reads nothing, so the count may come first. Any other is read first, and a
        // receiver read by its name is then bound once, as C# spills it, where an offset that has an
        // effect could reassign the name between the two reads (and the template's writer, seeing a name
        // read after a call, would bind the value of the write ahead of the count).
        if (offset is JsLiteral)
            return Made(entry: false, [receiver, offset], evaluated: 2, [$"{counted} - {{1}}"], computed: true, access, context);
        var once = receiver is JsIdentifier { Name: not ("this" or "super") } && !JsExprWriter.IsInlinable(offset)
            ? JsExpr.Group(receiver)
            : receiver;
        return Made(entry: false, [once, offset], evaluated: 2, [$"-{{1}} + {counted}"], computed: true, access, context);
    }

    /// <summary>
    /// The value a key the call leaves out takes: the constant the bound tree passes for it (its
    /// declared default, or what a caller-info attribute supplies), in the parameter's type, or the
    /// type's zero for a <c>default</c> that is no constant, a struct's.
    /// </summary>
    private static JsExpr Default(IArgumentOperation omitted, ConversionContext context) =>
        omitted.Value.ConstantValue is { HasValue: true } constant
        && ConstantLiteral.Write(constant.Value, omitted.Parameter!.Type, context) is { } literal
            ? JsExpr.Literal(literal)
            : JsExpr.Opaque(DefaultValue.Of(omitted.Parameter!.Type, context));

    /// <summary>
    /// The most parts a place takes: a template's holes are single digits, and a writer adds one part
    /// after them, its value or its operand.
    /// </summary>
    private const int MostParts = 9;

    /// <summary>The place, unless it takes more parts than a template holds, which is refused (EQ1004)
    /// rather than written into holes no template fills.</summary>
    private static Place? Made(bool entry, List<JsExpr> parts, int evaluated, IReadOnlyList<string> keys, bool computed,
        SyntaxNode at, ConversionContext context)
    {
        if (parts.Count <= MostParts) return new Place(entry, parts, evaluated, keys, computed, context);
        context.Unhandled(at, $"indexer's (it passes {parts.Count - 1} keys, and one read and write holds {MostParts - 1})");
        return null;
    }

    /// <summary>
    /// The keys as the access writes them, found by the in-tree argument the bound tree names: a strategy
    /// that rebuilt the access (the null-conditional's, a query's) copied its arguments position by
    /// position, and the copy is the one to convert.
    /// </summary>
    private sealed class Written(ExpressionSyntax access, BracketedArgumentListSyntax arguments, ConversionContext context)
    {
        private readonly BracketedArgumentListSyntax? _original = ArgumentsOf(context.SemanticHelper.Original(access));

        /// <summary>The written expression of a node of the bound tree (an argument, or an element of
        /// a params key), converted.</summary>
        public JsExpr Convert(SyntaxNode bound)
        {
            var argument = bound.AncestorsAndSelf().OfType<ArgumentSyntax>().FirstOrDefault();
            var at = argument is null || _original is null ? -1 : _original.Arguments.IndexOf(argument);
            var expression = at >= 0 && at < arguments.Arguments.Count
                ? arguments.Arguments[at].Expression
                : argument?.Expression ?? (ExpressionSyntax)bound;
            return context.Converter.ConvertIr(expression);
        }
    }
}
