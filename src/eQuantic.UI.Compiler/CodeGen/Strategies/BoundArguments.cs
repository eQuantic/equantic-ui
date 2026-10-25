using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using eQuantic.UI.Compiler.CodeGen.Ir;

namespace eQuantic.UI.Compiler.CodeGen.Strategies;

/// <summary>
/// A constructor call's arguments as C# binds them, read from the bound tree: a <c>new</c>, a
/// <c>: this(…)</c>, a <c>: base(…)</c> and a record's base clause. The bound tree lists the
/// arguments in the order C# EVALUATES them, which is the order they are written (a named argument
/// where it is written), and the defaults of the optional parameters left out after them, each with
/// the parameter it binds and how (an argument, a default, or the elements C# packs into a params
/// parameter's array). So every argument lands in its parameter's place, every argument is evaluated
/// in the order it is written, and a collection handed whole to a params parameter is spread, named
/// or not, while an element packed into one is passed as it is.
/// <para>
/// Each path wrote its own placement and each lost a case: <c>: base("square", Color: "red")</c>
/// went to <c>super('square', 'red')</c>, handing Sides the color; a base clause took every bare name
/// for a forwarded parameter, so <c>: Shape(DefaultKind)</c> named a constant as a variable no one
/// declared; <c>new Bag(1, Items: arr)</c> passed the array as the params array's one element; and
/// <c>: this(B: Log("b"), A: Log("a"))</c> evaluated A first.
/// </para>
/// </summary>
internal sealed class BoundArguments
{
    /// <summary>One argument in its parameter's place: the written argument it is (an index into
    /// <see cref="Written"/>), spread where it is a collection handed whole to a params parameter, or
    /// none (<c>-1</c>) for an optional parameter the call leaves out, whose twin takes its own
    /// default for the undefined it is handed.</summary>
    public readonly record struct Slot(int Written, bool Spread);

    private BoundArguments(IReadOnlyList<ExpressionSyntax> sources, IReadOnlyList<JsExpr> written,
        IReadOnlyList<IReadOnlyList<Slot>?> byParameter)
    {
        Sources = sources;
        Written = written;
        ByParameter = byParameter;
        // An optional parameter left out after the last argument is not passed at all.
        var slots = byParameter.SelectMany(placed => placed ?? [new Slot(-1, false)]).ToList();
        while (slots.Count > 0 && slots[^1].Written < 0) slots.RemoveAt(slots.Count - 1);
        Slots = slots;
    }

    /// <summary>The C# of each written argument, in the order it is evaluated: what the variables it
    /// declares (<c>out var n</c>) are read from.</summary>
    public IReadOnlyList<ExpressionSyntax> Sources { get; }

    /// <summary>Each written argument converted, in the order C# evaluates it.</summary>
    public IReadOnlyList<JsExpr> Written { get; }

    /// <summary>The arguments in parameter order, a params parameter's elements each a slot of its own.</summary>
    public IReadOnlyList<Slot> Slots { get; }

    /// <summary>What each parameter takes, in parameter order: its argument, the elements packed into a
    /// params parameter's array or collection (none for an empty one), or null for an optional parameter left out.</summary>
    public IReadOnlyList<IReadOnlyList<Slot>?> ByParameter { get; }

    /// <summary>Whether passing the arguments in parameter order evaluates them in the order they are
    /// written, as C# does: no named argument stands before one it follows in the signature.</summary>
    public bool InWrittenOrder
    {
        get
        {
            var next = 0;
            foreach (var slot in Slots.Where(slot => slot.Written >= 0))
                if (slot.Written != next++) return false;
            return true;
        }
    }

    /// <summary>
    /// The arguments of <paramref name="call"/> (an <see cref="IObjectCreationOperation"/>, or the
    /// <see cref="IInvocationOperation"/> of a constructor initializer or a base clause), each converted
    /// by <paramref name="convert"/>; null where the bound tree has no such call, and the caller keeps
    /// to the syntax.
    /// </summary>
    public static BoundArguments? Of(IOperation? call, Func<ExpressionSyntax, JsExpr> convert)
    {
        var (method, arguments) = call switch
        {
            IObjectCreationOperation { Constructor: { } constructor } creation => (constructor, creation.Arguments),
            IInvocationOperation invocation => (invocation.TargetMethod, invocation.Arguments),
            _ => (null, default),
        };
        if (method is null || arguments.IsDefault) return null;

        var sources = new List<ExpressionSyntax>();
        var written = new List<JsExpr>();
        var placed = new List<Slot>?[method.Parameters.Length];
        foreach (var argument in arguments)
        {
            if (argument.Parameter is not { } parameter) return null;
            switch (argument.ArgumentKind)
            {
                case ArgumentKind.DefaultValue:
                    placed[parameter.Ordinal] = null;
                    break;
                case ArgumentKind.ParamArray or ArgumentKind.ParamCollection:
                    // The array or the collection C# packs the elements into: each element written where it stands.
                    if (PackedElements(argument) is not { } elements) return null;
                    var slots = new List<Slot>();
                    foreach (var element in elements)
                    {
                        if (element.Syntax is not ExpressionSyntax source) return null;
                        sources.Add(source);
                        written.Add(convert(source));
                        slots.Add(new Slot(written.Count - 1, false));
                    }
                    placed[parameter.Ordinal] = slots;
                    break;
                case ArgumentKind.Explicit:
                    if (argument.Value.Syntax is not ExpressionSyntax value) return null;
                    sources.Add(value);
                    written.Add(convert(value));
                    // A collection handed whole to a params parameter is spread into the twin's rest parameter.
                    placed[parameter.Ordinal] = [new Slot(written.Count - 1, parameter.IsParams)];
                    break;
                default:
                    // A kind this does not know is never read as one written argument: the value of a
                    // packed collection is the whole call, and converting it converted the call again,
                    // until the stack ran out (Copilot's second review of #1062).
                    return null;
            }
        }

        return new BoundArguments(sources, written, placed);
    }

    /// <summary>
    /// The elements C# packs into a params parameter for a call that lists them: the array of a
    /// <c>params T[]</c>, or the collection of a <c>params IReadOnlyList&lt;T&gt;</c> and the other
    /// forms C# 13 takes. Null for an argument that is not packed, or packed in a form this does not know.
    /// </summary>
    public static ImmutableArray<IOperation>? PackedElements(IArgumentOperation argument) => argument switch
    {
        { ArgumentKind: ArgumentKind.ParamArray, Value: IArrayCreationOperation array } =>
            array.Initializer?.ElementValues ?? ImmutableArray<IOperation>.Empty,
        { ArgumentKind: ArgumentKind.ParamCollection, Value: ICollectionExpressionOperation collection } => collection.Elements,
        _ => null,
    };

    /// <summary>
    /// Whether <paramref name="argument"/> hands a collection WHOLE to a params parameter, which the rest
    /// parameter on the other side receives spread, where an element a call lists is passed as it is.
    /// The bound tree tells the two forms apart, and only it can: an array of ints handed to
    /// <c>params object[]</c> is ONE element, and a list handed to <c>params IReadOnlyList&lt;int&gt;</c>
    /// is the collection itself. Read from the argument's type, the array was spread into two elements
    /// and the list, not being an array, arrived as one (Copilot's second review of #1062).
    /// </summary>
    private static bool IsHandedWhole(ArgumentSyntax argument, ConversionContext context) =>
        context.SemanticHelper.GetOperation(argument) is IArgumentOperation
        {
            ArgumentKind: ArgumentKind.Explicit, Parameter.IsParams: true,
        };

    /// <summary>A call's written argument converted as its parameter takes it, where the call is read
    /// from its syntax: spread where it hands a collection whole to a params parameter
    /// (<see cref="IsHandedWhole"/>), and as it is otherwise.</summary>
    public static JsExpr Convert(ArgumentSyntax argument, ConversionContext context)
    {
        var value = context.Converter.ConvertIr(argument.Expression);
        return IsHandedWhole(argument, context) ? JsExpr.Spread(value) : value;
    }

    /// <summary>The arguments in parameter order, each the written argument it is, or what
    /// <paramref name="read"/> answers for it (a temporary it was evaluated into), spread where it
    /// is spread, and <c>undefined</c> for an optional parameter left out.</summary>
    public IReadOnlyList<JsExpr> InParameterOrder(Func<int, JsExpr>? read = null) =>
        Slots.Select(slot => slot.Written < 0
                ? JsExpr.Identifier("undefined")
                : slot.Spread ? JsExpr.Spread(Read(slot.Written, read)) : Read(slot.Written, read))
            .ToList();

    private JsExpr Read(int index, Func<int, JsExpr>? read = null) => read?.Invoke(index) ?? Written[index];

    /// <summary>
    /// <c>new <paramref name="type"/>(…)</c> with these arguments: passed in parameter order where that
    /// is the order they are written, and otherwise evaluated in the order they are written by the
    /// template's single evaluation (each bound once, as the arguments of the function that passes them
    /// in parameter order, so an <c>await</c> among them stays in the caller's function). A template
    /// holds ten parts; past them the same function is written out, its parameters `$a0`, `$a1`… in
    /// the order the arguments are written. It kept the parameter order instead, so eleven named
    /// arguments out of their order ran in the signature's (found by Copilot's review of #608).
    /// <paramref name="types"/>, for a construction of a generic exception class of the app's, builds it
    /// through <see cref="Eq.ExceptionConstruct"/> with the types it is, the arguments evaluated as they
    /// are for a <c>new</c>.
    /// </summary>
    public JsExpr New(string type, JsExpr? types = null) => Pass(
        arguments => types is null
            ? JsExpr.New(JsExpr.Identifier(type), arguments)
            : JsExpr.Call(JsExpr.Identifier(Eq.ExceptionConstruct), [JsExpr.Identifier(type), types, .. arguments]),
        types is null ? $"new {type}(" : $"{Eq.ExceptionConstruct}({type}, {JsExprWriter.Write(types)}, ");

    /// <summary><paramref name="function"/>(…) with these arguments, a runtime helper that takes them in
    /// its parameters' order, evaluated as <see cref="New"/> evaluates them.</summary>
    public JsExpr Call(string function) =>
        Pass(arguments => JsExpr.Call(JsExpr.Identifier(function), arguments), $"{function}(");

    /// <summary>The call <paramref name="built"/> writes from the arguments in parameter order, or, where
    /// that order is not the written one, the template that evaluates them as written and passes them on
    /// (<paramref name="head"/> is what stands before them in it).</summary>
    private JsExpr Pass(Func<IReadOnlyList<JsExpr>, JsExpr> built, string head)
    {
        // Literals and names alone can be read in any order: nothing among them runs.
        if (InWrittenOrder || Written.All(part => part is JsLiteral or JsIdentifier))
            return built(InParameterOrder());
        if (Written.Count > 10)
        {
            var parameters = string.Join(", ", Written.Select((_, index) => $"$a{index}"));
            var construction = built(InParameterOrder(index => JsExpr.Identifier($"$a{index}")));
            return JsExpr.Call(JsExpr.Arrow(parameters, construction), Written);
        }
        var holes = Slots.Select(slot => slot.Written < 0 ? "undefined" : (slot.Spread ? "..." : "") + $"{{{slot.Written}}}");
        return JsExpr.Template($"({head}{string.Join(", ", holes)}))", Written);
    }
}
